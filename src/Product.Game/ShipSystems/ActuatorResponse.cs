namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// One actuator's answer to a demand, carried between admitted fixed steps: it
/// takes time to get to where it is told, and it can arrive past the demand and
/// be found on the way back.
/// </summary>
/// <remarks>
/// <para>
/// The travel is the second-order response every actuator on the ship is
/// described by:
/// </para>
/// <code>
/// response'' + 2·ζ·ω·response' + ω²·response = ω²·command
/// </code>
/// <para>
/// <c>ω</c> is <see cref="ActuatorTuning.Frequency"/> and <c>ζ</c> is
/// <see cref="ActuatorTuning.DampingRatio"/>. Frequency decides how quickly the
/// actuator closes on a demand and damping ratio decides whether it overshoots.
/// Nothing here is random: the same demand over the same admitted steps always
/// produces the same travel, which is what makes a ship's quirk something a
/// player can learn and later use.
/// </para>
/// <para>
/// An interval is integrated in however many sub-intervals its own length needs
/// to keep <c>ω·dt</c> small, so a stiff <see cref="ActuatorTuning"/> stays
/// stable at any admitted rate. This integrates its own value only. It never
/// touches the body, never pushes the hull, and is not a second integrator
/// beside the Engine's: the delivered value is handed to the owner that turns it
/// into force, and the Engine remains the only thing that moves the ship.
/// </para>
/// </remarks>
internal sealed class ActuatorResponse
{
    // Sub-intervals are chosen so one never carries more than this much of the
    // response's own angle; past it, explicit integration starts to ring on its
    // own and the wobble would say something about the arithmetic rather than
    // about the hardware.
    private const double MaximumResponseAnglePerInterval = 0.30;
    private const int SingleInterval = 1;
    private const double NoLimit = 0.0;
    private const double NoDemand = 0.0;
    private const double MinimumDampingRatio = 0.0;

    private readonly ActuatorTuning tuning;
    private readonly double limit;
    private double value;
    private double velocity;

    internal ActuatorResponse(ActuatorTuning tuning, double limit)
    {
        tuning.Validate();
        if (!double.IsFinite(limit) || limit < NoLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        this.tuning = tuning;
        this.limit = limit;
    }

    /// <summary>Where the actuator actually is, as delivered to its owner.</summary>
    internal double Value => value;

    /// <summary>How fast it is travelling, in value per second.</summary>
    internal double Velocity => velocity;

    /// <summary>The stop the actuator is built against, in either direction.</summary>
    internal double Limit => limit;

    /// <summary>The damping ratio this actuator was tuned with.</summary>
    internal double DampingRatio => tuning.DampingRatio;

    /// <summary>
    /// Whether the demand asked for more than the actuator can reach, so its
    /// travel is against the stop rather than toward the demand.
    /// </summary>
    internal bool Saturated { get; private set; }

    /// <summary>
    /// Travels the actuator over one admitted fixed step. The interval is the
    /// Engine's, handed in by the owner that is resolving this substep, so a
    /// turn that admits four steps advances four steps of travel.
    /// </summary>
    internal void Advance(double demand, TimeSpan step) => Advance(demand, tuning.DampingRatio, step);

    /// <summary>
    /// The same travel with the damping ratio the actuator answers with this
    /// step spelled out, for hardware whose damping depends on the load it is
    /// working against. Worn effectors ring only once the flow is hard enough to
    /// ring them, and this is where the owner says so.
    /// </summary>
    internal void Advance(double demand, double dampingRatio, TimeSpan step)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(step, TimeSpan.Zero);
        if (!double.IsFinite(dampingRatio) || dampingRatio < MinimumDampingRatio)
        {
            throw new ArgumentOutOfRangeException(nameof(dampingRatio));
        }

        double seconds = step.TotalSeconds;
        Saturated = Math.Abs(demand) > limit;

        int intervals = IntervalsFor(seconds);
        double interval = seconds / intervals;
        double restoring = tuning.Frequency * tuning.Frequency;
        double friction = 2.0 * dampingRatio * tuning.Frequency;
        for (int visited = 0; visited < intervals; visited++)
        {
            // Semi-implicit order: the new velocity, then the position that new
            // velocity produces. It keeps the energy of an underdamped response
            // from creeping upward over a long flight, which an explicit
            // position-then-velocity step does not.
            velocity += ((restoring * (demand - value)) - (friction * velocity)) * interval;
            value += velocity * interval;
        }

        HoldAtStop();
    }

    internal void Reset()
    {
        value = NoDemand;
        velocity = NoDemand;
        Saturated = false;
    }

    private int IntervalsFor(double seconds)
    {
        double angle = tuning.Frequency * seconds;
        if (angle <= MaximumResponseAnglePerInterval)
        {
            return SingleInterval;
        }

        return checked((int)Math.Ceiling(angle / MaximumResponseAnglePerInterval));
    }

    private void HoldAtStop()
    {
        double bounded = Math.Clamp(value, -limit, limit);
        if (bounded == value)
        {
            return;
        }

        // Against the stop the actuator has nowhere further to go, so the travel
        // that was heading for the stop is spent rather than stored: nothing
        // accumulates that would launch the value back the other side.
        value = bounded;
        if (value * velocity > NoDemand)
        {
            velocity = NoDemand;
        }
    }
}
