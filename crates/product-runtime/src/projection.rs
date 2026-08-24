//! Renderer-neutral projection of the flight state into a retained frame.
//!
//! The frame shows the ship and navigation telemetry on the XZ plane: the hull
//! (whose long axis is its heading), explicit heading/velocity/path rods, a
//! live field-flow rod, and the one planet/wake source. Heading (cyan) and
//! velocity (orange) being distinct colors is what makes the "heading is not
//! velocity" invariant legible; the green/purple field nodes keep current and
//! wake state legible beside it.

use thiserror::Error;

use rusty_engine::render_model::{
    Geometry, Material, RenderDiff, RenderFrameDiff, RenderFrameError, RenderHandle, RenderLayer,
    RenderMetadata, RenderNode, Transform,
};
use rusty_space_gameplay::{FieldSample, Vec2};

use crate::flight_runtime::FlightReadout;

/// Stable render handles for the navigation nodes.
pub const SHIP_NODE_HANDLE: u64 = 1;
pub const HEADING_NODE_HANDLE: u64 = 2;
pub const VELOCITY_NODE_HANDLE: u64 = 3;
pub const PATH_NODE_HANDLE: u64 = 4;
pub const FIELD_FLOW_NODE_HANDLE: u64 = 5;
pub const FIELD_WAKE_NODE_HANDLE: u64 = 6;
pub const FIELD_PLANET_NODE_HANDLE: u64 = 7;

const HEADING_LENGTH: f64 = 2.0;
const HEADING_THICKNESS: f32 = 0.05;
/// Velocity rod length = speed * this (seconds of travel shown).
const VELOCITY_SECONDS: f64 = 0.6;
const VELOCITY_THICKNESS: f32 = 0.06;
/// Projected-path rod length = speed * this, capped.
const PATH_SECONDS: f64 = 1.5;
const PATH_MAX_LENGTH: f64 = 40.0;
const PATH_THICKNESS: f32 = 0.03;
const FIELD_THICKNESS: f32 = 0.08;
const FIELD_WAKE_THICKNESS: f32 = 0.045;

// The application host uses a top-down camera. Give coplanar overlays stable
// depth ordering so the wake cannot z-fight with (or completely hide) the
// ship and its navigation cues where they cross.
const WAKE_HEIGHT: f32 = -0.30;
const PATH_HEIGHT: f32 = -0.10;
const SHIP_HEIGHT: f32 = 0.10;
const VELOCITY_HEIGHT: f32 = 0.25;
const FIELD_HEIGHT: f32 = 0.40;
const HEADING_HEIGHT: f32 = 0.55;
const FIELD_LATERAL_OFFSET: f64 = 0.8;

const SHIP_COLOR: [f32; 4] = [0.23, 0.79, 1.0, 1.0];
const HEADING_COLOR: [f32; 4] = [0.85, 1.0, 1.0, 1.0];
const VELOCITY_COLOR: [f32; 4] = [1.0, 0.55, 0.1, 1.0];
const PATH_COLOR: [f32; 4] = [0.45, 0.55, 0.65, 1.0];
const FIELD_COLOR: [f32; 4] = [0.33, 1.0, 0.58, 1.0];
const FIELD_WAKE_COLOR: [f32; 4] = [0.92, 0.35, 0.88, 1.0];
const FIELD_PLANET_COLOR: [f32; 4] = [0.96, 0.72, 0.25, 1.0];

