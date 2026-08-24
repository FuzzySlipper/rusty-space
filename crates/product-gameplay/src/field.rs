//! A deterministic fictional navigation field and its authored ship coupling.
//!
//! The field is deliberately a small analytic source: one broad stellar
//! current plus a single spatially bounded planetary wake.  It is sampled in
//! the product gameplay layer so the runtime can pass a renderer-neutral
//! [`FieldSample`] through the Engine step without giving the browser any
//! authority over the force model.

use crate::ship_handling::ShipHandlingDefinition;
use crate::{NavigationBodyState, ShipWrench, Vec2};

/// The gradient of the vector flow field.  The first row contains the
/// derivative of `flow_velocity.x` with respect to `(x, z)`; the second row
/// contains the corresponding derivative for `flow_velocity.z`.
pub type FieldGradient = [[f64; 2]; 2];

/// The complete local field observation used by gameplay and presentation.
///
/// Every value is derived from the ship's world position and the deterministic
/// source below.  It is intentionally a value object: a browser may display
/// it, but cannot author or mutate it.
#[derive(Debug, Clone, Copy, PartialEq, serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FieldSample {
    pub flow_velocity: Vec2,
    pub intensity: f64,
    pub gradient: FieldGradient,
    pub turbulence: Vec2,
}

/// Position of the one fictional planet that generates the wake.
pub const FIELD_PLANET_POSITION: Vec2 = Vec2::new(14.0, 0.0);

/// Length of the visible wake ribbon behind [`FIELD_PLANET_POSITION`].
pub const FIELD_WAKE_LENGTH: f64 = 18.0;

const STELLAR_FLOW: Vec2 = Vec2::new(0.0, 1.75);
const STELLAR_INTENSITY: f64 = 0.24;
const WAKE_CENTER_BEHIND_PLANET: f64 = 5.0;
const WAKE_LONGITUDINAL_SCALE: f64 = 7.0;
const WAKE_LATERAL_SCALE: f64 = 3.5;
const WAKE_DOWNSTREAM_EDGE_SCALE: f64 = 1.5;
const WAKE_FLOW_X: f64 = 1.2;
const WAKE_FLOW_Z: f64 = 4.0;

/// Named deterministic field source.  It has no mutable state, clock, or
/// random generator, so the same position always produces the same sample.
#[derive(Debug, Clone, Copy, Default)]
pub struct StellarField;

impl StellarField {
    pub const fn new() -> Self {
        Self
    }

    /// Sample the broad stellar current and the one planet wake at `position`.
    pub fn sample(self, position: Vec2) -> FieldSample {
        let wake = wake_weight(position);
        let wake_dx = wake_weight_dx(position);
        let wake_dz = wake_weight_dz(position, wake);

        FieldSample {
            flow_velocity: STELLAR_FLOW + Vec2::new(WAKE_FLOW_X * wake, WAKE_FLOW_Z * wake),
            intensity: (STELLAR_INTENSITY + 0.72 * wake).clamp(0.0, 1.0),
            gradient: [
                [WAKE_FLOW_X * wake_dx, WAKE_FLOW_X * wake_dz],
                [WAKE_FLOW_Z * wake_dx, WAKE_FLOW_Z * wake_dz],
            ],
            turbulence: Vec2::new(
                0.22 * wake * (position.x * 0.16 + position.z * 0.22).sin(),
                0.30 * wake * (position.x * 0.13 - position.z * 0.11).cos(),
            ),
        }
    }
}

/// Convenience function for callers that do not need to retain a source
/// value.  This is the sole field source used by the product runtime.
pub fn sample_field(position: Vec2) -> FieldSample {
    StellarField::new().sample(position)
}

