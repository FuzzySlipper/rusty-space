using Rusty.Engine.Mechanics;

namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// Stored burst energy. The sustainable drive remains available when this is
/// empty; steering and field hardware do not draw from this reserve.
/// </summary>
internal sealed class DriveReserve
{
    private readonly DriveReserveTuning tuning;
    private readonly Track energy;

    internal DriveReserve(DriveReserveTuning tuning)
    {
        this.tuning = tuning.Validate();
        energy = new Track(new Stat(tuning.Capacity));
    }

    internal double Fraction => energy.Value / energy.MaximumValue;

    internal bool Low => Fraction <= tuning.WarningFraction;

    internal double OutputFraction => tuning.SustainableOutputFraction
        + ((1.0 - tuning.SustainableOutputFraction)
            * Math.Min(1.0, Fraction / tuning.DeratingFraction));

    internal void Advance(double demandFraction, TimeSpan step)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(step, TimeSpan.Zero);
        if (!double.IsFinite(demandFraction))
            throw new ArgumentOutOfRangeException(nameof(demandFraction));

        double demand = Math.Clamp(Math.Abs(demandFraction), 0.0, 1.0);
        // Only demand beyond the sustainable supply spends the burst reserve.
        // Gentle manoeuvres recharge it; coasting recharges it fastest.
        double burst = Math.Max(0.0, demand - tuning.SustainableOutputFraction)
            / (1.0 - tuning.SustainableOutputFraction);
        energy.Spend(Math.Min(energy.Value, burst * tuning.DrawPerSecond * step.TotalSeconds));
        energy.Restore((1.0 - demand) * tuning.RechargePerSecond * step.TotalSeconds);
    }

    internal void Reset() => energy.Restore(energy.MaximumValue);
}
