namespace Rusty.Space.Product.Flight;

/// <summary>
/// How eagerly the ship's coupling actuator takes the player's trim toward a
/// new setting. <see cref="DefaultLevel"/> is where a fresh ship leaves the
/// cradle: engaged enough that the local field and every drift band bend its
/// line, and low enough that main drive still decides where the ship goes.
/// </summary>
internal sealed record CouplingTuning(double DefaultLevel, TimeSpan TrimResponse)
{
    private const double MinimumLevel = 0.0;
    private const double MaximumLevel = 1.0;

    internal CouplingTuning Validate()
    {
        if (!double.IsFinite(DefaultLevel)
            || DefaultLevel < MinimumLevel
            || DefaultLevel > MaximumLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(DefaultLevel));
        }

        if (TrimResponse <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(TrimResponse));
        }

        return this;
    }
}
