using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// One side of the heading effector pair: the hardware that yaws the ship by
/// pushing on one side of the keel, and the wear that makes one side answer
/// differently from the other.
/// </summary>
/// <remarks>
/// <para>
/// A stabilizer is rated for the force it can put on the keel
/// (<see cref="ForceAuthority"/>) and for how fast and how exactly it delivers it
/// (<see cref="Response"/>). The rated peak is what the stop is built against:
/// worn hardware is worn in the <see cref="Response"/> — slower to get there, and
/// ringing once the load is hard enough to make it ring — not in a ceiling clipped
/// below what it is rated for. <see cref="Health"/> is the condition the part is in
/// today: how much of its own punishment it has left room for, which is what a
/// contact spends. Repair, heat limits, and how damage accumulates belong to the
/// health and thermal phase, not here.
/// </para>
/// <para>
/// Worn hardware does not simply get weaker, it answers differently. Below
/// <see cref="LoadThreshold"/> a tired side still closes on a demand as designed.
/// Past it the response rings toward <see cref="WearDampingRatio"/>, and the
/// part adds a standing pull of <see cref="PullPerUnitLoad"/> for every unit of
/// field load the hull is carrying. Both are continuous in the load, so the
/// onset is somewhere a player can find and remember rather than a switch that
/// flips between one turn and the next.
/// </para>
/// <para>
/// The pair is mounted mirrored: the port side's <see cref="PartDefinition.Mount"/>
/// is to <c>-Z</c>, the starboard side's to <c>+Z</c>. Each side is authored and
/// advanced on its own, which is what lets the two of them disagree.
/// </para>
/// </remarks>
internal sealed record StabilizerDefinition(
    PartId Id,
    PlanarVector Mount,
    double Mass,
    double Health,
    double ForceAuthority,
    ActuatorTuning Response,
    double LoadThreshold,
    double WearDampingRatio,
    double PullPerUnitLoad)
    : PartDefinition(Id, PartRole.Stabilizer, Mount, Mass, Health)
{
    private const double MinimumPositiveAuthority = 0.0;
    private const double MinimumLoad = 0.0;
    private const double FullLoad = 1.0;
    private const double MinimumDampingRatio = 0.0;

    /// <summary>
    /// The stop this side's actuator is built against: its rated authority, which
    /// a tired side still reaches — eventually, and not without ringing on the way.
    /// What a part can still put up once something has struck it is the installed
    /// part's own business, applied to what these stops let through.
    /// </summary>
    internal double DeliveredAuthority => ForceAuthority;

    /// <summary>
    /// How much of this side's wear is showing at a given field load: nothing
    /// below the threshold, all of it at full load, and a straight line between.
    /// </summary>
    internal double WearAt(double load)
    {
        if (load <= LoadThreshold)
        {
            return MinimumLoad;
        }

        return Math.Clamp(
            (load - LoadThreshold) / (FullLoad - LoadThreshold),
            MinimumLoad,
            FullLoad);
    }

    /// <summary>
    /// The damping ratio this side answers with at a given field load. Healthy
    /// hardware gives the same answer at every load.
    /// </summary>
    internal double DampingRatioAt(double load)
    {
        double wear = WearAt(load);
        return (Response.DampingRatio * (FullLoad - wear)) + (WearDampingRatio * wear);
    }

    /// <summary>
    /// The standing yaw pull this side adds at a given field load, in the
    /// heading-positive sense. Healthy hardware adds none.
    /// </summary>
    internal double PullAt(double load) => PullPerUnitLoad * load;

    internal StabilizerDefinition Validate()
    {
        ValidateIdentity();

        if (!double.IsFinite(ForceAuthority) || ForceAuthority <= MinimumPositiveAuthority)
        {
            throw new ArgumentOutOfRangeException(nameof(ForceAuthority));
        }

        Response.Validate();

        if (!double.IsFinite(LoadThreshold)
            || LoadThreshold < MinimumLoad
            || LoadThreshold >= FullLoad)
        {
            throw new ArgumentOutOfRangeException(nameof(LoadThreshold));
        }

        if (!double.IsFinite(WearDampingRatio) || WearDampingRatio < MinimumDampingRatio)
        {
            throw new ArgumentOutOfRangeException(nameof(WearDampingRatio));
        }

        if (!double.IsFinite(PullPerUnitLoad))
        {
            throw new ArgumentOutOfRangeException(nameof(PullPerUnitLoad));
        }

        return this;
    }
}
