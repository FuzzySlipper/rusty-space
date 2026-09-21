namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// What a hit costs the hardware fitted to a hull, and how long it takes the
/// crew to hold something back where it belongs.
/// </summary>
/// <remarks>
/// <para>
/// Impulses are the Engine's, in newton-seconds, and they are what a contact is
/// worth: a two-kilogram hull arriving at three metres a second carries about
/// six of them. The thresholds below are written against that, so a brush is a
/// brush at any speed the hull is capable of and a clunk is a clunk.
/// </para>
/// <para>
/// A contact below <see cref="GlancingImpulse"/> costs nothing at all: the push
/// is the event, and hardware rated for the hull shrugs off being nudged. Past
/// it, health is lost in proportion to what the nudge exceeded, which keeps the
/// cost of a hit continuous in the hit rather than waiting behind a threshold
/// for a player to trip over.
/// </para>
/// <para>
/// <see cref="MinimumHealth"/> is the floor nothing goes through. A hull that
/// has taken a beating flies badly and knows it, but a physics mistake is meant
/// to open a situation rather than end one, and hardware that gives out entirely
/// ends the situation on the Engine's terms instead of the player's.
/// </para>
/// <para>
/// Hardware knocked past <see cref="KnockoutImpulse"/> latches: a vane that was
/// told to center stays <see cref="TrimOffsetFraction"/> of its rated authority
/// off where it was told, and stays there until the crew has held a patch on it
/// for <see cref="RepairTime"/>. What is dented stays dented — a patch holds a
/// mechanism in place, it does not un-bend it.
/// </para>
/// </remarks>
internal sealed record DamageTuning(
    double MinimumHealth,
    double HealthPerUnitImpulse,
    double GlancingImpulse,
    double KnockoutImpulse,
    double TrimOffsetFraction,
    TimeSpan RepairTime)
{
    private const double MinimumHealthFloor = 0.0;
    private const double FullHealth = 1.0;
    private const double MinimumImpulse = 0.0;
    private const double NoOffset = 0.0;
    private const double FullTrimOffset = 1.0;

    internal DamageTuning Validate()
    {
        if (!double.IsFinite(MinimumHealth)
            || MinimumHealth <= MinimumHealthFloor
            || MinimumHealth >= FullHealth)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumHealth));
        }

        if (!double.IsFinite(HealthPerUnitImpulse) || HealthPerUnitImpulse <= MinimumImpulse)
        {
            throw new ArgumentOutOfRangeException(nameof(HealthPerUnitImpulse));
        }

        if (!double.IsFinite(GlancingImpulse) || GlancingImpulse < MinimumImpulse)
        {
            throw new ArgumentOutOfRangeException(nameof(GlancingImpulse));
        }

        if (!double.IsFinite(KnockoutImpulse) || KnockoutImpulse <= GlancingImpulse)
        {
            throw new ArgumentOutOfRangeException(nameof(KnockoutImpulse));
        }

        if (!double.IsFinite(TrimOffsetFraction)
            || TrimOffsetFraction <= NoOffset
            || TrimOffsetFraction > FullTrimOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(TrimOffsetFraction));
        }

        if (RepairTime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(RepairTime));
        }

        return this;
    }
}
