using Rusty.Space.Product.Approach;
using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// What the Engine's contacts did to the hull on the step it last resolved: the
/// push the hull was given, in world axes and in the hull's own, how hard it was,
/// and the authored obstacle on the other end of it when there was one.
/// </summary>
/// <remarks>
/// <para>
/// This is a reading, not an instruction. The Engine has already applied the
/// impulse to the body — the hull's velocity arriving on the next readout is the
/// proof — so nothing downstream re-applies it, and a second push for the same
/// contact would bill the player twice for one mistake.
/// </para>
/// <para>
/// <see cref="LocalImpulse"/> is the same push in the hull's frame, which is what
/// an impact report wants: a player struck on the starboard quarter is told about
/// the starboard quarter, and where that was in the chart is a separate fact
/// already carried by the hull's position.
/// </para>
/// </remarks>
internal readonly record struct HullImpact(
    bool Present,
    PlanarVector Impulse,
    PlanarVector LocalImpulse,
    double Magnitude,
    ObstacleId? Struck)
{
    /// <summary>
    /// Nothing touched the hull on the step the readout came from.
    /// </summary>
    internal static HullImpact None { get; } = new(
        Present: false,
        PlanarVector.Zero,
        PlanarVector.Zero,
        0.0,
        Struck: null);
}
