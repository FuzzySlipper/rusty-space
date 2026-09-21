namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// One part as it stands on this hull: what it is, how warm it is, where its
/// actuator actually is, and whether it managed what it was asked for.
/// </summary>
/// <remarks>
/// <para>
/// A definition is a design; this is a specific piece of hardware that has been
/// fitted. The part carries the state that only a fitted part has: the travel of
/// its actuator between admitted steps, and the heat it has taken on. Nothing
/// here decides how the ship should move — the part reports what its actuator
/// reached, and the owner that asked for it turns that into force.
/// </para>
/// <para>
/// Heat accumulates while a part is worked and gives itself up when it is left
/// alone. It is reported so a hot part is visible to the player's instruments.
/// </para>
/// <para>
/// A part also carries what a hit left behind. Health starts where it was fitted
/// and only goes down: a contact takes a part's rating with it, and nothing this
/// owner does gives it back. What a hard enough contact does besides that is
/// latch something — a vane that was told to center stays held off center until
/// the crew has had <see cref="DamageTuning.RepairTime"/> on it. A patch is what
/// clears a latch; it is not what un-bends a part, so a hull that has been
/// struck keeps the dent after the repair and flies with it.
/// </para>
/// </remarks>
internal sealed class InstalledPart
{
    // Heat gained in one second at full demand, and the fraction of the heat it
    // carries that a part gives up in one second. Together they settle a part
    // worked continuously at its rating just under the nominal envelope, so a
    // part asked to overdeliver is the one that climbs past it.
    private const double HeatPerSecondAtFullDemand = 0.25;
    private const double CoolingPerSecond = 0.30;
    private const double AmbientTemperature = 0.0;
    private const double NoDemand = 0.0;
    private const double NoTrimOffset = 0.0;
    private const double NoDamage = 0.0;
    private const double NoRepair = 0.0;
    private const double FullRepair = 1.0;

    private readonly ActuatorResponse response;
    private readonly DamageTuning damage;
    private readonly double trimOffsetWhenLatched;
    private readonly double fittedHealth;
    private double temperature = AmbientTemperature;
    private double health;
    private double repair;

