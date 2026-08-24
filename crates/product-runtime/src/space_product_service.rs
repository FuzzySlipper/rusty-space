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

use crate::{
    FlightProjectionError, FlightReadout, FlightRuntime, FlightRuntimeError, ship_frame_diff,
};

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
#[derive(Debug, Clone, Copy, PartialEq, serde::Serialize)]
#[serde(tag = "type", rename_all = "camelCase")]
pub enum SpaceProductCommand {
    /// Set the current main-drive and yaw intent.
    SetFlightIntent { throttle: f64, turn: f64 },
}

/// The normalized command that the service accepted.
#[derive(Debug, Clone, Copy, PartialEq, serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SpaceProductCommandReceipt {
    pub sequence: u64,
    pub command: SpaceProductCommand,
}

/// The monotonically increasing identity of one adapter session.
///
/// A session identity is minted by the Rust service, not a browser. It fences
/// a controller lease, so a delayed message from a replaced transport cannot
/// mutate the current product command.
#[derive(Debug, Clone, Copy, PartialEq, Eq, serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SpaceProductSession {
    pub generation: u64,
}

/// The complete renderer and readout state required before a session may
/// consume retained-frame diffs.
#[derive(Debug, Clone, serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SpaceProductSessionBaseline {
    pub session: SpaceProductSession,
    pub update: SpaceProductUpdate,
}

/// The effect of an adapter declaring a session unavailable.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SpaceProductSessionRelease {
    /// This session owned the controller lease and its held input was cleared.
    ReleasedController,
    /// A newer session already replaced this generation, so no state changed.
    AlreadyStale,
}

/// A command was received from a session that no longer owns the controller
/// lease.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Error)]
#[error(
    "session generation {session_generation} is stale; active controller is {active_generation:?}"
)]
pub struct SpaceProductStaleSessionError {
    pub session_generation: u64,
    pub active_generation: Option<u64>,
}

/// The service has issued every representable session generation. Refusing a
/// new session is safer than silently reusing an identity that could be stale.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Error)]
#[error("session generation space is exhausted")]
pub struct SpaceProductSessionGenerationExhausted;

