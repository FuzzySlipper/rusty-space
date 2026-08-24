//! Transport-neutral owner of the live Rusty Space product.
//!
//! Adapters provide elapsed wall time and typed player intent. This service
//! admits the complete handling package, owns command and scheduling state,
//! advances the Engine-backed flight runtime, and retains the latest
//! renderer-neutral projection for delivery.

use std::time::Duration;

use thiserror::Error;

use rusty_engine::render_model::RenderFrameDiff;
use rusty_space_gameplay::{FlightCommand, ShipHandlingError, compile_ship_handling};

use crate::{FlightReadout, FlightRuntime, FlightRuntimeError, ship_frame_diff};

/// At most this many fixed ticks may remain accumulated after an advance.
/// Older excess elapsed time is intentionally discarded, preventing a stalled
/// adapter from carrying an unbounded simulation backlog forward.
pub const MAX_ACCUMULATED_STEPS: u32 = 4;

// One scheduler unit is one sixtieth of a nanosecond. Transport `Duration`s
// become exact 60 Hz work units by multiplying their integral nanoseconds by
// 60; each fixed step then consumes exactly one billion units.
const NANOSECONDS_PER_SECOND: u128 = 1_000_000_000;
const TICKS_PER_SECOND: u128 = 60;
const STEP_UNITS: u128 = NANOSECONDS_PER_SECOND;
const UNITS_PER_SECOND: u128 = NANOSECONDS_PER_SECOND * TICKS_PER_SECOND;
const MAX_ACCUMULATOR_UNITS: u128 = STEP_UNITS * MAX_ACCUMULATED_STEPS as u128;

/// A closed semantic command vocabulary for the live Space product.
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum SpaceProductCommand {
    /// Set the current main-drive and yaw intent.
    SetFlightIntent { throttle: f64, turn: f64 },
}

/// The normalized command that the service accepted.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct SpaceProductCommandReceipt {
    pub sequence: u64,
    pub command: SpaceProductCommand,
}

/// A renderer-neutral product update retained for delivery by any adapter.
#[derive(Debug, Clone, serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SpaceProductUpdate {
    pub sequence: u64,
    pub tick: u64,
    pub frame: RenderFrameDiff,
    pub readout: FlightReadout,
}

/// Result of adding elapsed wall-clock time to the product clock.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct SpaceProductAdvanceReceipt {
    pub steps: u32,
    pub remaining_seconds: f64,
    pub discarded_seconds: f64,
}

