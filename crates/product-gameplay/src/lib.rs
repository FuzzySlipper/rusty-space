//! Product-owned gameplay content admission.
//!
//! This deliberately small vocabulary is an example product contract, not an
//! Engine grammar. TypeScript may materialize this exact wire format, while
//! Rust retains the only admission and semantic interpretation path.

#![forbid(unsafe_code)]

use serde::{Deserialize, Serialize};
use thiserror::Error;

mod ship_handling;
pub use ship_handling::{
    AuthoredShipHandling, SHIP_HANDLING_DOMAIN, SHIP_HANDLING_PACKAGE,
    SHIP_HANDLING_SCHEMA_VERSION, ShipHandlingDefinition, ShipHandlingError, compile_ship_handling,
};

pub const SCENE_SCHEMA_VERSION: u32 = 1;
const MAX_LABEL_BYTES: usize = 64;
const MAX_CUBE_SCALE: f32 = 8.0;

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct AuthoredScene {
    pub schema_version: u32,
    pub cube: AuthoredCube,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct AuthoredCube {
    pub label: String,
    pub color: [f32; 4],
    pub scale: f32,
}

#[derive(Debug, Clone, PartialEq)]
pub struct AdmittedScene {
    pub cube: AdmittedCube,
}

#[derive(Debug, Clone, PartialEq)]
pub struct AdmittedCube {
    pub label: String,
    pub color: [f32; 4],
    pub scale: f32,
}

#[derive(Debug, Error)]
pub enum AdmissionError {
    #[error("gameplay content is not valid JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("unsupported gameplay content schema version {actual}; expected {expected}")]
    UnsupportedSchema { expected: u32, actual: u32 },
    #[error("cube label must contain 1 through {MAX_LABEL_BYTES} non-whitespace UTF-8 bytes")]
    InvalidLabel,
    #[error("cube color component {index} must be finite and within 0 through 1")]
    InvalidColor { index: usize },
    #[error("cube scale must be finite and within 0 (exclusive) through {MAX_CUBE_SCALE}")]
    InvalidScale,
}

impl AuthoredScene {
    pub fn admit(self) -> Result<AdmittedScene, AdmissionError> {
        if self.schema_version != SCENE_SCHEMA_VERSION {
            return Err(AdmissionError::UnsupportedSchema {
                expected: SCENE_SCHEMA_VERSION,
                actual: self.schema_version,
            });
        }
        if self.cube.label.trim().is_empty() || self.cube.label.len() > MAX_LABEL_BYTES {
            return Err(AdmissionError::InvalidLabel);
        }
        for (index, component) in self.cube.color.iter().enumerate() {
            if !component.is_finite() || !(0.0..=1.0).contains(component) {
                return Err(AdmissionError::InvalidColor { index });
            }
        }
        if !self.cube.scale.is_finite()
            || !(0.0..=MAX_CUBE_SCALE).contains(&self.cube.scale)
            || self.cube.scale == 0.0
        {
            return Err(AdmissionError::InvalidScale);
        }

        Ok(AdmittedScene {
            cube: AdmittedCube {
                label: self.cube.label,
                color: self.cube.color,
                scale: self.cube.scale,
            },
        })
    }
}

pub fn decode_and_admit(bytes: &[u8]) -> Result<AdmittedScene, AdmissionError> {
    serde_json::from_slice::<AuthoredScene>(bytes)?.admit()
}

#[cfg(test)]
mod tests {
    use super::*;

    const VALID: &[u8] = br#"{"schemaVersion":1,"cube":{"label":"Rust-owned cube","color":[0.2,0.75,1.0,1.0],"scale":1.5}}"#;

    #[test]
    fn admits_the_product_owned_scene_shape() {
        let scene = decode_and_admit(VALID).expect("valid fixture admits");
        assert_eq!(scene.cube.label, "Rust-owned cube");
        assert_eq!(scene.cube.scale, 1.5);
    }

    #[test]
    fn rejects_unknown_fields_before_any_product_interpretation() {
        let error = decode_and_admit(br#"{"schemaVersion":1,"cube":{"label":"cube","color":[0.2,0.75,1.0,1.0],"scale":1.5,"liveEvaluator":"no"}}"#)
            .expect_err("unknown content is rejected");
        assert!(error.to_string().contains("unknown field"));
    }

    #[test]
    fn rejects_out_of_bounds_values() {
        let error = decode_and_admit(br#"{"schemaVersion":1,"cube":{"label":"cube","color":[0.2,0.75,1.0,1.0],"scale":9.0}}"#)
            .expect_err("oversized cube is rejected");
        assert!(matches!(error, AdmissionError::InvalidScale));
    }
}