    internal InstalledPart(
        PartDefinition definition,
        ActuatorResponse response,
        DamageTuning damage,
        double trimOffsetWhenLatched = NoTrimOffset)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        this.response = response ?? throw new ArgumentNullException(nameof(response));
        this.damage = damage ?? throw new ArgumentNullException(nameof(damage));
        this.trimOffsetWhenLatched = trimOffsetWhenLatched;
        fittedHealth = definition.Health;
        health = definition.Health;
    }

    internal PartDefinition Definition { get; }

    internal PartId Id => Definition.Id;

    /// <summary>
    /// The fraction of its rating this part can still put up. It starts where it
    /// was fitted and a contact takes it down, never below the floor the damage
    /// tuning allows; nothing lifts it back.
    /// </summary>
    internal double Health => health;

    /// <summary>
    /// How much of what this part was fitted with it can still put up: its health
    /// against the health it arrived with, so a part that came off something
    /// already dented is not charged for the dent twice. One until a contact takes
    /// something off it.
    /// </summary>
    internal double DeliveryFraction => fittedHealth <= NoDamage
        ? NoDamage
        : health / fittedHealth;

    /// <summary>
    /// How warm the part is against its nominal envelope: <c>0</c> at ambient,
    /// <c>1</c> at the envelope, and past it for hardware being overworked.
    /// </summary>
    internal double Temperature => temperature;

    /// <summary>Where the part's actuator stands after the last advance.</summary>
    internal double ActuatorValue => response.Value;

    /// <summary>How fast the part's actuator is travelling.</summary>
    internal double ActuatorTravel => response.Velocity;

    /// <summary>The stop the part's actuator is built against.</summary>
    internal double ActuatorLimit => response.Limit;

    /// <summary>Whether the last demand asked for more than this part can reach.</summary>
    internal bool Saturated => response.Saturated;

    /// <summary>
    /// Travels this part's actuator toward a demand over one admitted fixed step
    /// and takes on the heat that working it costs. Returns where the actuator
    /// ended up, which is what the requesting owner delivers.
    /// </summary>
    internal double Advance(double demand, double demandAsFractionOfRating, TimeSpan step)
    {
        response.Advance(demand, step);
        Warm(demandAsFractionOfRating, step);
        return response.Value;
    }

    /// <summary>
    /// The same advance with the damping the actuator answers with this step
    /// supplied by the owner, for a part whose wear depends on how hard the
    /// environment is pushing it.
    /// </summary>
    internal double Advance(
        double demand,
        double dampingRatio,
        double demandAsFractionOfRating,
        TimeSpan step)
    {
        response.Advance(demand, dampingRatio, step);
        Warm(demandAsFractionOfRating, step);
        return response.Value;
    }

    /// <summary>
    /// Whether something on this part is held away from where it was told to be.
    /// Only hardware with a vane to jam can be held: a part authored with no trim
    /// offset takes the dent and nothing else.
    /// </summary>
    internal bool OutOfTrim { get; private set; }

    /// <summary>
    /// How far this part's actuator is held off its demand while it is latched,
    /// in the same units the demand is asked in, and nothing while it is not.
    /// </summary>
    internal double TrimOffset => OutOfTrim ? trimOffsetWhenLatched : NoTrimOffset;

    /// <summary>
    /// How far a patch held on this part has got, from nothing to <c>1.0</c> at
    /// the moment the latch lets go. A patch let go of before then is starting
    /// over, not pausing.
    /// </summary>
    internal double RepairProgress => repair;

    /// <summary>
    /// Takes a contact of the given impulse, in newton-seconds. Health goes by
    /// whatever the hit exceeded a glancing brush with, and a hit hard enough on
    /// hardware that can be held latches it. Returns the health actually lost,
    /// which is nothing once the floor has been reached: a part at the floor is
    /// still latched by a hard enough hit, it just has no rating left to lose.
    /// </summary>
    internal double TakeImpact(double impulse)
    {
        if (impulse <= damage.GlancingImpulse)
        {
            return NoDamage;
        }

        double cost = damage.HealthPerUnitImpulse * (impulse - damage.GlancingImpulse);
        double room = Math.Max(NoDamage, health - damage.MinimumHealth);
        double lost = Math.Min(cost, room);
        health -= lost;
        if (impulse >= damage.KnockoutImpulse && trimOffsetWhenLatched != NoTrimOffset)
        {
            OutOfTrim = true;
        }

        return lost;
    }

    /// <summary>
    /// Holds a patch on this part for one admitted fixed step. There is nothing
    /// to patch on hardware that is not latched, and a patch let go of is
    /// abandoned rather than parked.
    /// </summary>
    internal void AdvanceRepair(bool patchHeld, TimeSpan step)
    {
        if (!patchHeld || !OutOfTrim)
        {
            repair = NoRepair;
            return;
        }

        repair = Math.Min(FullRepair, repair + (step.TotalSeconds / damage.RepairTime.TotalSeconds));
        if (repair >= FullRepair)
        {
            OutOfTrim = false;
            repair = NoRepair;
        }
    }

    /// <summary>
    /// Brings the actuators home and lets the heat go. What a hit left behind
    /// stays: a reset puts the hull back on the line, it does not send the crew
    /// out with a patch kit.
    /// </summary>
    internal void Reset()
    {
        response.Reset();
        temperature = AmbientTemperature;
        repair = NoRepair;
    }

    private void Warm(double demandAsFractionOfRating, TimeSpan step)
    {
        double seconds = step.TotalSeconds;
        double gathered = Math.Abs(demandAsFractionOfRating) * HeatPerSecondAtFullDemand * seconds;
        double givenUp = (temperature - AmbientTemperature) * CoolingPerSecond * seconds;
        temperature = Math.Max(AmbientTemperature, temperature + gathered - givenUp);
    }
}
