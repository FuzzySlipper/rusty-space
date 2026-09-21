using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Approach;

/// <summary>
/// One authored piece of approach geometry: where it sits on the chart, how it
/// is turned, how heavy it is, and how grippy its face is.
/// </summary>
/// <remarks>
/// <para>
/// These are simple proxies, not shapes with detail. A space is worth authoring
/// because a line through it can be flown badly, so the geometry only has to be
/// legible and hold still; anything finer belongs to how it is drawn.
/// </para>
/// <para>
/// <see cref="Mass"/> is never zero. The Engine admits no body without positive
/// mass, and a rock is not held in place by its weight:
/// <see cref="ApproachField"/> locks its translation axes, which is how a
/// boulder stays exactly where it was put while answering contacts like one.
/// </para>
/// <para>
/// A round proxy has no meaningful heading and is authored with none; every
/// other shape states the turn it sits at on the chart.
/// </para>
/// <para>
/// <see cref="Friction"/> is how much the face grabs a hull that slides along
/// it. It is authored per obstacle because a spar and a boulder do not feel the
/// same to brush past, and it is the Engine's own surface parameter: the
/// product states it, and the Engine decides what a slide against it costs.
/// </para>
/// </remarks>
internal abstract record ObstacleDefinition(
    ObstacleId Id,
    PlanarVector Position,
    double HeadingRadians,
    double Mass,
    double Friction)
{
    private const double MinimumPositiveMass = 0.0;
    private const double MinimumFriction = 0.0;

    protected ObstacleDefinition ValidatePlacement()
    {
        ArgumentNullException.ThrowIfNull(Id.Value);

        if (!double.IsFinite(Position.X) || !double.IsFinite(Position.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(Position));
        }

        if (!double.IsFinite(HeadingRadians))
        {
            throw new ArgumentOutOfRangeException(nameof(HeadingRadians));
        }

        if (!double.IsFinite(Mass) || Mass <= MinimumPositiveMass)
        {
            throw new ArgumentOutOfRangeException(nameof(Mass));
        }

        if (!double.IsFinite(Friction) || Friction < MinimumFriction)
        {
            throw new ArgumentOutOfRangeException(nameof(Friction));
        }

        return this;
    }
}
