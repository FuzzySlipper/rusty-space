//! Live fixed-step flight runtime: one ship dynamic rigid body driven through
//! the Engine's rigid-body seam by the product-owned flight controller.
//!
//! Integration is the Engine's job; this module owns only the tick-loop glue,
//! the ship body configuration, and the f32/f64 + XZ-plane mapping.

use thiserror::Error;

use rusty_engine::core_ids::EntityId;
use rusty_engine::core_math::Vec3;
use rusty_engine::engine_spatial::{
    RigidBodyAction, RigidBodyService, RigidBodyStepRequest, VoxelCollisionScene,
};
use rusty_engine::entity_state::{
    EntityAuthoringService, EntityDefinition, EntityState, Quat, RigidBodyComponent, RigidBodyShape,
};
use rusty_space_gameplay::{
    FlightCommand, FlightState, NavigationBodyState, ShipHandlingDefinition, ShipWrench, Vec2,
    controller,
};

/// One fixed simulation step (60 Hz).
pub const FIXED_STEP_SECONDS: f64 = 1.0 / 60.0;

const SHIP_ENTITY_ID: u64 = 1;
const SHIP_MASS: f32 = 2.0;
/// Ship hull half-extents: X forward, Y thin, Z wide.
const SHIP_HALF_EXTENTS: [f32; 3] = [1.0, 0.25, 0.6];

/// The renderer-neutral state the runtime projects each tick for presentation.
#[derive(Debug, Clone, Copy, PartialEq, serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FlightReadout {
    pub position: Vec2,
    pub heading: f64,
    pub linear_velocity: Vec2,
    pub angular_velocity: f64,
    pub throttle_level: f64,
}

#[derive(Debug, Error)]
pub enum FlightRuntimeError {
    #[error("ship spawn rejected: {0}")]
    Spawn(String),
    #[error("ship state read rejected: {0}")]
    Read(String),
    #[error("rigid-body step rejected: {0}")]
    Step(String),
}

pub struct FlightRuntime {
    ship: EntityId,
    state: EntityState,
    scene: VoxelCollisionScene,
    service: RigidBodyService,
    handling: ShipHandlingDefinition,
    /// Yaw moment of inertia derived from the ship cuboid and mass.
    moment_of_inertia: f64,
    throttle_level: f64,
}

impl FlightRuntime {
    /// Spawn one ship: a planar (XZ) dynamic cuboid with gravity and damping
    /// off, ready for per-tick control.
    pub fn spawn(handling: ShipHandlingDefinition) -> Result<Self, FlightRuntimeError> {
        let ship = EntityId::new(SHIP_ENTITY_ID);
        let half_extents = Vec3::new(
            SHIP_HALF_EXTENTS[0],
            SHIP_HALF_EXTENTS[1],
            SHIP_HALF_EXTENTS[2],
        );
        let mut body =
            RigidBodyComponent::dynamic(RigidBodyShape::Cuboid { half_extents }, SHIP_MASS);
        body.locked_translation_axes = [false, true, false];
        body.locked_rotation_axes = [true, false, true];
        body.gravity_scale = 0.0;

        let mut state = EntityState::from_definitions([
            EntityDefinition::new(ship, "ship").with_transform(Vec3::ZERO)
        ])
        .map_err(|error| FlightRuntimeError::Spawn(error.to_string()))?;
        let revision = state
            .component_revision::<RigidBodyComponent>(ship)
            .map_err(|error| FlightRuntimeError::Spawn(error.to_string()))?;
        EntityAuthoringService
            .attach_component(&mut state, revision, ship, body)
            .map_err(|error| FlightRuntimeError::Spawn(error.to_string()))?;

        let scene = VoxelCollisionScene::from_solid_voxels(1.0, 8, std::iter::empty::<[i64; 3]>())
            .map_err(|error| FlightRuntimeError::Spawn(error.to_string()))?;

        // Yaw moment of inertia for a cuboid: m/3 * (hx² + hz²). This matches
        // the Engine's derive-from-shape-and-mass policy (Rapier analytic box
        // inertia); engine task #7219 will expose authored mass properties so
        // this formula stops being duplicated downstream.
        let moment_of_inertia = (SHIP_MASS as f64)
            * ((SHIP_HALF_EXTENTS[0] as f64).powi(2) + (SHIP_HALF_EXTENTS[2] as f64).powi(2))
            / 3.0;

        Ok(Self {
            ship,
            state,
            scene,
            service: RigidBodyService::default(),
            handling,
            moment_of_inertia,
            throttle_level: 0.0,
        })
    }