/// A readout could not be safely represented by the f32 renderer contract.
/// This remains a typed product failure instead of emitting invalid frame data
/// or panicking while constructing a retained frame.
#[derive(Debug, Error)]
pub enum FlightProjectionError {
    #[error("flight readout {field} must be finite")]
    NonFiniteReadout { field: &'static str },
    #[error("flight readout {field} is outside the renderer f32 range")]
    ReadoutOutOfRange { field: &'static str },
    #[error("projected renderer frame was rejected: {error:?}")]
    InvalidFrame { error: RenderFrameError },
}

/// Project the ship and its navigation rods into a frame: `Create` on the
/// first tick, transform-only `Update` afterward.
pub fn ship_frame_diff(
    readout: &FlightReadout,
    create: bool,
) -> Result<RenderFrameDiff, FlightProjectionError> {
    validate_readout(readout)?;
    let ship = ship_transform(readout);
    let heading = rod_at_height(
        readout.position,
        forward(readout.heading),
        HEADING_LENGTH,
        HEADING_THICKNESS,
        HEADING_HEIGHT,
    );
    let speed = readout.linear_velocity.magnitude();
    let direction = if speed > 1e-6 {
        readout.linear_velocity.scale(1.0 / speed)
    } else {
        Vec2::new(1.0, 0.0)
    };
    let velocity = rod_at_height(
        readout.position,
        direction,
        speed * VELOCITY_SECONDS,
        VELOCITY_THICKNESS,
        VELOCITY_HEIGHT,
    );
    let path_length = (speed * PATH_SECONDS).min(PATH_MAX_LENGTH);
    let path = rod_at_height(
        readout.position,
        direction,
        path_length,
        PATH_THICKNESS,
        PATH_HEIGHT,
    );
    let field_flow = field_flow_rod(readout.position, &readout.field);
    let field_wake = wake_transform();
    let field_planet = planet_transform();

    let operations = if create {
        vec![
            create_node(SHIP_NODE_HANDLE, ship, SHIP_COLOR, "ship"),
            create_node(HEADING_NODE_HANDLE, heading, HEADING_COLOR, "heading"),
            create_node(VELOCITY_NODE_HANDLE, velocity, VELOCITY_COLOR, "velocity"),
            create_node(PATH_NODE_HANDLE, path, PATH_COLOR, "projected-path"),
            create_node(
                FIELD_FLOW_NODE_HANDLE,
                field_flow,
                FIELD_COLOR,
                "field-flow",
            ),
            create_node(
                FIELD_WAKE_NODE_HANDLE,
                field_wake,
                FIELD_WAKE_COLOR,
                "field-wake",
            ),
            create_planet_node(FIELD_PLANET_NODE_HANDLE, field_planet),
        ]
    } else {
        vec![
            update_transform(SHIP_NODE_HANDLE, ship),
            update_transform(HEADING_NODE_HANDLE, heading),
            update_transform(VELOCITY_NODE_HANDLE, velocity),
            update_transform(PATH_NODE_HANDLE, path),
            update_transform(FIELD_FLOW_NODE_HANDLE, field_flow),
        ]
    };
    RenderFrameDiff::try_from_ops(operations)
        .map_err(|error| FlightProjectionError::InvalidFrame { error })
}

fn validate_readout(readout: &FlightReadout) -> Result<(), FlightProjectionError> {
    for (field, value) in [
        ("position.x", readout.position.x),
        ("position.z", readout.position.z),
        ("heading", readout.heading),
        ("linearVelocity.x", readout.linear_velocity.x),
        ("linearVelocity.z", readout.linear_velocity.z),
        ("angularVelocity", readout.angular_velocity),
        ("throttleLevel", readout.throttle_level),
    ] {
        if !value.is_finite() {
            return Err(FlightProjectionError::NonFiniteReadout { field });
        }
        if value.abs() > f32::MAX as f64 {
            return Err(FlightProjectionError::ReadoutOutOfRange { field });
        }
    }
    for (field, value) in [
        ("field.flowVelocity.x", readout.field.flow_velocity.x),
        ("field.flowVelocity.z", readout.field.flow_velocity.z),
        ("field.intensity", readout.field.intensity),
        ("field.turbulence.x", readout.field.turbulence.x),
        ("field.turbulence.z", readout.field.turbulence.z),
        ("field.gradient.x.x", readout.field.gradient[0][0]),
        ("field.gradient.x.z", readout.field.gradient[0][1]),
        ("field.gradient.z.x", readout.field.gradient[1][0]),
        ("field.gradient.z.z", readout.field.gradient[1][1]),
    ] {
        if !value.is_finite() {
            return Err(FlightProjectionError::NonFiniteReadout { field });
        }
        if value.abs() > f32::MAX as f64 {
            return Err(FlightProjectionError::ReadoutOutOfRange { field });
        }
    }
    if !(0.0..=1.0).contains(&readout.field.intensity) {
        return Err(FlightProjectionError::ReadoutOutOfRange {
            field: "field.intensity",
        });
    }
    Ok(())
}

fn create_node(handle: u64, transform: Transform, color: [f32; 4], name: &str) -> RenderDiff {
    let mut node = RenderNode::new(Geometry::Cube);
    node.material = Material {
        color,
        wireframe: false,
    };
    node.transform = transform;
    node.layer = RenderLayer::Scene;
    node.metadata = RenderMetadata {
        source_entity: None,
        source_scene_node: None,
        tags: vec![format!("rusty-space-{name}")],
        label: Some(name.to_owned()),
    };
    RenderDiff::Create {
        handle: RenderHandle::new(handle),
        parent: None,
        node,
    }
}

fn create_planet_node(handle: u64, transform: Transform) -> RenderDiff {
    let mut node = RenderNode::new(Geometry::Sphere);
    node.material = Material {
        color: FIELD_PLANET_COLOR,
        wireframe: false,
    };
    node.transform = transform;
    node.layer = RenderLayer::Scene;
    node.metadata = RenderMetadata {
        source_entity: None,
        source_scene_node: None,
        tags: vec!["rusty-space-field-planet".to_owned()],
        label: Some("field-planet".to_owned()),
    };
    RenderDiff::Create {
        handle: RenderHandle::new(handle),
        parent: None,
        node,
    }
}

fn update_transform(handle: u64, transform: Transform) -> RenderDiff {
    RenderDiff::Update {
        handle: RenderHandle::new(handle),
        transform: Some(transform),
        material: None,
        visible: None,
        metadata: None,
    }
}

fn ship_transform(readout: &FlightReadout) -> Transform {
    yaw_transform_at_height(
        [readout.position.x, readout.position.z],
        readout.heading,
        [1.4, 0.3, 0.7],
        SHIP_HEIGHT,
    )
}

fn field_flow_rod(position: Vec2, field: &FieldSample) -> Transform {
    let speed = field.flow_velocity.magnitude();
    let direction = if speed > 1e-6 {
        field.flow_velocity.scale(1.0 / speed)
    } else {
        Vec2::new(1.0, 0.0)
    };
    let origin = position + Vec2::new(-direction.z, direction.x).scale(FIELD_LATERAL_OFFSET);
    rod_at_height(
        origin,
        direction,
        (speed * 0.55).max(0.35),
        FIELD_THICKNESS,
        FIELD_HEIGHT,
    )
}

fn wake_transform() -> Transform {
    use rusty_space_gameplay::{FIELD_PLANET_POSITION, FIELD_WAKE_LENGTH};

    // A fixed ribbon makes the one analytic wake legible even before the ship
    // reaches it; the sampled flow/intensity in the HUD remains live.
    rod_at_height(
        Vec2::new(
            FIELD_PLANET_POSITION.x - FIELD_WAKE_LENGTH,
            FIELD_PLANET_POSITION.z,
        ),
        Vec2::new(1.0, 0.0),
        FIELD_WAKE_LENGTH,
        FIELD_WAKE_THICKNESS,
        WAKE_HEIGHT,
    )
}

fn planet_transform() -> Transform {
    use rusty_space_gameplay::FIELD_PLANET_POSITION;

    yaw_transform(
        [FIELD_PLANET_POSITION.x, FIELD_PLANET_POSITION.z],
        0.0,
        [1.4, 1.4, 1.4],
    )
}

/// A thin elongated cube (a "rod") from `origin` along `direction`, used for
/// heading, velocity, and projected-path overlays.
#[cfg(test)]
fn rod(origin: Vec2, direction: Vec2, length: f64, thickness: f32) -> Transform {
    rod_at_height(origin, direction, length, thickness, 0.0)
}

fn rod_at_height(
    origin: Vec2,
    direction: Vec2,
    length: f64,
    thickness: f32,
    height: f32,
) -> Transform {
    let yaw = direction.z.atan2(direction.x);
    let half = length / 2.0;
    yaw_transform_at_height(
        [origin.x + direction.x * half, origin.z + direction.z * half],
        yaw,
        [length as f32, thickness, thickness],
        height,
    )
}

fn forward(heading: f64) -> Vec2 {
    Vec2::new(heading.cos(), heading.sin())
}

fn yaw_transform(position: [f64; 2], yaw: f64, scale: [f32; 3]) -> Transform {
    yaw_transform_at_height(position, yaw, scale, 0.0)
}

fn yaw_transform_at_height(
    position: [f64; 2],
    yaw: f64,
    scale: [f32; 3],
    height: f32,
) -> Transform {
    Transform {
        translation: [position[0] as f32, height, position[1] as f32],
        // Yaw-only quaternion in x/y/z/w order.
        rotation: [0.0, (yaw / 2.0).sin() as f32, 0.0, (yaw / 2.0).cos() as f32],
        scale,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn readout() -> FlightReadout {
        FlightReadout {
            position: Vec2::new(3.0, -4.0),
            heading: std::f64::consts::FRAC_PI_2,
            linear_velocity: Vec2::new(1.0, 2.0),
            angular_velocity: 0.5,
            throttle_level: 0.75,
            field: rusty_space_gameplay::sample_field(Vec2::new(3.0, -4.0)),
        }
    }

    #[test]
    fn first_frame_creates_navigation_and_field_nodes() {
        let frame = ship_frame_diff(&readout(), true).expect("valid readout projects");
        assert_eq!(frame.ops.len(), 7);
        assert!(
            frame
                .ops
                .iter()
                .all(|operation| matches!(operation, RenderDiff::Create { .. }))
        );
    }

    #[test]
    fn later_frames_update_live_navigation_and_field_transforms() {
        let frame = ship_frame_diff(&readout(), false).expect("valid readout projects");
        assert_eq!(frame.ops.len(), 5);
        assert!(frame.ops.iter().all(|operation| {
            matches!(
                operation,
                RenderDiff::Update {
                    transform: Some(_),
                    material: None,
                    visible: None,
                    metadata: None,
                    ..
                }
            )
        }));
    }

    #[test]
    fn heading_and_velocity_rods_orient_differently_when_drifting() {
        let heading_rod = rod(Vec2::ZERO, forward(0.0), 2.0, 0.05);
        let velocity_rod = rod(Vec2::ZERO, Vec2::new(0.0, 1.0), 2.0, 0.06);
        // Facing +X (yaw 0) versus moving +Z (yaw 90°): the yaw lives in the
        // quaternion's y component (x/y/z/w order).
        assert_ne!(heading_rod.rotation, velocity_rod.rotation);
        assert!(heading_rod.rotation[1].abs() < 1e-6);
        assert!(velocity_rod.rotation[1].abs() > 1e-1);
    }

    #[test]
    fn top_down_overlays_have_stable_depth_and_the_field_cue_is_offset() {
        let readout = readout();
        let ship = ship_transform(&readout);
        let heading = rod_at_height(
            readout.position,
            forward(readout.heading),
            HEADING_LENGTH,
            HEADING_THICKNESS,
            HEADING_HEIGHT,
        );
        let field = field_flow_rod(readout.position, &readout.field);
        let wake = wake_transform();

        assert!(wake.translation[1] < ship.translation[1]);
        assert!(ship.translation[1] < field.translation[1]);
        assert!(field.translation[1] < heading.translation[1]);

        let flow = readout.field.flow_velocity;
        let direction = flow.scale(1.0 / flow.magnitude());
        let unoffset_midpoint = readout.position + direction.scale(flow.magnitude() * 0.55 / 2.0);
        let lateral_distance = Vec2::new(
            f64::from(field.translation[0]) - unoffset_midpoint.x,
            f64::from(field.translation[2]) - unoffset_midpoint.z,
        )
        .magnitude();
        assert!((lateral_distance - FIELD_LATERAL_OFFSET).abs() < 1e-5);
    }

    #[test]
    fn a_ship_at_rest_has_a_zero_length_velocity_rod() {
        let readout = FlightReadout {
            position: Vec2::ZERO,
            heading: 0.0,
            linear_velocity: Vec2::ZERO,
            angular_velocity: 0.0,
            throttle_level: 0.0,
            field: rusty_space_gameplay::sample_field(Vec2::ZERO),
        };
        let frame = ship_frame_diff(&readout, false).expect("valid readout projects");
        // The velocity rod has zero length (invisible) when speed is zero.
        match &frame.ops[2] {
            RenderDiff::Update {
                transform: Some(transform),
                ..
            } => assert_eq!(transform.scale[0], 0.0),
            other => panic!("expected velocity Update, got {other:?}"),
        }
    }

    #[test]
    fn non_finite_and_overflowing_readouts_return_typed_errors() {
        let mut non_finite = readout();
        non_finite.heading = f64::NAN;
        assert!(matches!(
            ship_frame_diff(&non_finite, false),
            Err(FlightProjectionError::NonFiniteReadout { field: "heading" })
        ));

        let mut non_finite_field = readout();
        non_finite_field.field.turbulence.z = f64::NAN;
        assert!(matches!(
            ship_frame_diff(&non_finite_field, false),
            Err(FlightProjectionError::NonFiniteReadout {
                field: "field.turbulence.z"
            })
        ));

        let mut overflowing = readout();
        overflowing.position.x = f64::from(f32::MAX) * 2.0;
        assert!(matches!(
            ship_frame_diff(&overflowing, false),
            Err(FlightProjectionError::ReadoutOutOfRange {
                field: "position.x"
            })
        ));

        let mut overflowing_field = readout();
        overflowing_field.field.gradient[1][0] = f64::from(f32::MAX) * 2.0;
        assert!(matches!(
            ship_frame_diff(&overflowing_field, false),
            Err(FlightProjectionError::ReadoutOutOfRange {
                field: "field.gradient.z.x"
            })
        ));

        // Every direct readout field fits f32, but the velocity rod's midpoint
        // overflows once it is added to this valid edge-position. Frame
        // validation must still return a typed failure rather than panic.
        let derived_overflow = FlightReadout {
            position: Vec2::new(f64::from(f32::MAX), f64::from(f32::MAX)),
            heading: 0.0,
            linear_velocity: Vec2::new(f64::from(f32::MAX), f64::from(f32::MAX)),
            angular_velocity: 0.0,
            throttle_level: 0.0,
            field: rusty_space_gameplay::sample_field(Vec2::ZERO),
        };
        assert!(matches!(
            ship_frame_diff(&derived_overflow, false),
            Err(FlightProjectionError::InvalidFrame { .. })
        ));
    }
}
