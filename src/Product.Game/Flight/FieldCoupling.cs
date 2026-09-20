namespace Rusty.Space.Product.Flight;

/// <summary>
/// The ship's coupling actuator: the one value that decides how much of the
/// local field and of every drift band the hull actually feels. Trim moves it
/// at a bounded rate so the contest between environment and controller can be
/// wound up and down smoothly, and an emergency uncouple dumps it to nothing at
/// once. It gates flow-coupled push only. The orbital well is a mass well
/// rather than a field drive, so it pulls the same ship at any coupling.
/// </summary>
internal sealed class FieldCoupling
{
    private const double MinimumLevel = 0.0;
    private const double MaximumLevel = 1.0;
    private const double NoTrimIntent = 0.0;

    private readonly CouplingTuning tuning;
    private double level;

    internal FieldCoupling(CouplingTuning tuning)
    {
        this.tuning = tuning;
        level = tuning.DefaultLevel;
    }

    internal double Level => level;

    /// <summary>
    /// Travels the actuator over one admitted fixed step. The level is the
    /// actuator's own: the caller reads it back with <see cref="Level"/>, so an
    /// interval can only be counted once and a turn that admits four steps gets
    /// four calls, not one call with four times the travel.
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

        double travel = trimIntent * (step.TotalSeconds / tuning.TrimResponse.TotalSeconds);
        level = Math.Clamp(level + travel, MinimumLevel, MaximumLevel);
    }

    internal void Reset() => level = tuning.DefaultLevel;
}
