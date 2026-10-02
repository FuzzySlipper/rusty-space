namespace Rusty.Space.Product.ShipSystems;

/// <summary>The drive's rechargeable burst reserve above its sustainable output.</summary>
internal sealed record DriveReserveTuning(
    double Capacity,
    double DrawPerSecond,
    double RechargePerSecond,
    double WarningFraction,
    double DeratingFraction,
    double SustainableOutputFraction)
{
    internal DriveReserveTuning Validate()
    {
        if (!double.IsFinite(Capacity) || Capacity <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(Capacity));
        if (!double.IsFinite(DrawPerSecond) || DrawPerSecond <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(DrawPerSecond));
        if (!double.IsFinite(RechargePerSecond) || RechargePerSecond <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(RechargePerSecond));
        if (!double.IsFinite(DeratingFraction) || DeratingFraction <= 0.0 || DeratingFraction >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(DeratingFraction));
        if (!double.IsFinite(WarningFraction) || WarningFraction <= DeratingFraction || WarningFraction > 1.0)
            throw new ArgumentOutOfRangeException(nameof(WarningFraction));
        if (!double.IsFinite(SustainableOutputFraction) || SustainableOutputFraction <= 0.0 || SustainableOutputFraction >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(SustainableOutputFraction));
        return this;
    }
}
