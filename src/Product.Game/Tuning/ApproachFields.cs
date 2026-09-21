using Rusty.Space.Product.Approach;
using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Tuning;

/// <summary>
/// The authored approach spaces a chart can put a hull into. Each is a handful
/// of masses with a line through them, close enough together that the line can
/// be flown badly and far enough apart that it can be flown well.
/// </summary>
/// <remarks>
/// <para>
/// Kestrel approach sits on the spawn line: a hull leaving the cradle at the
/// default heading runs down +X toward it, with the planet's well pulling
/// alongside. The gate between the plate and the spar is two and a half units
/// wide against a hull one and a half across, which is a gap a careful line
/// goes through clean and a hurried one does not.
/// </para>
/// <para>
/// Everything past the gate is recovery room rather than another test: one
/// boulder just off the line to be honest about what carrying speed through a
/// gap costs, and a wreck section and a small boulder further out so a hull
/// that misses has somewhere to arrive rather than an empty chart to explain it.
/// </para>
/// </remarks>
internal static class ApproachFields
{
    /// <summary>
    /// The first approach every hull is flown down: a wide plate and a turned
    /// spar holding a gate on the spawn line, a boulder just past it, and the
    /// remains further out.
    /// </summary>
    internal static ApproachFieldDefinition KestrelApproach { get; } = new(
        Name: "kestrel-approach",
        Obstacles:
        [
            new WreckBlock(
                Id: new ObstacleId("wreck-plate-a"),
                Position: new PlanarVector(8.0, -3.4),
                HeadingRadians: 0.35,
                Mass: 6.0,
                Friction: 0.40,
                HalfExtents: new PlanarVector(1.2, 2.3),
                HalfHeight: 0.9),
            new WreckBlock(
                Id: new ObstacleId("wreck-spar-b"),
                Position: new PlanarVector(8.2, 3.0),
                HeadingRadians: -0.50,
                Mass: 4.0,
                Friction: 0.35,
                HalfExtents: new PlanarVector(0.6, 1.6),
                HalfHeight: 0.5),
            new Boulder(
                Id: new ObstacleId("boulder-kestrel"),
                Position: new PlanarVector(11.4, -1.4),
                Mass: 9.0,
                Friction: 0.45,
                Radius: 1.3),
            new WreckBlock(
                Id: new ObstacleId("wreck-hull-c"),
                Position: new PlanarVector(12.6, 3.2),
                HeadingRadians: 1.10,
                Mass: 7.5,
                Friction: 0.40,
                HalfExtents: new PlanarVector(1.8, 0.8),
                HalfHeight: 0.7),
            new Boulder(
                Id: new ObstacleId("boulder-fan"),
                Position: new PlanarVector(5.4, 4.6),
                Mass: 5.0,
                Friction: 0.50,
                Radius: 0.9),
        ]);
}
