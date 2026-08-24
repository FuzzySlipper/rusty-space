//! Ship handling: the authored flight-feel constants, admitted from a
//! `gameplay-rules` package and compiled into a canonical definition.
//!
//! Rust owns the semantic meaning and validation; TypeScript authors the
//! values. The compiled definition is what the flight controller reads each
//! tick — the feel numbers never live as scattered Rust constants.

use serde::Deserialize;
use thiserror::Error;

use rusty_engine::gameplay_rules::{RulePackageError, decode_rule_package};

pub const SHIP_HANDLING_SCHEMA_VERSION: u64 = 1;
pub const SHIP_HANDLING_DOMAIN: &str = "rusty-space";
pub const SHIP_HANDLING_PACKAGE: &str = "core";

/// Generous but finite admission bounds; they reject nonsense while leaving
/// room to tune in the authoring catalog.
const MAX_SPEED: f64 = 10_000.0;
const MAX_THRUST: f64 = 1_000_000.0;
const MAX_TURN_RATE: f64 = 100.0;
const MAX_RESPONSE_TIME: f64 = 10.0;

/// Authored payload DTO (decoded candidate only). Mirrors
/// `gameplay/authoring/src/authoring/definitions.ts`.
#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct AuthoredShipHandling {
    pub schema_version: u64,
    pub max_speed: f64,
    pub max_thrust: f64,
    pub max_turn_rate: f64,
    pub throttle_response_time: f64,
    pub steering_response_time: f64,
}

/// Canonical compiled definition the runtime consumes.
#[derive(Debug, Clone, PartialEq)]
pub struct ShipHandlingDefinition {
    max_speed: f64,
    max_thrust: f64,
    max_turn_rate: f64,
    throttle_response_time: f64,
    steering_response_time: f64,
}

impl ShipHandlingDefinition {
    /// Construct a runtime-safe handling definition. Keeping the fields
    /// private prevents callers from bypassing the same bounds that package
    /// admission applies.
    pub fn new(
        max_speed: f64,
        max_thrust: f64,
        max_turn_rate: f64,
        throttle_response_time: f64,
        steering_response_time: f64,
    ) -> Result<Self, ShipHandlingError> {
        validate_field("maxSpeed", max_speed, MAX_SPEED)?;
        validate_field("maxThrust", max_thrust, MAX_THRUST)?;
        validate_field("maxTurnRate", max_turn_rate, MAX_TURN_RATE)?;
        validate_field(
            "throttleResponseTime",
            throttle_response_time,
            MAX_RESPONSE_TIME,
        )?;
        validate_field(
            "steeringResponseTime",
            steering_response_time,
            MAX_RESPONSE_TIME,
        )?;
        Ok(Self {
            max_speed,
            max_thrust,
            max_turn_rate,
            throttle_response_time,
            steering_response_time,
        })
    }

    pub const fn max_speed(&self) -> f64 {
        self.max_speed
    }

    pub const fn max_thrust(&self) -> f64 {
        self.max_thrust
    }

    pub const fn max_turn_rate(&self) -> f64 {
        self.max_turn_rate
    }

    pub const fn throttle_response_time(&self) -> f64 {
        self.throttle_response_time
    }

    pub const fn steering_response_time(&self) -> f64 {
        self.steering_response_time
    }
}

