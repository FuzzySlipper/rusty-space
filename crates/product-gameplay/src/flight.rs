//! The ship flight controller: a pure, solver-neutral capability.
//!
//! It turns a command and live state into a force/torque wrench. Integration is
//! the Engine's job — this module owns only authored force generation and never
//! integrates or mutates body state itself. The single `dt`-aware piece is the
//! drive spool, which advances an explicit actuator state carried in
//! [`FlightState`] (the product runtime holds it between ticks).

use crate::ship_handling::ShipHandlingDefinition;

/// A 2D vector in the XZ navigation plane (Y-up, yaw around +Y).
#[derive(Debug, Clone, Copy, PartialEq, serde::Serialize)]
pub struct Vec2 {
    pub x: f64,
    pub z: f64,
}

impl Vec2 {
    pub const ZERO: Self = Self { x: 0.0, z: 0.0 };

    pub const fn new(x: f64, z: f64) -> Self {
        Self { x, z }
    }

    pub fn dot(self, other: Self) -> f64 {
        self.x * other.x + self.z * other.z
    }

    pub fn magnitude_sq(self) -> f64 {
        self.dot(self)
    }

    pub fn magnitude(self) -> f64 {
        self.magnitude_sq().sqrt()
    }

    pub fn scale(self, factor: f64) -> Self {
        Self::new(self.x * factor, self.z * factor)
    }
}

impl std::ops::Add for Vec2 {
    type Output = Self;

    fn add(self, other: Self) -> Self {
        Self::new(self.x + other.x, self.z + other.z)
    }
}

impl std::ops::Sub for Vec2 {
    type Output = Self;

    fn sub(self, other: Self) -> Self {
        Self::new(self.x - other.x, self.z - other.z)
    }
}

/// Physical navigation state the Engine integrates.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct NavigationBodyState {
    pub position: Vec2,
    /// Yaw around +Y, radians. `heading == 0` points along +X.
    pub heading: f64,
    pub linear_velocity: Vec2,
    pub angular_velocity: f64,
}

/// Live flight state: the body the Engine integrates plus the actuator state
/// the product runtime holds (the spooled drive level).
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct FlightState {
    pub body: NavigationBodyState,
    /// Current spooled main-drive output magnitude (0..=max_thrust).
    pub throttle_level: f64,
}

/// Classic Asteroids command: turn the ship and thrust.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct FlightCommand {
    /// Main-drive intent, 0..=1.
    pub throttle: f64,
    /// Yaw intent, -1..=1 (positive turns toward increasing heading).
    pub turn: f64,
}

/// Force and torque to apply to the body (world-space XZ force, +Y torque).
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct ShipWrench {
    pub force: Vec2,
    pub torque_y: f64,
}

/// The controller's result: the wrench to apply and the next actuator state.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct ControllerOutput {
    pub wrench: ShipWrench,
    pub throttle_level: f64,
}

/// Ship-forward direction for a heading (XZ plane).
fn ship_forward(heading: f64) -> Vec2 {
    Vec2::new(heading.cos(), heading.sin())
}

/// Compute the authored wrench from a command and live state.
///
/// `moment_of_inertia` is the body's yaw moment of inertia (from the Engine
/// rigid body; positive and finite). `dt` is the fixed timestep, used only to
/// advance the drive spool.
pub fn controller(
    state: &FlightState,
    command: &FlightCommand,
    handling: &ShipHandlingDefinition,
    moment_of_inertia: f64,
    dt: f64,
) -> ControllerOutput {
    let throttle = command.throttle.clamp(0.0, 1.0);
    let turn = command.turn.clamp(-1.0, 1.0);

    // Drive spool: first-order lag toward the commanded thrust.
    let desired_thrust = throttle * handling.max_thrust;
    let lag = lag_factor(dt, handling.throttle_response_time);
    let throttle_level = state.throttle_level + (desired_thrust - state.throttle_level) * lag;

    // Main thrust along ship-forward, then the hard max-speed gate. At or above
    // max speed, thrust may still steer sideways but may not grow the speed
    // component along the current velocity.
    let mut force = ship_forward(state.body.heading).scale(throttle_level);
    let speed = state.body.linear_velocity.magnitude();
    if speed >= handling.max_speed && speed > 0.0 {
        let velocity_direction = state.body.linear_velocity.scale(1.0 / speed);
        let along_velocity = force.dot(velocity_direction);
        if along_velocity > 0.0 {
            force = force - velocity_direction.scale(along_velocity);
        }
    }

    // Steering as angular-rate intent with a finite torque authority.
    let desired_angular_velocity = turn * handling.max_turn_rate;
    let error = desired_angular_velocity - state.body.angular_velocity;
    let torque_y = if moment_of_inertia.is_finite() && moment_of_inertia > 0.0 {
        let authority =
            moment_of_inertia * handling.max_turn_rate / handling.steering_response_time;
        let requested = moment_of_inertia * error / handling.steering_response_time;
        requested.clamp(-authority, authority)
    } else {
        0.0
    };

    ControllerOutput {
        wrench: ShipWrench { force, torque_y },
        throttle_level,
    }
}

fn lag_factor(dt: f64, response_time: f64) -> f64 {
    // `dt` and `response_time` are positive and finite by construction (fixed
    // step and validated handling); clamp to 1.0 for first-order stability.
    (dt / response_time).min(1.0)
}

