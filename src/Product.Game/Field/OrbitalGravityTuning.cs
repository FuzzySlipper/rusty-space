using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Field;

/// <summary>
/// Gamey orbital pull around a visible body. This is not real orbital
/// mechanics: a bounded inward pull plus a steady tangential swirl, both
/// fading with distance, so a coasting ship visibly bends around the body.
/// The body itself stays a visual-only Engine appearance; it has no Dynamics
/// collider and the product never teleports the ship.
/// </summary>
internal sealed record OrbitalGravityTuning(
    PlanarVector Center,
    double Strength,
    double Swirl,
    double Radius,
    double MaximumForce)
{
    private const double MinimumNonNegativeMagnitude = 0.0;
    private const double MinimumPositiveMagnitude = 0.0;

    internal OrbitalGravityTuning Validate()
    {
        ValidateFinite(Center, nameof(Center));
        ValidateNonNegativeFinite(Strength, nameof(Strength));
        ValidateNonNegativeFinite(Swirl, nameof(Swirl));
        ValidatePositiveFinite(Radius, nameof(Radius));
        ValidatePositiveFinite(MaximumForce, nameof(MaximumForce));
        return this;
    }

    private static void ValidateFinite(PlanarVector value, string parameterName)
    {
        if (!double.IsFinite(value.X) || !double.IsFinite(value.Z))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateNonNegativeFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < MinimumNonNegativeMagnitude)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= MinimumPositiveMagnitude)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