#[derive(Debug, Error)]
pub enum ShipHandlingError {
    #[error("ship handling package rejected: {0}")]
    Package(#[from] RulePackageError),
    #[error("ship handling package has unexpected identity {domain}/{package}")]
    WrongIdentity { domain: String, package: String },
    #[error("ship handling payload rejected: {0}")]
    Payload(#[from] serde_json::Error),
    #[error("unsupported ship handling schema version {actual}; expected {expected}")]
    UnsupportedSchema { expected: u64, actual: u64 },
    #[error("ship handling field {field} must be finite and within (0, {maximum}]")]
    InvalidField { field: &'static str, maximum: f64 },
}

/// Compile one canonical `rusty-space/core` package into the ship handling
/// definition. Fail-atomic: an error yields no partial definition.
pub fn compile_ship_handling(bytes: &[u8]) -> Result<ShipHandlingDefinition, ShipHandlingError> {
    let package = decode_rule_package(bytes)?;
    let identity = package.identity();
    if identity.domain().as_str() != SHIP_HANDLING_DOMAIN
        || identity.package().as_str() != SHIP_HANDLING_PACKAGE
    {
        return Err(ShipHandlingError::WrongIdentity {
            domain: identity.domain().as_str().to_owned(),
            package: identity.package().as_str().to_owned(),
        });
    }

    let mut payload_value = package.payload().clone();
    normalize_binary64_integers(&mut payload_value);
    let authored: AuthoredShipHandling = serde_json::from_value(payload_value)?;
    if authored.schema_version != SHIP_HANDLING_SCHEMA_VERSION {
        return Err(ShipHandlingError::UnsupportedSchema {
            expected: SHIP_HANDLING_SCHEMA_VERSION,
            actual: authored.schema_version,
        });
    }
    ShipHandlingDefinition::new(
        authored.max_speed,
        authored.max_thrust,
        authored.max_turn_rate,
        authored.throttle_response_time,
        authored.steering_response_time,
    )
}

fn validate_field(field: &'static str, value: f64, maximum: f64) -> Result<(), ShipHandlingError> {
    if !value.is_finite() || !(0.0..=maximum).contains(&value) || value == 0.0 {
        return Err(ShipHandlingError::InvalidField { field, maximum });
    }
    Ok(())
}

/// Schema 2 (binary64) canonicalizes every payload number as a float, so an
/// integer field like `schemaVersion: 1` arrives as `1.0`. Convert
/// integer-valued floats back to integers before typed deserialization, so
/// `u64` fields decode and integer-authored feel values stay exact.
fn normalize_binary64_integers(value: &mut serde_json::Value) {
    match value {
        serde_json::Value::Number(number) => {
            if number.is_i64() || number.is_u64() {
                return;
            }
            if let Some(float) = number.as_f64()
                && float.fract() == 0.0
                && float >= i64::MIN as f64
                && float <= i64::MAX as f64
            {
                *number = serde_json::Number::from(float as i64);
            }
        }
        serde_json::Value::Array(entries) => {
            for entry in entries.iter_mut() {
                normalize_binary64_integers(entry);
            }
        }
        serde_json::Value::Object(entries) => {
            for entry in entries.values_mut() {
                normalize_binary64_integers(entry);
            }
        }
        _ => {}
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn fixture() -> Vec<u8> {
        std::fs::read(
            std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("../../content/gameplay/rusty-space-core.package.json"),
        )
        .expect("committed ship handling package exists")
    }

    fn replace(bytes: &[u8], from: &str, to: &str) -> Vec<u8> {
        let text = String::from_utf8(bytes.to_vec()).expect("fixture is UTF-8");
        assert!(
            text.contains(from),
            "expected substring {from:?} in committed fixture"
        );
        text.replace(from, to).into_bytes()
    }

    #[test]
    fn committed_typescript_artifact_compiles_to_the_named_definition() {
        let handling = compile_ship_handling(&fixture()).expect("committed artifact compiles");
        assert_eq!(handling.max_speed(), 12.0);
        assert_eq!(handling.max_thrust(), 18.0);
        assert_eq!(handling.max_turn_rate(), 3.0);
        assert_eq!(handling.throttle_response_time(), 0.08);
        assert_eq!(handling.steering_response_time(), 0.12);
    }

    #[test]
    fn rejects_wrong_package_domain() {
        let bytes = replace(
            &fixture(),
            "\"domain\":\"rusty-space\"",
            "\"domain\":\"other-space\"",
        );
        let error = compile_ship_handling(&bytes).expect_err("wrong domain is rejected");
        assert!(matches!(error, ShipHandlingError::WrongIdentity { .. }));
    }

    #[test]
    fn rejects_unknown_payload_field() {
        let bytes = replace(&fixture(), "\"maxSpeed\":12", "\"maxSpeed\":12,\"bogus\":1");
        let error = compile_ship_handling(&bytes).expect_err("unknown field is rejected");
        assert!(matches!(error, ShipHandlingError::Payload(_)));
    }

    #[test]
    fn rejects_non_positive_speed() {
        let bytes = replace(&fixture(), "\"maxSpeed\":12", "\"maxSpeed\":0");
        let error = compile_ship_handling(&bytes).expect_err("zero speed is rejected");
        assert!(matches!(
            error,
            ShipHandlingError::InvalidField {
                field: "maxSpeed",
                ..
            }
        ));
    }

    #[test]
    fn direct_construction_revalidates_every_runtime_field() {
        for (field, handling) in [
            (
                "maxSpeed",
                ShipHandlingDefinition::new(0.0, 18.0, 3.0, 0.08, 0.12),
            ),
            (
                "maxThrust",
                ShipHandlingDefinition::new(12.0, -1.0, 3.0, 0.08, 0.12),
            ),
            (
                "maxTurnRate",
                ShipHandlingDefinition::new(12.0, 18.0, f64::NAN, 0.08, 0.12),
            ),
            (
                "throttleResponseTime",
                ShipHandlingDefinition::new(12.0, 18.0, 3.0, 11.0, 0.12),
            ),
            (
                "steeringResponseTime",
                ShipHandlingDefinition::new(12.0, 18.0, 3.0, 0.08, f64::INFINITY),
            ),
        ] {
            assert!(
                matches!(handling, Err(ShipHandlingError::InvalidField { field: rejected, .. }) if rejected == field)
            );
        }
    }

    #[test]
    fn rejects_unsupported_schema_version() {
        let bytes = replace(&fixture(), "\"schemaVersion\":1", "\"schemaVersion\":2");
        let error = compile_ship_handling(&bytes).expect_err("wrong schema is rejected");
        assert!(matches!(error, ShipHandlingError::UnsupportedSchema { .. }));
    }
}
