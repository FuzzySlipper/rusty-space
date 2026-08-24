//! One named product service that owns this product's admitted scene and
//! renderer-neutral projection. A real product replaces the export edge with
//! its selected host adapter while retaining this ownership boundary.

#![forbid(unsafe_code)]

use rusty_engine::render_model::{
    Geometry, Material, RenderDiff, RenderFrameDiff, RenderLayer, RenderMetadata, RenderNode,
    Transform,
};
use rusty_space_gameplay::{AdmissionError, AdmittedScene, decode_and_admit};
use thiserror::Error;

mod flight_runtime;
pub use flight_runtime::{FIXED_STEP_SECONDS, FlightReadout, FlightRuntime, FlightRuntimeError};

#[derive(Debug, Error)]
pub enum ProductServiceError {
    #[error(transparent)]
    Admission(#[from] AdmissionError),
    #[error("Rusty Engine rejected the projected retained frame: {0:?}")]
    Frame(rusty_engine::render_model::RenderFrameError),
}

#[derive(Debug, Clone)]
pub struct SpaceProductService {
    scene: AdmittedScene,
}

impl SpaceProductService {
    pub fn admit_gameplay(bytes: &[u8]) -> Result<Self, ProductServiceError> {
        Ok(Self {
            scene: decode_and_admit(bytes)?,
        })
    }

    /// Produces the complete initial frame from authoritative product facts.
    /// The browser may decode this JSON, but cannot create or change its meaning.
    pub fn initial_frame(&self) -> Result<RenderFrameDiff, ProductServiceError> {
        let cube = &self.scene.cube;
        let mut node = RenderNode::new(Geometry::Cube);
        node.material = Material {
            color: cube.color,
            wireframe: false,
        };
        node.transform = Transform {
            translation: [0.0, 0.0, -3.0],
            rotation: [0.24, 0.36, 0.0, 0.9],
            scale: [cube.scale, cube.scale, cube.scale],
        };
        node.layer = RenderLayer::Scene;
        node.metadata = RenderMetadata {
            source_entity: None,
            source_scene_node: None,
            tags: vec!["rusty-space".to_owned()],
            label: Some(cube.label.clone()),
        };
        RenderFrameDiff::try_from_ops(vec![RenderDiff::Create {
            handle: rusty_engine::render_model::RenderHandle::new(1),
            parent: None,
            node,
        }])
        .map_err(ProductServiceError::Frame)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn project_service_produces_a_valid_visible_cube_frame() {
        let service = SpaceProductService::admit_gameplay(
            br#"{"schemaVersion":1,"cube":{"label":"Rust-owned cube","color":[0.2,0.75,1.0,1.0],"scale":1.5}}"#,
        )
        .expect("fixture admits");
        let frame = service.initial_frame().expect("frame is valid");
        assert_eq!(frame.ops.len(), 1);
        assert!(matches!(frame.ops[0], RenderDiff::Create { .. }));
    }
}
