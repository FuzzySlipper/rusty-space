namespace Rusty.Space.Product.Flight;

/// <summary>
/// How quickly relative trim winds the pilot's retained coupling demand. <see cref="DefaultLevel"/> is where a fresh ship leaves the
/// cradle: engaged enough that the local field and every drift band bend its
/// line, and low enough that main drive still decides where the ship goes.
/// </summary>
internal sealed record CouplingTuning(double DefaultLevel, TimeSpan FullSweepTime)
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

        if (FullSweepTime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(FullSweepTime));
        }

        return this;
    }
}
