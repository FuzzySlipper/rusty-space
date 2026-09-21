using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// Where the hull is on its way to, read ahead of it: points walked forward from
/// the ship's actual state along the line its current push keeps taking it, each
/// sample the same interval apart.
/// </summary>
/// <remarks>
/// This is a reading of the present, extended forward, not a promise about the
/// future. It assumes the player holds the controls exactly as they are: the
/// throttle stays where it is spooled, the bow stays where it points, and the
/// coupling stays at its trim. What it does re-read at every step is the
/// environment at the point the line has reached, which is what makes the line
/// bend toward a current the ship has not entered yet — the reading the view
/// exists to give. It contains no term that slows the ship which is not a force
/// the hull would actually feel.
/// </remarks>
internal readonly record struct FlightPath(
    ReadOnlyMemory<PlanarVector> Points,
    TimeSpan SampleInterval)
{
    /// <summary>A path with nothing on it, as a flight at rest before a turn has run.</summary>
    internal static FlightPath None { get; } = new(
        ReadOnlyMemory<PlanarVector>.Empty,
        TimeSpan.Zero);

    internal bool IsEmpty => Points.Length == 0;
}
