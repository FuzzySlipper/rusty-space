using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Field;

/// <summary>
/// Resolves one drift band's push as a flow-following force, in the same
/// sailing spirit as the stellar field response: a parked ship is carried
/// toward the local flow speed, and the push fades as the ship matches it,
/// so currents bend routes without becoming universal drag. The product owns
/// this meaning; the Engine owns the integration through the DynamicsAction
/// force the caller publishes.
/// </summary>
internal sealed class DriftCurrent
{
    private const double GaussianSquaredExponent = 2.0;
    private const double NegligibleWeight = 1e-6;
    private const double NoYawTorque = 0.0;

    private readonly DriftCurrentTuning tuning;
    private readonly PlanarVector direction;

    internal DriftCurrent(DriftCurrentTuning tuning)
    {
        this.tuning = tuning.Validate();
        direction = tuning.Direction.Scale(1.0 / tuning.Direction.Magnitude);
    }

    internal PlanarVector FlowDirection => direction;

    internal FlightWrench Resolve(PlanarVector position, PlanarVector velocity, double mass)
    {
        PlanarVector offset = position - tuning.Center;
        double along = offset.Dot(direction);
        PlanarVector lateral = offset - direction.Scale(along);
        double lateralDistance = lateral.Magnitude;
        double lateralWeight = Math.Exp(-Math.Pow(
            lateralDistance / tuning.Width,
            GaussianSquaredExponent));
        double beyondEnd = Math.Max(0.0, Math.Abs(along) - (tuning.Length / 2.0));
        double endGate = Math.Exp(-Math.Pow(
            beyondEnd / tuning.Width,
            GaussianSquaredExponent));
        double weight = lateralWeight * endGate;
        if (weight < NegligibleWeight)
        {
            return FlightWrench.Zero;
        }

        double swell = 1.0 + (tuning.WaveAmplitude * Math.Sin(along * tuning.WaveFrequency));
        PlanarVector flow = direction.Scale(tuning.FlowSpeed * swell);
        PlanarVector force = (flow - velocity).Scale(tuning.ResponseGain * weight * mass);
        return new FlightWrench(ClampMagnitude(force, tuning.MaximumForce), NoYawTorque);
    }

    private static PlanarVector ClampMagnitude(PlanarVector force, double maximum)
    {
        double magnitude = force.Magnitude;
        return magnitude > maximum
            ? force.Scale(maximum / magnitude)
            : force;
    }
}
