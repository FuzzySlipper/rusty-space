namespace Rusty.Space.Product.ShipSystems;

/// <summary>Heat gained at full demand and cooling as a fraction of accumulated heat per second.</summary>
internal sealed record PartThermalTuning(double HeatPerSecondAtFullDemand, double CoolingPerSecond)
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
        return this;
    }
}
