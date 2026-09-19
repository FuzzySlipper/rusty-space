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
        this.tuning = tuning.Validate();
        level = tuning.DefaultLevel;
    }

    internal double Level => level;

    /// <summary>
    /// Advances the actuator over one fixed step from the level the caller has
    /// staged. Pure about the actuator, exactly like the throttle spool: it
    /// moves the value it was handed and leaves publication to
    /// <see cref="Commit"/>.
    /// </summary>
    internal double Prepare(FlightCommand command, TimeSpan step, double currentLevel)
    {
        ValidateStep(step);

        if (command.EmergencyUncouple)
        {
            return MinimumLevel;
        }

        double trimIntent = Math.Clamp(command.CouplingTrim, -MaximumLevel, MaximumLevel);
        if (trimIntent == NoTrimIntent)
        {
            return currentLevel;
        }

        double travel = trimIntent * (step.TotalSeconds / tuning.TrimResponse.TotalSeconds);
        return Math.Clamp(currentLevel + travel, MinimumLevel, MaximumLevel);
    }

    internal void Commit(double nextLevel) => level = nextLevel;

    internal void Reset() => level = tuning.DefaultLevel;

    private static void ValidateStep(TimeSpan step)
    {
        if (step <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(step));
        }
    }
}
