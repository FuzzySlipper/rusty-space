using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

internal sealed record FlightBodyTuning(
    PlanarVector SpawnPosition,
    double SpawnHeight,
    double SpawnHeadingRadians,
    PlanarVector HalfExtents,
    double HalfHeight,
    double Mass)
{
    private const double MinimumPositiveMagnitude = 0.0;

    internal FlightBodyTuning Validate()
    {
        ValidateFinite(SpawnPosition, nameof(SpawnPosition));
        ValidateFinite(SpawnHeight, nameof(SpawnHeight));
        ValidateFinite(SpawnHeadingRadians, nameof(SpawnHeadingRadians));
        ValidatePositiveFinite(HalfExtents.X, nameof(HalfExtents));
        ValidatePositiveFinite(HalfExtents.Z, nameof(HalfExtents));
        ValidatePositiveFinite(HalfHeight, nameof(HalfHeight));
        ValidatePositiveFinite(Mass, nameof(Mass));
        return this;
    }

    private static void ValidateFinite(PlanarVector value, string parameterName)
    {
        ValidateFinite(value.X, parameterName);
        ValidateFinite(value.Z, parameterName);
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
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
