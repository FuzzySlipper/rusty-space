namespace Rusty.Space.Product.ShipSystems;

/// <summary>Live hardware facts handed to telemetry, the bridge and the DOM.</summary>
internal readonly record struct ShipSystemsReading(
    double ReserveFraction,
    double DriveTemperature,
    double DriveOutputFraction,
    bool ReserveLow,
    bool HeatWarning,
    int JammedParts,
    double PatchProgress)
{
    internal static ShipSystemsReading Ready { get; } = new(1.0, 0.0, 1.0, false, false, 0, 0.0);
}