    /// Run one fixed 60 Hz step with the given command, returning the readout.
    pub fn tick(&mut self, command: FlightCommand) -> Result<FlightReadout, FlightRuntimeError> {
        let body = self.sample()?;
        let flight_state = FlightState {
            body,
            throttle_level: self.throttle_level,
        };
        let output = controller(
            &flight_state,
            &command,
            &self.handling,
            self.moment_of_inertia,
            FIXED_STEP_SECONDS,
        );
        self.throttle_level = output.throttle_level;

        let (force, torque) = to_engine_wrench(&output.wrench);
        self.service
            .step(
                &mut self.state,
                &self.scene,
                RigidBodyStepRequest {
                    step_seconds: FIXED_STEP_SECONDS as f32,
                    steps: 1,
                    gravity: Vec3::ZERO,
                    actions: vec![RigidBodyAction {
                        entity: self.ship,
                        force,
                        torque,
                        impulse: Vec3::ZERO,
                        torque_impulse: Vec3::ZERO,
                        wake: true,
                    }],
                },
            )
            .map_err(|error| FlightRuntimeError::Step(error.to_string()))?;

        self.readout()
    }

    /// The current ship state, for projection.
    pub fn readout(&self) -> Result<FlightReadout, FlightRuntimeError> {
        let body = self.sample()?;
        Ok(FlightReadout {
            position: body.position,
            heading: body.heading,
            linear_velocity: body.linear_velocity,
            angular_velocity: body.angular_velocity,
            throttle_level: self.throttle_level,
        })
    }

    fn sample(&self) -> Result<NavigationBodyState, FlightRuntimeError> {
        let body = self
            .state
            .rigid_body(self.ship)
            .ok_or_else(|| FlightRuntimeError::Read("missing ship rigid body".to_owned()))?;
        let transform = self
            .state
            .transform(self.ship)
            .ok_or_else(|| FlightRuntimeError::Read("missing ship transform".to_owned()))?;
        Ok(NavigationBodyState {
            position: Vec2::new(
                transform.translation.x as f64,
                transform.translation.z as f64,
            ),
            heading: yaw(transform.rotation),
            linear_velocity: Vec2::new(
                body.linear_velocity.x as f64,
                body.linear_velocity.z as f64,
            ),
            angular_velocity: body.angular_velocity.y as f64,
        })
    }
}

/// Extract yaw around +Y from a (yaw-only) quaternion.
fn yaw(rotation: Quat) -> f64 {
    2.0 * (rotation.y as f64).atan2(rotation.w as f64)
}

