namespace Rusty.Space.Product.ShipSystems;

/// <summary>Heat gained at full demand and cooling as a fraction of accumulated heat per second.</summary>
internal sealed record PartThermalTuning(
    double HeatPerSecondAtFullDemand,
    double CoolingPerSecond,
    double WarningTemperature,
    double DeratingTemperature,
    double FullDeratingTemperature,
    double MinimumOutputFraction,
    double DriveHeatMultiplier)
{
    internal PartThermalTuning Validate()
    {
        if (!double.IsFinite(HeatPerSecondAtFullDemand) || HeatPerSecondAtFullDemand < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(HeatPerSecondAtFullDemand));
        }
        if (!double.IsFinite(CoolingPerSecond) || CoolingPerSecond < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(CoolingPerSecond));
        }
        if (!double.IsFinite(WarningTemperature) || WarningTemperature <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(WarningTemperature));
        if (!double.IsFinite(DeratingTemperature) || DeratingTemperature <= WarningTemperature)
            throw new ArgumentOutOfRangeException(nameof(DeratingTemperature));
        if (!double.IsFinite(FullDeratingTemperature) || FullDeratingTemperature <= DeratingTemperature)
            throw new ArgumentOutOfRangeException(nameof(FullDeratingTemperature));
        if (!double.IsFinite(MinimumOutputFraction) || MinimumOutputFraction <= 0.0 || MinimumOutputFraction >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(MinimumOutputFraction));
        if (!double.IsFinite(DriveHeatMultiplier) || DriveHeatMultiplier <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(DriveHeatMultiplier));
        return this;
    }
}
