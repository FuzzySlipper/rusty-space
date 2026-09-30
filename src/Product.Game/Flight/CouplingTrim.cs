namespace Rusty.Space.Product.Flight;

/// <summary>
/// The pilot's retained coupling setting. Relative trim winds this demand;
/// the installed emitter alone owns the physical response to it. Releasing
/// trim leaves the requested setting in place, rather than commanding zero.
/// </summary>
internal sealed class CouplingTrim
{
    private const double MinimumLevel = 0.0;
    private const double MaximumLevel = 1.0;
    private const double NoTrimIntent = 0.0;

    private readonly CouplingTuning tuning;
    private double level;

    internal CouplingTrim(CouplingTuning tuning)
    {
        this.tuning = tuning;
        level = tuning.DefaultLevel;
    }

    internal double Level => level;

    /// <summary>
    /// Winds the requested setting over one admitted fixed step. FullSweepTime
    /// is the input dial rate, not an emitter response or a force limit.
    /// </summary>
    internal void Advance(FlightCommand command, TimeSpan step)
    {
        if (command.EmergencyUncouple)
        {
            level = MinimumLevel;
            return;
        }

        double trimIntent = Math.Clamp(command.CouplingTrim, -MaximumLevel, MaximumLevel);
        if (trimIntent == NoTrimIntent)
        {
            return;
        }

        double travel = trimIntent * (step.TotalSeconds / tuning.FullSweepTime.TotalSeconds);
        level = Math.Clamp(level + travel, MinimumLevel, MaximumLevel);
    }

    internal void Reset() => level = tuning.DefaultLevel;
}
