using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Approach;

/// <summary>
/// An angular piece of approach geometry: a plate, a spar, or a section of
/// something that was built and came apart. Where a boulder is met, a block is
/// landed on, and the difference is that a block has faces a hull can catch on
/// and a corner a glancing line can slide along.
/// </summary>
/// <remarks>
/// <see cref="HeadingRadians"/> is the turn the piece sits at on the chart, and
/// it is what makes a spar worth authoring: the same obstacle flown at forty
/// degrees presents a face, and flown end-on presents a knife edge.
/// <see cref="HalfHeight"/> stands off the chart plane; a hull is flat and a
/// plate is thinner, and neither needs more than that to be met.
/// </remarks>
internal sealed record WreckBlock(
    ObstacleId Id,
    PlanarVector Position,
    double HeadingRadians,
    double Mass,
    double Friction,
    PlanarVector HalfExtents,
    double HalfHeight)
    : ObstacleDefinition(Id, Position, HeadingRadians, Mass, Friction)
{
    private const double MinimumExtent = 0.0;

    internal WreckBlock Validate()
    {
        ValidatePlacement();

        if (!double.IsFinite(HalfExtents.X) || !double.IsFinite(HalfExtents.Z)
            || HalfExtents.X <= MinimumExtent || HalfExtents.Z <= MinimumExtent)
        {
            throw new ArgumentOutOfRangeException(nameof(HalfExtents));
        }

        if (!double.IsFinite(HalfHeight) || HalfHeight <= MinimumExtent)
        {
            throw new ArgumentOutOfRangeException(nameof(HalfHeight));
        }

        return this;
    }
}
