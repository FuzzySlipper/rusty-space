//! Renderer-neutral projection of the flight state into a retained frame.
//!
//! The browser host serializes these diffs and the shell applies them through
//! the Engine application host; neither side owns gameplay meaning.

use rusty_engine::render_model::{
    Geometry, Material, RenderDiff, RenderFrameDiff, RenderHandle, RenderLayer, RenderMetadata,
    RenderNode, Transform,
};

use crate::flight_runtime::FlightReadout;

/// Stable render handle for the ship marker node.
pub const SHIP_NODE_HANDLE: u64 = 1;

/// Project the ship into a frame: `Create` on the first tick, `Update`
/// (transform only) afterward.
pub fn ship_frame_diff(readout: &FlightReadout, create: bool) -> RenderFrameDiff {
    let transform = ship_transform(readout);
    let operation = if create {
        let mut node = RenderNode::new(Geometry::Cube);
        node.material = Material {
            color: [0.23, 0.79, 1.0, 1.0],
            wireframe: false,
        };
        node.transform = transform;
        node.layer = RenderLayer::Scene;
        node.metadata = RenderMetadata {
            source_entity: None,
            source_scene_node: None,
            tags: vec!["rusty-space-ship".to_owned()],
            label: Some("ship".to_owned()),
        };
        RenderDiff::Create {
            handle: RenderHandle::new(SHIP_NODE_HANDLE),
            parent: None,
            node,
        }
    } else {
        RenderDiff::Update {
            handle: RenderHandle::new(SHIP_NODE_HANDLE),
            transform: Some(transform),
            material: None,
            visible: None,
            metadata: None,
        }
    };
    RenderFrameDiff::try_from_ops(vec![operation]).expect("ship frame is valid")
}

fn ship_transform(readout: &FlightReadout) -> Transform {
    let half = readout.heading / 2.0;
    Transform {
        translation: [readout.position.x as f32, 0.0, readout.position.z as f32],
        // Yaw-only quaternion in x/y/z/w order.
        rotation: [0.0, half.sin() as f32, 0.0, half.cos() as f32],
        // A flat, forward-elongated hull so heading is legible.
        scale: [1.4, 0.3, 0.7],
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use rusty_space_gameplay::Vec2;

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
    fn first_frame_creates_the_ship_node() {
        let frame = ship_frame_diff(&readout(), true);
        assert_eq!(frame.ops.len(), 1);
        assert!(matches!(frame.ops[0], RenderDiff::Create { .. }));
    }

    #[test]
    fn later_frames_update_only_the_transform() {
        let frame = ship_frame_diff(&readout(), false);
        assert_eq!(frame.ops.len(), 1);
        match &frame.ops[0] {
            RenderDiff::Update {
                transform: Some(transform),
                material: None,
                visible: None,
                metadata: None,
                ..
            } => {
                assert_eq!(transform.translation, [3.0, 0.0, -4.0]);
            }
            other => panic!("expected transform-only Update, got {other:?}"),
        }
    }
}