/// Opening a controller session failed before the service changed its lease.
#[derive(Debug, Error)]
pub enum SpaceProductSessionOpenError {
    #[error(transparent)]
    GenerationExhausted(#[from] SpaceProductSessionGenerationExhausted),
    #[error(transparent)]
    Projection(#[from] FlightProjectionError),
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
    #[error("renderer projection rejected: {0}")]
    Projection(#[from] FlightProjectionError),
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
    next_session_generation: u64,
    controller: Option<SpaceProductSession>,
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
            frame: ship_frame_diff(&readout, true)?,
            readout,
        };
        Ok(Self {
            runtime,
            command: FlightCommand {
                throttle: 0.0,
                turn: 0.0,
            },
            command_sequence: 0,
            next_session_generation: 0,
            controller: None,
            accumulator_units: 0,
            tick: 0,
            readout,
            latest_update,
        })
    }

    /// Validate, normalize, and retain a semantic command for subsequent
    /// fixed steps. Invalid commands leave the current command unchanged.
    fn submit_command(
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

    /// Open an adapter session and replace any prior controller lease.
    ///
    /// The returned snapshot is always a complete retained frame, even if the
    /// product has already advanced for many ticks. This lets each transport
    /// initialize an empty renderer before it receives update-only diffs.
    pub fn open_session(
        &mut self,
    ) -> Result<SpaceProductSessionBaseline, SpaceProductSessionOpenError> {
        let next_session_generation = self
            .next_session_generation
            .checked_add(1)
            .ok_or(SpaceProductSessionGenerationExhausted)?;
        let session = SpaceProductSession {
            generation: next_session_generation,
        };
        let frame = ship_frame_diff(&self.readout, true)?;
        // Do not publish a new lease until its complete baseline is valid.
        self.next_session_generation = next_session_generation;
        self.controller = Some(session);
        self.neutralize_command();
        Ok(SpaceProductSessionBaseline {
            session,
            update: SpaceProductUpdate {
                sequence: self.latest_update.sequence,
                tick: self.tick,
                frame,
                readout: self.readout,
            },
        })
    }

    /// Submit a semantic command only when its owning session still holds the
    /// single-controller lease.
    pub fn submit_session_command(
        &mut self,
        session: SpaceProductSession,
        command: SpaceProductCommand,
    ) -> Result<SpaceProductCommandReceipt, SpaceProductSessionError> {
        if self.controller != Some(session) {
            return Err(SpaceProductSessionError::StaleSession(
                SpaceProductStaleSessionError {
                    session_generation: session.generation,
                    active_generation: self.controller.map(|active| active.generation),
                },
            ));
        }
        self.submit_command(command)
            .map_err(SpaceProductSessionError::InvalidCommand)
    }

    /// Release a session. Only the active controller can clear live input;
    /// delayed teardown from a replaced transport is deliberately harmless.
    pub fn release_session(&mut self, session: SpaceProductSession) -> SpaceProductSessionRelease {
        if self.controller == Some(session) {
            self.controller = None;
            self.neutralize_command();
            SpaceProductSessionRelease::ReleasedController
        } else {
            SpaceProductSessionRelease::AlreadyStale
        }
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
            let next_readout = self.runtime.tick(self.command)?;
            let next_tick = self.tick.saturating_add(1);
            let next_update = SpaceProductUpdate {
                sequence: next_tick,
                tick: next_tick,
                frame: ship_frame_diff(&next_readout, false)?,
                readout: next_readout,
            };
            // A completed Engine tick becomes the authoritative retained
            // product state before attempting another tick. If a later step
            // rejects, runtime and published service state remain aligned at
            // this last successful boundary.
            self.accumulator_units -= STEP_UNITS;
            self.tick = next_tick;
            self.readout = next_readout;
            self.latest_update = next_update;
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

    fn neutralize_command(&mut self) {
        self.command = FlightCommand {
            throttle: 0.0,
            turn: 0.0,
        };
    }
}

/// Session-scoped command failure. Adapters can map this closed result to a
/// typed receipt without inspecting error strings.
#[derive(Debug, Error)]
pub enum SpaceProductSessionError {
    #[error(transparent)]
    StaleSession(#[from] SpaceProductStaleSessionError),
    #[error(transparent)]
    InvalidCommand(#[from] SpaceProductServiceError),
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

    fn handling_with_max_thrust(max_thrust: u64) -> Vec<u8> {
        let fixture = std::str::from_utf8(HANDLING).expect("handling fixture is UTF-8");
        fixture
            .replace("\"maxSpeed\":12", "\"maxSpeed\":10000")
            .replace("\"maxThrust\":18", &format!("\"maxThrust\":{max_thrust}"))
            .into_bytes()
    }

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

    #[test]
    fn later_failed_step_retains_the_last_successful_runtime_publication() {
        // At this admitted thrust and high speed cap the first fixed tick
        // succeeds, then a later one is rejected by the Engine's one-unit
        // motion limit.
        let mut service = SpaceProductService::admit(&handling_with_max_thrust(34_000))
            .expect("admitted high-thrust service");
        service
            .submit_command(SpaceProductCommand::SetFlightIntent {
                throttle: 1.0,
                turn: 0.0,
            })
            .expect("valid command");

        let error = service
            .advance_elapsed(Duration::from_nanos(33_333_334))
            .expect_err("second Engine step exceeds its motion bound");
        assert!(matches!(
            error,
            SpaceProductServiceError::Runtime(FlightRuntimeError::Step(message))
                if message.contains("dynamics-motion-limit-exceeded")
        ));

        assert_eq!(service.latest_update().tick, 1);
        assert_eq!(service.readout(), service.runtime.readout().unwrap());
        assert_eq!(service.latest_update().readout, service.readout());
        assert_eq!(service.accumulator_units, 1_000_000_040);
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

    #[test]
    fn late_and_reconnecting_sessions_receive_complete_create_baselines() {
        let mut service = SpaceProductService::admit(HANDLING).expect("admitted service");
        service
            .advance_elapsed(Duration::from_secs(1))
            .expect("advance several ticks before the first session");

        let first = service.open_session().expect("first session");
        assert!(first.session.generation > 0);
        assert_eq!(first.update.tick, service.latest_update().tick);
        assert!(
            first
                .update
                .frame
                .ops
                .iter()
                .all(|operation| matches!(operation, RenderDiff::Create { .. }))
        );

        let second = service.open_session().expect("reconnecting session");
        assert!(second.session.generation > first.session.generation);
        assert!(
            second
                .update
                .frame
                .ops
                .iter()
                .all(|operation| matches!(operation, RenderDiff::Create { .. }))
        );
    }

    #[test]
    fn disconnect_and_replacement_neutralize_held_input_without_reviving_stale_control() {
        let mut service = SpaceProductService::admit(HANDLING).expect("admitted service");
        let first = service.open_session().expect("first session").session;
        service
            .submit_session_command(
                first,
                SpaceProductCommand::SetFlightIntent {
                    throttle: 1.0,
                    turn: 0.5,
                },
            )
            .expect("controller can command");

        let second = service.open_session().expect("replacement session").session;
        assert_eq!(
            service.current_command(),
            SpaceProductCommand::SetFlightIntent {
                throttle: 0.0,
                turn: 0.0,
            }
        );
        assert!(matches!(
            service.submit_session_command(
                first,
                SpaceProductCommand::SetFlightIntent {
                    throttle: 1.0,
                    turn: 0.0,
                },
            ),
            Err(SpaceProductSessionError::StaleSession(
                SpaceProductStaleSessionError {
                    active_generation: Some(generation),
                    ..
                }
            )) if generation == second.generation
        ));
        assert_eq!(
            service.release_session(first),
            SpaceProductSessionRelease::AlreadyStale
        );
        assert_eq!(
            service.release_session(second),
            SpaceProductSessionRelease::ReleasedController
        );
        assert_eq!(
            service.current_command(),
            SpaceProductCommand::SetFlightIntent {
                throttle: 0.0,
                turn: 0.0,
            }
        );
    }

    #[test]
    fn releasing_the_current_controller_stops_acceleration_without_a_replacement_session() {
        let mut service = SpaceProductService::admit(HANDLING).expect("admitted service");
        let session = service.open_session().expect("controller session").session;
        service
            .submit_session_command(
                session,
                SpaceProductCommand::SetFlightIntent {
                    throttle: 1.0,
                    turn: 0.0,
                },
            )
            .expect("controller starts thrusting");
        service
            .advance_elapsed(Duration::from_millis(100))
            .expect("thrusting ticks");
        assert!(service.readout().linear_velocity.magnitude() > 0.0);
        let throttle_before_disconnect = service.readout().throttle_level;

        assert_eq!(
            service.release_session(session),
            SpaceProductSessionRelease::ReleasedController
        );
        assert_eq!(
            service.current_command(),
            SpaceProductCommand::SetFlightIntent {
                throttle: 0.0,
                turn: 0.0,
            }
        );
        // The flight model intentionally spools drive force down rather than
        // braking instantaneously. These ticks run with no replacement session
        // and prove the held-input source was removed while the spool decays.
        for _ in 0..30 {
            service
                .advance_elapsed(Duration::from_nanos(16_666_667))
                .expect("unowned coasting tick");
        }
        assert!(service.readout().throttle_level < throttle_before_disconnect);
    }

    #[test]
    fn only_the_current_controller_can_submit_validated_commands() {
        let mut service = SpaceProductService::admit(HANDLING).expect("admitted service");
        let first = service.open_session().expect("first session").session;
        let second = service.open_session().expect("replacement session").session;

        assert!(matches!(
            service.submit_session_command(
                first,
                SpaceProductCommand::SetFlightIntent {
                    throttle: 0.0,
                    turn: 0.0,
                },
            ),
            Err(SpaceProductSessionError::StaleSession(_))
        ));
        assert!(matches!(
            service.submit_session_command(
                second,
                SpaceProductCommand::SetFlightIntent {
                    throttle: 2.0,
                    turn: 0.0,
                },
            ),
            Err(SpaceProductSessionError::InvalidCommand(
                SpaceProductServiceError::InvalidCommand { .. }
            ))
        ));
    }

    #[test]
    fn opening_the_first_session_neutralizes_preexisting_internal_input() {
        let mut service = SpaceProductService::admit(HANDLING).expect("admitted service");
        service
            .submit_command(SpaceProductCommand::SetFlightIntent {
                throttle: 1.0,
                turn: -0.5,
            })
            .expect("private test setup command");

        service.open_session().expect("first session");
        assert_eq!(
            service.current_command(),
            SpaceProductCommand::SetFlightIntent {
                throttle: 0.0,
                turn: 0.0,
            }
        );
    }

    #[test]
    fn exhausted_generation_space_never_reuses_a_session_identity() {
        let mut service = SpaceProductService::admit(HANDLING).expect("admitted service");
        service.next_session_generation = u64::MAX;

        assert!(matches!(
            service.open_session(),
            Err(SpaceProductSessionOpenError::GenerationExhausted(
                SpaceProductSessionGenerationExhausted
            ))
        ));
    }
}
