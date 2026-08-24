//! Product-owned live flight vocabulary and ship-handling admission.
//!
//! This deliberately small vocabulary is a product contract, not an Engine
//! grammar. TypeScript may materialize the ship-handling package at build time,
//! while Rust retains the only admission and semantic interpretation path.

#![forbid(unsafe_code)]

mod ship_handling;
pub use ship_handling::{
    AuthoredShipHandling, SHIP_HANDLING_DOMAIN, SHIP_HANDLING_PACKAGE,
    SHIP_HANDLING_PACKAGE_VERSION, SHIP_HANDLING_SCHEMA_VERSION, ShipHandlingDefinition,
    ShipHandlingError, compile_ship_handling,
};

mod flight;
pub use flight::{
    ControllerOutput, FlightCommand, FlightState, NavigationBodyState, ShipWrench, Vec2, controller,
};
