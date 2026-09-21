using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Field;

/// <summary>
/// Resolves one drift band's push as a flow-following force, in the same
/// sailing spirit as the stellar field response: a parked ship is carried
/// toward the local flow speed, and the push fades as the ship matches it,
/// so currents bend routes without becoming universal drag. The band's
/// authority is the ship's coupling: a hull that has wound coupling down
/// keeps its line straight through the band. The product owns this meaning;
/// the Engine owns the integration through the DynamicsAction force the
/// caller publishes.
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
        this.tuning = tuning;
        direction = tuning.Direction.Scale(1.0 / tuning.Direction.Magnitude);
    }

    internal PlanarVector FlowDirection => direction;

    /// <summary>
    /// The band's own shape, reported by the owner of the band so a consumer that
    /// has to draw it does not carry a second copy of the tuning and quietly
    /// disagree with the push.
    /// </summary>
    internal DriftCurrentTuning Shape => tuning;

    /// <summary>
    /// The flow this band puts where the hull is, scaled by the same authority
    /// weight its push resolves with: zero where the band has nothing to say, and
    /// the band's own swollen flow at full strength inside it. A view that shows
    /// the current asks for this rather than re-deriving the falloff.
    /// </summary>
    internal PlanarVector FlowAt(PlanarVector position)
    {
        double along = (position - tuning.Center).Dot(direction);
        double weight = AuthorityWeight(position, along);
        double swell = 1.0 + (tuning.WaveAmplitude * Math.Sin(along * tuning.WaveFrequency));
        return direction.Scale(tuning.FlowSpeed * swell * weight);
    }

    internal FlightWrench Resolve(
        PlanarVector position,
        PlanarVector velocity,
        double mass,
        double coupling)
    {
        double along = (position - tuning.Center).Dot(direction);
        double weight = AuthorityWeight(position, along);
        if (weight < NegligibleWeight)
        {
            return FlightWrench.Zero;
        }

        double swell = 1.0 + (tuning.WaveAmplitude * Math.Sin(along * tuning.WaveFrequency));
        PlanarVector flow = direction.Scale(tuning.FlowSpeed * swell);
        PlanarVector force = (flow - velocity)
            .Scale(tuning.ResponseGain * weight * mass * coupling);
        return new FlightWrench(ClampMagnitude(force, tuning.MaximumForce), NoYawTorque);
    }

    /// <summary>
    /// How much of the band reaches this point: the Gaussian across its width,
    /// gated at each end over the same scale so a band fades out rather than
    /// ending on a wall the ship can feel.
    /// </summary>
    private double AuthorityWeight(PlanarVector position, double along)
    {
        PlanarVector lateral = (position - tuning.Center) - direction.Scale(along);
        double lateralWeight = Math.Exp(-Math.Pow(
            lateral.Magnitude / tuning.Width,
            GaussianSquaredExponent));
        double beyondEnd = Math.Max(0.0, Math.Abs(along) - (tuning.Length / 2.0));
        double endGate = Math.Exp(-Math.Pow(
            beyondEnd / tuning.Width,
            GaussianSquaredExponent));
        return lateralWeight * endGate;
    }

    private static PlanarVector ClampMagnitude(PlanarVector force, double maximum)
    {
        double magnitude = force.Magnitude;
        return magnitude > maximum
            ? force.Scale(maximum / magnitude)
            : force;
    }
}