/// Resolve a local field sample into a force contribution for one ship.
///
/// The important distinction from universal damping is that every term is
/// based on `relative_velocity = ship_velocity - field_flow_velocity`, and
/// the contribution returns an exact zero wrench for an authored coupling of
/// zero.  Forward and lateral responses are intentionally anisotropic and
/// resolved through the ship's local axes, so rotating the hull changes how
/// it catches the same current.
pub fn field_wrench(
    body: &NavigationBodyState,
    sample: &FieldSample,
    handling: &ShipHandlingDefinition,
) -> ShipWrench {
    let coupling = handling.field_coupling();
    if coupling == 0.0 {
        return ShipWrench {
            force: Vec2::ZERO,
            torque_y: 0.0,
        };
    }

    let forward = Vec2::new(body.heading.cos(), body.heading.sin());
    let right = Vec2::new(-forward.z, forward.x);
    let relative_velocity = body.linear_velocity - sample.flow_velocity;
    let forward_slip = relative_velocity.dot(forward);
    let lateral_slip = relative_velocity.dot(right);

    // Gradient increases the response gently in the wake without becoming a
    // discontinuous kick at the edge of the analytic source.
    let gradient_magnitude = sample
        .gradient
        .into_iter()
        .flatten()
        .map(f64::abs)
        .sum::<f64>();
    let intensity = sample.intensity.clamp(0.0, 1.0);
    let response_scale = coupling * intensity * (1.0 + 0.12 * gradient_magnitude.min(4.0));

    // This product's rigid body weighs two local units.  Keeping that scale in
    // the named field contribution makes the authored coupling a direct
    // response control while leaving Engine integration authoritative.
    const SHIP_MASS: f64 = 2.0;
    const FORWARD_RESPONSE: f64 = 0.85;
    const LATERAL_RESPONSE: f64 = 1.8;
    const TURBULENCE_RESPONSE: f64 = 0.8;
    let turbulence_forward = sample.turbulence.dot(forward);
    let turbulence_lateral = sample.turbulence.dot(right);
    let local_force = Vec2::new(
        (-forward_slip * FORWARD_RESPONSE + turbulence_forward * TURBULENCE_RESPONSE)
            * response_scale
            * SHIP_MASS,
        (-lateral_slip * LATERAL_RESPONSE + turbulence_lateral * TURBULENCE_RESPONSE)
            * response_scale
            * SHIP_MASS,
    );

    ShipWrench {
        force: forward.scale(local_force.x) + right.scale(local_force.z),
        torque_y: 0.0,
    }
}

fn wake_weight(position: Vec2) -> f64 {
    // The wake only trails behind the planet.  The smooth Gaussian along and
    // across its centerline makes both field samples and their gradients
    // continuous as the ship crosses the wake boundary.
    let behind = FIELD_PLANET_POSITION.x - position.x;
    if behind <= 0.0 {
        return 0.0;
    }
    let longitudinal = (behind - WAKE_CENTER_BEHIND_PLANET) / WAKE_LONGITUDINAL_SCALE;
    let lateral = position.z / WAKE_LATERAL_SCALE;
    let downstream_gate = 1.0 - (-(behind / WAKE_DOWNSTREAM_EDGE_SCALE).powi(2)).exp();
    (-longitudinal * longitudinal - lateral * lateral).exp() * downstream_gate
}

fn wake_weight_dx(position: Vec2) -> f64 {
    let behind = FIELD_PLANET_POSITION.x - position.x;
    if behind <= 0.0 {
        return 0.0;
    }
    let longitudinal = (behind - WAKE_CENTER_BEHIND_PLANET) / WAKE_LONGITUDINAL_SCALE;
    let lateral = position.z / WAKE_LATERAL_SCALE;
    let longitudinal_envelope = (-longitudinal * longitudinal).exp();
    let lateral_envelope = (-lateral * lateral).exp();
    let edge_envelope = (-(behind / WAKE_DOWNSTREAM_EDGE_SCALE).powi(2)).exp();
    let downstream_gate = 1.0 - edge_envelope;
    let longitudinal_dx = if longitudinal_envelope == 0.0 {
        0.0
    } else {
        2.0 * (behind - WAKE_CENTER_BEHIND_PLANET) / WAKE_LONGITUDINAL_SCALE.powi(2)
            * longitudinal_envelope
    };
    let downstream_gate_dx = if edge_envelope == 0.0 {
        0.0
    } else {
        -2.0 * behind / WAKE_DOWNSTREAM_EDGE_SCALE.powi(2) * edge_envelope
    };
    (longitudinal_dx * downstream_gate + longitudinal_envelope * downstream_gate_dx)
        * lateral_envelope
}