#[derive(Debug, Error)]
pub enum SpaceProductServiceError {
    #[error("live handling admission rejected: {0}")]
    Admission(#[from] ShipHandlingError),
    #[error("live flight runtime rejected: {0}")]
    Runtime(#[from] FlightRuntimeError),
    #[error("flight intent {field} must be finite and within {minimum}..={maximum}; got {actual}")]
    InvalidCommand {
        field: &'static str,
        minimum: f64,
        maximum: f64,
        actual: f64,
    },
}

/// One admitted, live Rusty Space product instance.
///
/// This deliberately has no sockets, threads, clocks, or browser types. A
/// browser, desktop, or in-process adapter can supply elapsed time and deliver
/// [`SpaceProductUpdate`] without duplicating product meaning.
pub struct SpaceProductService {
    runtime: FlightRuntime,
    command: FlightCommand,
    command_sequence: u64,
    accumulator_units: u128,
    tick: u64,
    readout: FlightReadout,
    latest_update: SpaceProductUpdate,
}

impl SpaceProductService {
    /// Admit the complete current handling package and construct the live
    /// service. Admission fails closed: no partially initialized service is
    /// returned if package compilation or runtime spawn fails.
    pub fn admit(handling_bytes: &[u8]) -> Result<Self, SpaceProductServiceError> {
        let handling = compile_ship_handling(handling_bytes)?;
        let runtime = FlightRuntime::spawn(handling)?;
        let readout = runtime.readout()?;
        let latest_update = SpaceProductUpdate {
            sequence: 0,
            tick: 0,
            frame: ship_frame_diff(&readout, true),
            readout,
        };
        Ok(Self {
            runtime,
            command: FlightCommand {
                throttle: 0.0,
                turn: 0.0,
            },
            command_sequence: 0,
            accumulator_units: 0,
            tick: 0,
            readout,
            latest_update,
        })
    }

    /// Validate, normalize, and retain a semantic command for subsequent
    /// fixed steps. Invalid commands leave the current command unchanged.
    pub fn submit_command(
        &mut self,
        command: SpaceProductCommand,
    ) -> Result<SpaceProductCommandReceipt, SpaceProductServiceError> {
        let command = normalize_command(command)?;
        self.command = flight_command(command);
        self.command_sequence = self.command_sequence.saturating_add(1);
        Ok(SpaceProductCommandReceipt {
            sequence: self.command_sequence,
            command,
        })
    }

    /// Add adapter-observed wall-clock time and run bounded fixed simulation
    /// steps. The service owns all accumulation and projection choices.
    pub fn advance_elapsed(
        &mut self,
        elapsed: Duration,
    ) -> Result<SpaceProductAdvanceReceipt, SpaceProductServiceError> {
        let elapsed_units = elapsed.as_nanos().saturating_mul(TICKS_PER_SECOND);
        self.accumulator_units = self.accumulator_units.saturating_add(elapsed_units);

        let mut steps = 0;
        while self.accumulator_units >= STEP_UNITS && steps < MAX_ACCUMULATED_STEPS {
            self.readout = self.runtime.tick(self.command)?;
            self.accumulator_units -= STEP_UNITS;
            self.tick = self.tick.saturating_add(1);
            self.latest_update = SpaceProductUpdate {
                sequence: self.tick,
                tick: self.tick,
                frame: ship_frame_diff(&self.readout, false),
                readout: self.readout,
            };
            steps += 1;
        }

        let discarded_units = self.accumulator_units.saturating_sub(MAX_ACCUMULATOR_UNITS);
        self.accumulator_units = self.accumulator_units.min(MAX_ACCUMULATOR_UNITS);

        Ok(SpaceProductAdvanceReceipt {
            steps,
            remaining_seconds: units_to_seconds(self.accumulator_units),
            discarded_seconds: units_to_seconds(discarded_units),
        })
    }

    /// The authoritative readout after the most recently completed fixed step.
    pub const fn readout(&self) -> FlightReadout {
        self.readout
    }

    /// The command currently applied by future fixed steps.
    pub const fn current_command(&self) -> SpaceProductCommand {
        SpaceProductCommand::SetFlightIntent {
            throttle: self.command.throttle,
            turn: self.command.turn,
        }
    }

    /// The latest renderer-neutral update, including the admitted startup
    /// frame before the first elapsed-time advance.
    pub const fn latest_update(&self) -> &SpaceProductUpdate {
        &self.latest_update
    }
}

fn units_to_seconds(units: u128) -> f64 {
    (units as f64) / (UNITS_PER_SECOND as f64)
}

fn normalize_command(
    command: SpaceProductCommand,
) -> Result<SpaceProductCommand, SpaceProductServiceError> {
    let SpaceProductCommand::SetFlightIntent { throttle, turn } = command;
    validate_command_field("throttle", throttle, 0.0, 1.0)?;
    validate_command_field("turn", turn, -1.0, 1.0)?;
    Ok(SpaceProductCommand::SetFlightIntent {
        throttle: normalize_zero(throttle),
        turn: normalize_zero(turn),
    })
}

fn validate_command_field(
    field: &'static str,
    value: f64,
    minimum: f64,
    maximum: f64,
) -> Result<(), SpaceProductServiceError> {
    if !value.is_finite() || !(minimum..=maximum).contains(&value) {
        return Err(SpaceProductServiceError::InvalidCommand {
            field,
            minimum,
            maximum,
            actual: value,
        });
    }
    Ok(())
}

fn normalize_zero(value: f64) -> f64 {
    if value == 0.0 { 0.0 } else { value }
}

fn flight_command(command: SpaceProductCommand) -> FlightCommand {
    let SpaceProductCommand::SetFlightIntent { throttle, turn } = command;
    FlightCommand { throttle, turn }
}

#[cfg(test)]
mod tests {
    use super::*;
    use rusty_engine::render_model::RenderDiff;

    const HANDLING: &[u8] =
        include_bytes!("../../../content/gameplay/rusty-space-core.package.json");

    #[test]
    fn admits_complete_handling_and_projects_the_startup_frame() {
        let service = SpaceProductService::admit(HANDLING).expect("admitted service");

        assert_eq!(service.readout().position, rusty_space_gameplay::Vec2::ZERO);
        assert_eq!(service.latest_update().sequence, 0);
        assert!(
            service
                .latest_update()
                .frame
                .ops
                .iter()
                .all(|operation| matches!(operation, RenderDiff::Create { .. }))
        );
    }

    #[test]
    fn failed_handling_admission_yields_no_service() {
        let error = match SpaceProductService::admit(b"not a gameplay package") {
            Err(error) => error,
            Ok(_) => panic!("invalid package cannot create a service"),
        };
        assert!(matches!(error, SpaceProductServiceError::Admission(_)));
    }

    #[test]
    fn rejected_command_does_not_replace_the_current_intent() {
        let mut service = SpaceProductService::admit(HANDLING).expect("admitted service");
        let accepted = service
            .submit_command(SpaceProductCommand::SetFlightIntent {
                throttle: 0.75,
                turn: -0.5,
            })
            .expect("valid command");
        assert_eq!(accepted.sequence, 1);

        let error = service
            .submit_command(SpaceProductCommand::SetFlightIntent {
                throttle: f64::NAN,
                turn: 0.0,
            })
            .expect_err("non-finite intent is rejected");
        assert!(matches!(
            error,
            SpaceProductServiceError::InvalidCommand {
                field: "throttle",
                ..
            }
        ));
        assert!(matches!(
            service.submit_command(SpaceProductCommand::SetFlightIntent {
                throttle: 2.0,
                turn: 0.0,
            }),
            Err(SpaceProductServiceError::InvalidCommand {
                field: "throttle",
                ..
            })
        ));
        assert_eq!(
            service.current_command(),
            SpaceProductCommand::SetFlightIntent {
                throttle: 0.75,
                turn: -0.5,
            }
        );
    }

    #[test]
    fn elapsed_time_accumulates_deterministically_and_bounds_backlog() {
        let mut zero = SpaceProductService::admit(HANDLING).expect("admitted service");
        assert_eq!(
            zero.advance_elapsed(Duration::ZERO).unwrap(),
            SpaceProductAdvanceReceipt {
                steps: 0,
                remaining_seconds: 0.0,
                discarded_seconds: 0.0,
            }
        );

        assert_partition_equivalent(33_333_332, &[16_666_666, 16_666_666], 1);

        // Three ticks are exactly 50 ms, so these cover the integer scheduler
        // just below, exactly at, and just above an exact fixed-step boundary.
        assert_partition_equivalent(49_999_999, &[16_666_666, 33_333_333], 2);
        assert_partition_equivalent(50_000_000, &[16_666_666, 33_333_334], 3);
        assert_partition_equivalent(50_000_001, &[16_666_666, 33_333_335], 3);

        // The four-step backlog cap is also partition invariant as long as
        // the total fits its bounded service input window.
        assert_partition_equivalent(66_666_667, &[50_000_000, 16_666_667], 4);

        let mut bounded = SpaceProductService::admit(HANDLING).expect("admitted service");
        let receipt = bounded.advance_elapsed(Duration::from_secs(10)).unwrap();
        assert_eq!(receipt.steps, MAX_ACCUMULATED_STEPS);
        assert!(receipt.discarded_seconds > 9.0);

        let mut largest = SpaceProductService::admit(HANDLING).expect("admitted service");
        let receipt = largest.advance_elapsed(Duration::MAX).unwrap();
        assert_eq!(receipt.steps, MAX_ACCUMULATED_STEPS);
        assert!(receipt.discarded_seconds.is_finite());
        assert!(receipt.discarded_seconds > 1.0e18);
    }

    fn assert_partition_equivalent(total_nanoseconds: u64, parts: &[u64], expected_steps: u32) {
        assert_eq!(parts.iter().sum::<u64>(), total_nanoseconds);
        let mut batched = SpaceProductService::admit(HANDLING).expect("admitted service");
        let mut partitioned = SpaceProductService::admit(HANDLING).expect("admitted service");
        let intent = SpaceProductCommand::SetFlightIntent {
            throttle: 0.5,
            turn: 0.25,
        };
        batched.submit_command(intent).unwrap();
        partitioned.submit_command(intent).unwrap();

        let batched_receipt = batched
            .advance_elapsed(Duration::from_nanos(total_nanoseconds))
            .unwrap();
        let mut partitioned_steps = 0;
        let mut partitioned_discarded = 0.0;
        let mut partitioned_receipt = SpaceProductAdvanceReceipt {
            steps: 0,
            remaining_seconds: 0.0,
            discarded_seconds: 0.0,
        };
        for nanoseconds in parts {
            partitioned_receipt = partitioned
                .advance_elapsed(Duration::from_nanos(*nanoseconds))
                .unwrap();
            partitioned_steps += partitioned_receipt.steps;
            partitioned_discarded += partitioned_receipt.discarded_seconds;
        }

        assert_eq!(batched_receipt.steps, expected_steps);
        assert_eq!(partitioned_steps, expected_steps);
        assert_eq!(
            batched_receipt.remaining_seconds,
            partitioned_receipt.remaining_seconds
        );
        assert_eq!(batched_receipt.discarded_seconds, partitioned_discarded);
        assert_eq!(batched.readout(), partitioned.readout());
        assert_eq!(
            batched.latest_update().tick,
            partitioned.latest_update().tick
        );
    }

    #[test]
    fn stepped_projection_updates_the_retained_startup_nodes() {
        let mut service = SpaceProductService::admit(HANDLING).expect("admitted service");
        service
            .advance_elapsed(Duration::from_nanos(16_666_667))
            .unwrap();

        assert_eq!(service.latest_update().sequence, 1);
        assert!(
            service
                .latest_update()
                .frame
                .ops
                .iter()
                .all(|operation| matches!(operation, RenderDiff::Update { .. }))
        );
    }
}
