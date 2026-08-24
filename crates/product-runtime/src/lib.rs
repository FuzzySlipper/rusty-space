//! Live product runtime and renderer-neutral projection.

#![forbid(unsafe_code)]

mod flight_runtime;
pub use flight_runtime::{FIXED_STEP_SECONDS, FlightReadout, FlightRuntime, FlightRuntimeError};

mod projection;
pub use projection::{SHIP_NODE_HANDLE, ship_frame_diff};
