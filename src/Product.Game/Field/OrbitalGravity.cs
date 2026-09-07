using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Field;

/// <summary>
/// Resolves the orbital-well push for one ship position. The product owns the
/// force meaning; the Engine owns the integration through the DynamicsAction
/// force the caller publishes.
/// </summary>
internal sealed class OrbitalGravity
{
    private const double MinimumDistance = 1e-6;
    private const double FalloffExponent = 2.0;

    private readonly OrbitalGravityTuning tuning;

    internal OrbitalGravity(OrbitalGravityTuning tuning)
    {
        this.tuning = tuning.Validate();
    }

    internal FlightWrench Resolve(PlanarVector position, double mass)
    {
        PlanarVector offset = position - tuning.Center;
        double distance = offset.Magnitude;
        if (distance < MinimumDistance)
        {
            return FlightWrench.Zero;
        }

        double falloff = 1.0 / (1.0 + Math.Pow(distance / tuning.Radius, FalloffExponent));
        PlanarVector inward = offset.Scale(-1.0 / distance);
        PlanarVector tangential = new(-inward.Z, inward.X);
        PlanarVector acceleration =
            inward.Scale(tuning.Strength) + tangential.Scale(tuning.Swirl);
        return new FlightWrench(
            ClampMagnitude(acceleration.Scale(falloff * mass), tuning.MaximumForce),
            0.0);
    }

    private static PlanarVector ClampMagnitude(PlanarVector force, double maximum)
    {
        double magnitude = force.Magnitude;
        return magnitude > maximum
            ? force.Scale(maximum / magnitude)
            : force;
    }
}