/// Map the planar ship wrench into the Engine's 3D force/torque vectors.
fn to_engine_wrench(wrench: &ShipWrench) -> (Vec3, Vec3) {
    (
        Vec3::new(wrench.force.x as f32, 0.0, wrench.force.z as f32),
        Vec3::new(0.0, wrench.torque_y as f32, 0.0),
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    use rusty_space_gameplay::compile_ship_handling;

    fn handling() -> ShipHandlingDefinition {
        let bytes = std::fs::read(
            std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("../../content/gameplay/rusty-space-core.package.json"),
        )
        .expect("committed ship handling package exists");
        compile_ship_handling(&bytes).expect("committed artifact compiles")
    }

    #[test]
    fn spawns_a_stationary_ship_at_the_origin() {
        let runtime = FlightRuntime::spawn(handling()).expect("spawn");
        let readout = runtime.readout().unwrap();
        assert_eq!(readout.position, Vec2::ZERO);
        assert_eq!(readout.heading, 0.0);
        assert_eq!(readout.linear_velocity, Vec2::ZERO);
        assert_eq!(readout.angular_velocity, 0.0);
    }

    #[test]
    fn thrust_accelerates_forward_and_caps_at_max_speed() {
        let handling = handling();
        let mut runtime = FlightRuntime::spawn(handling.clone()).expect("spawn");
        for _ in 0..240 {
            runtime
                .tick(FlightCommand {
                    throttle: 1.0,
                    turn: 0.0,
                })
                .unwrap();
        }
        let readout = runtime.readout().unwrap();
        let speed = readout.linear_velocity.magnitude();
        // Without the cap, 4 s of thrust would reach ~72 u/s.
        assert!(speed > handling.max_speed - 0.5);
        assert!(speed < handling.max_speed + 0.5);
        // Pure thrust never yaws nor drifts sideways.
        assert!(readout.heading.abs() < 1e-3);
        assert!(readout.linear_velocity.z.abs() < 1e-3);
    }

    #[test]
    fn releasing_thrust_preserves_velocity() {
        let mut runtime = FlightRuntime::spawn(handling()).expect("spawn");
        // Thrust long enough to reach the max-speed plateau (mass 2.0, so
        // acceleration is max_thrust / mass = 9 u/s²; ~80 ticks to 12 u/s).
        for _ in 0..120 {
            runtime
                .tick(FlightCommand {
                    throttle: 1.0,
                    turn: 0.0,
                })
                .unwrap();
        }
        let speed_before = runtime.readout().unwrap().linear_velocity.magnitude();
        for _ in 0..60 {
            runtime
                .tick(FlightCommand {
                    throttle: 0.0,
                    turn: 0.0,
                })
                .unwrap();
        }
        let speed_after = runtime.readout().unwrap().linear_velocity.magnitude();
        assert!((speed_after - speed_before).abs() < 1e-3);
    }

    #[test]
    fn turning_yaws_without_linear_motion() {
        let mut runtime = FlightRuntime::spawn(handling()).expect("spawn");
        for _ in 0..30 {
            runtime
                .tick(FlightCommand {
                    throttle: 0.0,
                    turn: 1.0,
                })
                .unwrap();
        }
        let readout = runtime.readout().unwrap();
        assert!(
            readout.heading > 0.5,
            "ship should have yawed: {:?}",
            readout
        );
        assert!(readout.angular_velocity > 0.0);
        assert!(readout.position.x.abs() < 1e-3);
        assert!(readout.position.z.abs() < 1e-3);
        assert!(readout.linear_velocity.x.abs() < 1e-3);
        assert!(readout.linear_velocity.z.abs() < 1e-3);
    }

    #[test]
    fn steering_reaches_max_turn_rate() {
        let handling = handling();
        let mut runtime = FlightRuntime::spawn(handling.clone()).expect("spawn");
        for _ in 0..120 {
            runtime
                .tick(FlightCommand {
                    throttle: 0.0,
                    turn: 1.0,
                })
                .unwrap();
        }
        let angular_velocity = runtime.readout().unwrap().angular_velocity;
        assert!(
            (angular_velocity - handling.max_turn_rate).abs() < 0.2,
            "steady-state turn rate {angular_velocity} should approach {}",
            handling.max_turn_rate
        );
    }

    #[test]
    fn steering_angular_response_matches_the_derived_inertia() {
        let handling = handling();
        let mut runtime = FlightRuntime::spawn(handling.clone()).expect("spawn");
        // One full-turn tick from rest. If the derived yaw inertia matches the
        // Engine's solver inertia, the angular delta is
        // max_turn_rate / steering_response_time * dt.
        runtime
            .tick(FlightCommand {
                throttle: 0.0,
                turn: 1.0,
            })
            .unwrap();
        let angular_velocity = runtime.readout().unwrap().angular_velocity;
        let expected =
            handling.max_turn_rate / handling.steering_response_time * FIXED_STEP_SECONDS;
        assert!(
            (angular_velocity - expected).abs() < 0.05,
            "first-tick angular velocity {angular_velocity} should be ~{expected}"
        );
    }
}
