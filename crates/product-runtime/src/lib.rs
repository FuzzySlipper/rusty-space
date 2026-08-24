//! Live product runtime and renderer-neutral projection.

#![forbid(unsafe_code)]

mod flight_runtime;
pub use flight_runtime::{FIXED_STEP_SECONDS, FlightReadout};
use flight_runtime::{FlightRuntime, FlightRuntimeError};

mod projection;
use projection::ship_frame_diff;

mod space_product_service;
pub use space_product_service::{
    MAX_ACCUMULATED_STEPS, SpaceProductAdvanceReceipt, SpaceProductCommand,
    SpaceProductCommandReceipt, SpaceProductService, SpaceProductServiceError, SpaceProductUpdate,
};
