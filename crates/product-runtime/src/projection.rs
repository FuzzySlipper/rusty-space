//! Renderer-neutral projection of the flight state into a retained frame.
//!
//! The frame shows four things on the XZ navigation plane: the ship hull
//! (whose long axis is its heading), an explicit heading rod, a velocity rod
//! (length proportional to speed), and a projected-path rod along the velocity.
//! Heading (cyan) and velocity (orange) being distinct colors is what makes the
//! "heading is not velocity" invariant legible.

use rusty_engine::render_model::{
    Geometry, Material, RenderDiff, RenderFrameDiff, RenderHandle, RenderLayer, RenderMetadata,
    RenderNode, Transform,
};
use rusty_space_gameplay::Vec2;

use crate::flight_runtime::FlightReadout;

/// Stable render handles for the navigation nodes.
pub const SHIP_NODE_HANDLE: u64 = 1;
pub const HEADING_NODE_HANDLE: u64 = 2;
pub const VELOCITY_NODE_HANDLE: u64 = 3;
pub const PATH_NODE_HANDLE: u64 = 4;

const HEADING_LENGTH: f64 = 2.0;
const HEADING_THICKNESS: f32 = 0.05;
/// Velocity rod length = speed * this (seconds of travel shown).
const VELOCITY_SECONDS: f64 = 0.6;
const VELOCITY_THICKNESS: f32 = 0.06;
/// Projected-path rod length = speed * this, capped.
const PATH_SECONDS: f64 = 1.5;
const PATH_MAX_LENGTH: f64 = 40.0;
const PATH_THICKNESS: f32 = 0.03;

const SHIP_COLOR: [f32; 4] = [0.23, 0.79, 1.0, 1.0];
const HEADING_COLOR: [f32; 4] = [0.85, 1.0, 1.0, 1.0];
const VELOCITY_COLOR: [f32; 4] = [1.0, 0.55, 0.1, 1.0];
const PATH_COLOR: [f32; 4] = [0.45, 0.55, 0.65, 1.0];

/// Project the ship and its navigation rods into a frame: `Create` on the
/// first tick, transform-only `Update` afterward.
pub fn ship_frame_diff(readout: &FlightReadout, create: bool) -> RenderFrameDiff {
    let ship = ship_transform(readout);
    let heading = rod(
        readout.position,
        forward(readout.heading),
        HEADING_LENGTH,
        HEADING_THICKNESS,
    );
    let speed = readout.linear_velocity.magnitude();
    let direction = if speed > 1e-6 {
        readout.linear_velocity.scale(1.0 / speed)
    } else {
        Vec2::new(1.0, 0.0)
    };
    let velocity = rod(
        readout.position,
        direction,
        speed * VELOCITY_SECONDS,
        VELOCITY_THICKNESS,
    );
    let path_length = (speed * PATH_SECONDS).min(PATH_MAX_LENGTH);
    let path = rod(readout.position, direction, path_length, PATH_THICKNESS);

    let operations = if create {
        vec![
            create_node(SHIP_NODE_HANDLE, ship, SHIP_COLOR, "ship"),
            create_node(HEADING_NODE_HANDLE, heading, HEADING_COLOR, "heading"),
            create_node(VELOCITY_NODE_HANDLE, velocity, VELOCITY_COLOR, "velocity"),
            create_node(PATH_NODE_HANDLE, path, PATH_COLOR, "projected-path"),
        ]
    } else {
        vec![
            update_transform(SHIP_NODE_HANDLE, ship),
            update_transform(HEADING_NODE_HANDLE, heading),
            update_transform(VELOCITY_NODE_HANDLE, velocity),
            update_transform(PATH_NODE_HANDLE, path),
        ]
    };
    RenderFrameDiff::try_from_ops(operations).expect("ship frame is valid")
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
    yaw_transform(
        [readout.position.x, readout.position.z],
        readout.heading,
        [1.4, 0.3, 0.7],
    )
}

/// A thin elongated cube (a "rod") from `origin` along `direction`, used for
/// heading, velocity, and projected-path overlays.
fn rod(origin: Vec2, direction: Vec2, length: f64, thickness: f32) -> Transform {
    let yaw = direction.z.atan2(direction.x);
    let half = length / 2.0;
    yaw_transform(
        [origin.x + direction.x * half, origin.z + direction.z * half],
        yaw,
        [length as f32, thickness, thickness],
    )
}

fn forward(heading: f64) -> Vec2 {
    Vec2::new(heading.cos(), heading.sin())
}

fn yaw_transform(position: [f64; 2], yaw: f64, scale: [f32; 3]) -> Transform {
    Transform {
        translation: [position[0] as f32, 0.0, position[1] as f32],
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
        }
    }

    #[test]
    fn first_frame_creates_all_four_navigation_nodes() {
        let frame = ship_frame_diff(&readout(), true);
        assert_eq!(frame.ops.len(), 4);
        assert!(
            frame
                .ops
                .iter()
                .all(|operation| matches!(operation, RenderDiff::Create { .. }))
        );
    }

    #[test]
    fn later_frames_update_only_transforms() {
        let frame = ship_frame_diff(&readout(), false);
        assert_eq!(frame.ops.len(), 4);
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
    fn a_ship_at_rest_has_a_zero_length_velocity_rod() {
        let readout = FlightReadout {
            position: Vec2::ZERO,
            heading: 0.0,
            linear_velocity: Vec2::ZERO,
            angular_velocity: 0.0,
            throttle_level: 0.0,
        };
        let frame = ship_frame_diff(&readout, false);
        // The velocity rod has zero length (invisible) when speed is zero.
        match &frame.ops[2] {
            RenderDiff::Update {
                transform: Some(transform),
                ..
            } => assert_eq!(transform.scale[0], 0.0),
            other => panic!("expected velocity Update, got {other:?}"),
        }
    }
}