fn wake_weight_dz(position: Vec2, wake: f64) -> f64 {
    if wake == 0.0 {
        return 0.0;
    }
    -2.0 * position.z / WAKE_LATERAL_SCALE.powi(2) * wake
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ShipHandlingDefinition;

    fn body(heading: f64, velocity: Vec2) -> NavigationBodyState {
        NavigationBodyState {
            position: Vec2::ZERO,
            heading,
            linear_velocity: velocity,
            angular_velocity: 0.0,
        }
    }

    fn coupled() -> ShipHandlingDefinition {
        ShipHandlingDefinition::new_with_field_coupling(12.0, 18.0, 3.0, 0.08, 0.12, 0.55)
            .expect("valid coupled handling")
    }

    #[test]
    fn field_is_finite_and_has_a_broad_current_and_one_stronger_wake() {
        let broad = sample_field(Vec2::new(-30.0, 0.0));
        let wake = sample_field(Vec2::new(FIELD_PLANET_POSITION.x - 5.0, 0.0));
        assert!(broad.flow_velocity.magnitude() > 0.0);
        assert!(wake.intensity > broad.intensity);
        assert!(wake.gradient.into_iter().flatten().all(f64::is_finite));
        assert!(wake.turbulence.x.is_finite() && wake.turbulence.z.is_finite());
    }

    #[test]
    fn wake_is_continuous_at_the_planet_downstream_boundary_and_repeatable() {
        let source = StellarField::new();
        let before = source.sample(Vec2::new(FIELD_PLANET_POSITION.x - 1.0e-6, 0.0));
        let after = source.sample(Vec2::new(FIELD_PLANET_POSITION.x + 1.0e-6, 0.0));
        assert!((before.flow_velocity.x - after.flow_velocity.x).abs() < 1.0e-9);
        assert!((before.flow_velocity.z - after.flow_velocity.z).abs() < 1.0e-9);
        assert!((before.intensity - after.intensity).abs() < 1.0e-9);
        let sample = Vec2::new(4.25, -1.75);
        assert_eq!(source.sample(sample), source.sample(sample));
        assert_eq!(sample_field(sample), sample_field(sample));
        let extreme = sample_field(Vec2::new(-f64::MAX, f64::MAX));
        assert!(extreme.flow_velocity.x.is_finite());
        assert!(extreme.flow_velocity.z.is_finite());
        assert!(extreme.gradient.into_iter().flatten().all(f64::is_finite));
        assert!(extreme.turbulence.x.is_finite() && extreme.turbulence.z.is_finite());
    }

    #[test]
    fn zero_coupling_is_exactly_inertial_even_inside_the_wake() {
        let uncoupled = ShipHandlingDefinition::new(12.0, 18.0, 3.0, 0.08, 0.12)
            .expect("valid uncoupled handling");
        let sample = sample_field(Vec2::new(FIELD_PLANET_POSITION.x - 5.0, 0.0));
        let wrench = field_wrench(&body(0.0, Vec2::new(2.0, -1.0)), &sample, &uncoupled);
        assert_eq!(wrench.force, Vec2::ZERO);
        assert_eq!(wrench.torque_y, 0.0);
    }

    #[test]
    fn relative_flow_and_heading_change_the_caught_force() {
        let sample = FieldSample {
            flow_velocity: Vec2::new(0.0, 4.0),
            intensity: 1.0,
            gradient: [[0.0; 2]; 2],
            turbulence: Vec2::ZERO,
        };
        let along_x = field_wrench(&body(0.0, Vec2::ZERO), &sample, &coupled());
        let along_z = field_wrench(
            &body(std::f64::consts::FRAC_PI_2, Vec2::ZERO),
            &sample,
            &coupled(),
        );
        assert!(
            along_x.force.z > 0.0,
            "current should carry a +Z-resting ship"
        );
        // A 90° rotation catches the same +Z current on the anisotropic
        // forward axis instead of the stronger lateral axis.  The force must
        // therefore change while still carrying in the current direction.
        assert!(along_z.force.z < along_x.force.z);
        assert_ne!(along_x.force, along_z.force);
    }
}