#[cfg(test)]
mod tests {
    use super::*;

    const DT: f64 = 1.0 / 60.0;

    fn stock() -> ShipHandlingDefinition {
        ShipHandlingDefinition {
            max_speed: 12.0,
            max_thrust: 18.0,
            max_turn_rate: 3.0,
            throttle_response_time: 0.08,
            steering_response_time: 0.12,
        }
    }

    fn at_rest() -> FlightState {
        FlightState {
            body: NavigationBodyState {
                position: Vec2::ZERO,
                heading: 0.0,
                linear_velocity: Vec2::ZERO,
                angular_velocity: 0.0,
            },
            throttle_level: 0.0,
        }
    }

    /// Test-only semi-implicit Euler oracle (unit mass, unit inertia). It exists
    /// solely to verify the controller's force model; production integration is
    /// the Engine's job.
    fn euler_step(body: &mut NavigationBodyState, wrench: &ShipWrench, dt: f64) {
        let linear_acceleration = wrench.force;
        let angular_acceleration = wrench.torque_y;
        body.linear_velocity = body.linear_velocity + linear_acceleration.scale(dt);
        body.angular_velocity += angular_acceleration * dt;
        body.position = body.position + body.linear_velocity.scale(dt);
        body.heading += body.angular_velocity * dt;
    }

    fn tick(state: &mut FlightState, command: &FlightCommand, handling: &ShipHandlingDefinition) {
        let output = controller(state, command, handling, 1.0, DT);
        euler_step(&mut state.body, &output.wrench, DT);
        state.throttle_level = output.throttle_level;
    }

    #[test]
    fn zero_input_yields_zero_wrench_and_stays_at_rest() {
        let state = at_rest();
        let output = controller(
            &state,
            &FlightCommand {
                throttle: 0.0,
                turn: 0.0,
            },
            &stock(),
            1.0,
            DT,
        );
        assert_eq!(output.wrench.force, Vec2::ZERO);
        assert_eq!(output.wrench.torque_y, 0.0);
        assert_eq!(output.throttle_level, 0.0);
    }

    #[test]
    fn turn_produces_torque_not_linear_force() {
        let state = at_rest();
        let output = controller(
            &state,
            &FlightCommand {
                throttle: 0.0,
                turn: 1.0,
            },
            &stock(),
            1.0,
            DT,
        );
        assert_eq!(output.wrench.force, Vec2::ZERO);
        assert!(output.wrench.torque_y > 0.0);
    }

    #[test]
    fn releasing_thrust_preserves_velocity() {
        let mut state = at_rest();
        let thrust = FlightCommand {
            throttle: 1.0,
            turn: 0.0,
        };
        for _ in 0..30 {
            tick(&mut state, &thrust, &stock());
        }
        // The drive has fully spooled down; thrust is released.
        state.throttle_level = 0.0;
        let velocity = state.body.linear_velocity;
        let heading = state.body.heading;
        assert!(velocity.x > 0.0);

        // Coasting with no input: no drag, so the ship never brakes.
        let coast = FlightCommand {
            throttle: 0.0,
            turn: 0.0,
        };
        for _ in 0..60 {
            tick(&mut state, &coast, &stock());
        }
        assert_eq!(state.body.linear_velocity, velocity);
        assert_eq!(state.body.heading, heading);
    }

    #[test]
    fn heading_and_velocity_decouple_under_inertia() {
        let mut state = at_rest();
        let thrust = FlightCommand {
            throttle: 1.0,
            turn: 0.0,
        };
        for _ in 0..30 {
            tick(&mut state, &thrust, &stock());
        }
        state.throttle_level = 0.0;
        let velocity_before_turn = state.body.linear_velocity;
        assert!(velocity_before_turn.x > 0.0);
        let heading_before = state.body.heading;

        let turn = FlightCommand {
            throttle: 0.0,
            turn: 1.0,
        };
        for _ in 0..30 {
            tick(&mut state, &turn, &stock());
        }
        assert!(state.body.heading > heading_before);
        assert_eq!(state.body.linear_velocity, velocity_before_turn);
    }

    #[test]
    fn max_speed_cap_stops_forward_acceleration() {
        let mut state = at_rest();
        let thrust = FlightCommand {
            throttle: 1.0,
            turn: 0.0,
        };
        for _ in 0..240 {
            tick(&mut state, &thrust, &stock());
        }
        let speed = state.body.linear_velocity.magnitude();
        assert!(speed >= stock().max_speed);
        assert!(speed <= stock().max_speed + stock().max_thrust * DT + 1e-9);
    }

    #[test]
    fn steering_authority_is_clamped() {
        let handling = stock();
        let state = FlightState {
            body: NavigationBodyState {
                position: Vec2::ZERO,
                heading: 0.0,
                linear_velocity: Vec2::ZERO,
                angular_velocity: 100.0,
            },
            throttle_level: 0.0,
        };
        let output = controller(
            &state,
            &FlightCommand {
                throttle: 0.0,
                turn: -1.0,
            },
            &handling,
            1.0,
            DT,
        );
        let authority = handling.max_turn_rate / handling.steering_response_time;
        assert_eq!(output.wrench.torque_y, -authority);
    }
}
