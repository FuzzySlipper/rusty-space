//! Ship handling: the authored flight-feel constants, admitted from a
//! `gameplay-rules` package and compiled into a canonical definition.
//!
//! Rust owns the semantic meaning and validation; TypeScript authors the
//! values. The compiled definition is what the flight controller reads each
//! tick — the feel numbers never live as scattered Rust constants.

use serde::Deserialize;
use thiserror::Error;

use rusty_engine::gameplay_rules::{
    AdmittedRulePackage, RulePackageError, RulePackageSchemaVersion, decode_canonical_rule_package,
};

pub const SHIP_HANDLING_SCHEMA_VERSION: u64 = 1;
pub const SHIP_HANDLING_DOMAIN: &str = "rusty-space";
pub const SHIP_HANDLING_PACKAGE: &str = "core";
pub const SHIP_HANDLING_PACKAGE_VERSION: u64 = 1;

const SHIP_HANDLING_SOURCE_ID: &str = "ship-handling";
const SHIP_HANDLING_SOURCE_PATH: &str = "gameplay/authoring/src/catalogs/ship.ts";
const SHIP_HANDLING_SUBJECT: &str = "rusty-space-ship";

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
    #[error("ship handling package has unexpected identity {domain}/{package}@{version}")]
    WrongIdentity {
        domain: String,
        package: String,
        version: u64,
    },
    #[error("ship handling package has unexpected envelope schema version {actual}")]
    WrongEnvelopeSchema { actual: u64 },
    #[error("ship handling package must not declare dependencies")]
    UnexpectedDependencies,
    #[error("ship handling package has unexpected source records")]
    UnexpectedSources,
    #[error("ship handling package has unexpected provenance records")]
    UnexpectedProvenance,
    #[error("ship handling payload rejected: {0}")]
    Payload(#[from] serde_json::Error),
    #[error("unsupported ship handling schema version {actual}; expected {expected}")]
    UnsupportedSchema { expected: u64, actual: u64 },
    #[error("ship handling field {field} must be finite and within (0, {maximum}]")]
    InvalidField { field: &'static str, maximum: f64 },
}

/// Compile one canonical, fully identified `rusty-space/core@1` package into
/// the ship handling definition. Fail-atomic: an error yields no partial
/// definition.
pub fn compile_ship_handling(bytes: &[u8]) -> Result<ShipHandlingDefinition, ShipHandlingError> {
    let package = decode_canonical_rule_package(bytes)?;
    validate_package_identity(&package)?;

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

fn validate_package_identity(package: &AdmittedRulePackage) -> Result<(), ShipHandlingError> {
    if package.schema_version() != RulePackageSchemaVersion::Binary64V2 {
        return Err(ShipHandlingError::WrongEnvelopeSchema {
            actual: package.schema_version().get(),
        });
    }

    let identity = package.identity();
    if identity.domain().as_str() != SHIP_HANDLING_DOMAIN
        || identity.package().as_str() != SHIP_HANDLING_PACKAGE
        || identity.version().get() != SHIP_HANDLING_PACKAGE_VERSION
    {
        return Err(ShipHandlingError::WrongIdentity {
            domain: identity.domain().as_str().to_owned(),
            package: identity.package().as_str().to_owned(),
            version: identity.version().get(),
        });
    }

    if !package.dependencies().is_empty() {
        return Err(ShipHandlingError::UnexpectedDependencies);
    }

    if !matches!(package.sources(), [source]
        if source.id().as_str() == SHIP_HANDLING_SOURCE_ID
            && source.path() == SHIP_HANDLING_SOURCE_PATH)
    {
        return Err(ShipHandlingError::UnexpectedSources);
    }

    if !matches!(package.provenance(), [provenance]
        if provenance.subject().as_str() == SHIP_HANDLING_SUBJECT
            && provenance.source().as_str() == SHIP_HANDLING_SOURCE_ID
            && provenance.line().is_none()
            && provenance.column().is_none())
    {
        return Err(ShipHandlingError::UnexpectedProvenance);
    }

    Ok(())
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
    use rusty_engine::gameplay_rules::{decode_rule_package, encode_rule_package};
    use serde_json::{Value, json};

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

    fn canonicalize(mutator: impl FnOnce(&mut Value)) -> Vec<u8> {
        let mut value: Value = serde_json::from_slice(&fixture()).expect("fixture is JSON");
        mutator(&mut value);
        let altered = serde_json::to_vec(&value).expect("altered fixture serializes");
        let admitted = decode_rule_package(&altered).expect("altered envelope remains valid");
        encode_rule_package(&admitted)
    }

    fn root(value: &mut Value) -> &mut serde_json::Map<String, Value> {
        value.as_object_mut().expect("fixture root is an object")
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
        let bytes = canonicalize(|value| {
            root(value).insert("domain".to_owned(), json!("other-space"));
        });
        let error = compile_ship_handling(&bytes).expect_err("wrong domain is rejected");
        assert!(matches!(error, ShipHandlingError::WrongIdentity { .. }));
    }

    #[test]
    fn rejects_wrong_package_version() {
        let bytes = canonicalize(|value| {
            root(value).insert("version".to_owned(), json!(2));
        });
        let error = compile_ship_handling(&bytes).expect_err("wrong version is rejected");
        assert!(matches!(
            error,
            ShipHandlingError::WrongIdentity { version: 2, .. }
        ));
    }

    #[test]
    fn rejects_wrong_envelope_schema_version() {
        let bytes = canonicalize(|value| {
            root(value).insert("schemaVersion".to_owned(), json!(1));
            root(value).insert(
                "payload".to_owned(),
                json!({
                    "schemaVersion": 1,
                    "maxSpeed": 12,
                    "maxThrust": 18,
                    "maxTurnRate": 3,
                    "throttleResponseTime": 1,
                    "steeringResponseTime": 1,
                }),
            );
        });
        let error = compile_ship_handling(&bytes).expect_err("wrong envelope schema is rejected");
        assert!(matches!(
            error,
            ShipHandlingError::WrongEnvelopeSchema { actual: 1 }
        ));
    }

    #[test]
    fn rejects_noncanonical_package_bytes() {
        let bytes = replace(&fixture(), "{\"kind\"", "{ \"kind\"");
        let error = compile_ship_handling(&bytes).expect_err("noncanonical bytes are rejected");
        assert!(matches!(
            error,
            ShipHandlingError::Package(RulePackageError::NonCanonicalArtifact { .. })
        ));
    }

    #[test]
    fn rejects_unexpected_envelope_closure() {
        let dependency = canonicalize(|value| {
            root(value).insert(
                "dependencies".to_owned(),
                json!([{"domain":"other","package":"rules","version":1}]),
            );
        });
        assert!(matches!(
            compile_ship_handling(&dependency),
            Err(ShipHandlingError::UnexpectedDependencies)
        ));

        let source = canonicalize(|value| {
            root(value).insert(
                "sources".to_owned(),
                json!([{"id":"other","path":"other.ts"}]),
            );
            root(value).insert(
                "provenance".to_owned(),
                json!([{"subject":"rusty-space-ship","source":"other"}]),
            );
        });
        assert!(matches!(
            compile_ship_handling(&source),
            Err(ShipHandlingError::UnexpectedSources)
        ));

        let provenance = canonicalize(|value| {
            root(value).insert(
                "provenance".to_owned(),
                json!([{"subject":"other-subject","source":"ship-handling"}]),
            );
        });
        assert!(matches!(
            compile_ship_handling(&provenance),
            Err(ShipHandlingError::UnexpectedProvenance)
        ));
    }

    #[test]
    fn rejects_source_path_and_provenance_location_mismatches() {
        let source_path = canonicalize(|value| {
            root(value).insert(
                "sources".to_owned(),
                json!([{"id":"ship-handling","path":"gameplay/authoring/src/catalogs/other.ts"}]),
            );
        });
        assert!(matches!(
            compile_ship_handling(&source_path),
            Err(ShipHandlingError::UnexpectedSources)
        ));

        for location in [json!({"line": 1}), json!({"column": 1})] {
            let provenance = canonicalize(|value| {
                let entry = root(value)
                    .get_mut("provenance")
                    .and_then(Value::as_array_mut)
                    .and_then(|entries| entries.first_mut())
                    .and_then(Value::as_object_mut)
                    .expect("fixture has provenance");
                entry.extend(location.as_object().expect("location is an object").clone());
            });
            assert!(matches!(
                compile_ship_handling(&provenance),
                Err(ShipHandlingError::UnexpectedProvenance)
            ));
        }
    }

    #[test]
    fn rejects_unknown_payload_field() {
        let bytes = canonicalize(|value| {
            root(value)
                .get_mut("payload")
                .and_then(Value::as_object_mut)
                .expect("payload is an object")
                .insert("bogus".to_owned(), json!(1));
        });
        let error = compile_ship_handling(&bytes).expect_err("unknown field is rejected");
        assert!(matches!(error, ShipHandlingError::Payload(_)));
    }

    #[test]
    fn rejects_unknown_nested_envelope_fields() {
        let mut value: Value = serde_json::from_slice(&fixture()).expect("fixture is JSON");
        root(&mut value)
            .get_mut("sources")
            .and_then(Value::as_array_mut)
            .and_then(|sources| sources.first_mut())
            .and_then(Value::as_object_mut)
            .expect("fixture has a source")
            .insert("nestedBogus".to_owned(), json!(true));
        let bytes = serde_json::to_vec(&value).expect("altered fixture serializes");
        let error = compile_ship_handling(&bytes).expect_err("unknown source field is rejected");
        assert!(matches!(
            error,
            ShipHandlingError::Package(RulePackageError::UnknownField { .. })
        ));
    }

    #[test]
    fn rejects_non_positive_speed() {
        let bytes = canonicalize(|value| {
            root(value)
                .get_mut("payload")
                .and_then(Value::as_object_mut)
                .expect("payload is an object")
                .insert("maxSpeed".to_owned(), json!(0));
        });
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
    fn rejects_nonfinite_payload_numbers_before_typed_compilation() {
        let bytes = replace(&fixture(), "\"maxSpeed\":12", "\"maxSpeed\":1e999");
        let error = compile_ship_handling(&bytes).expect_err("nonfinite JSON number is rejected");
        assert!(matches!(
            error,
            ShipHandlingError::Package(RulePackageError::JsonNumberOutOfRange { .. })
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
        let bytes = canonicalize(|value| {
            root(value)
                .get_mut("payload")
                .and_then(Value::as_object_mut)
                .expect("payload is an object")
                .insert("schemaVersion".to_owned(), json!(2));
        });
        let error = compile_ship_handling(&bytes).expect_err("wrong schema is rejected");
        assert!(matches!(error, ShipHandlingError::UnsupportedSchema { .. }));
    }

    #[test]
    fn admits_inclusive_numeric_bounds_and_rejects_values_just_beyond_them() {
        let at_bounds = canonicalize(|value| {
            root(value).insert(
                "payload".to_owned(),
                json!({
                    "schemaVersion": 1,
                    "maxSpeed": MAX_SPEED,
                    "maxThrust": MAX_THRUST,
                    "maxTurnRate": MAX_TURN_RATE,
                    "throttleResponseTime": MAX_RESPONSE_TIME,
                    "steeringResponseTime": MAX_RESPONSE_TIME,
                }),
            );
        });
        assert!(compile_ship_handling(&at_bounds).is_ok());

        let beyond_speed = canonicalize(|value| {
            root(value)
                .get_mut("payload")
                .and_then(Value::as_object_mut)
                .expect("payload is an object")
                .insert("maxSpeed".to_owned(), json!(MAX_SPEED + 0.5));
        });
        assert!(matches!(
            compile_ship_handling(&beyond_speed),
            Err(ShipHandlingError::InvalidField {
                field: "maxSpeed",
                ..
            })
        ));
    }
}
