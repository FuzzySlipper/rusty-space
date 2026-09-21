using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Approach;

/// <summary>
/// A rounded mass of rock: the kind of thing an approach chart marks with a dot
/// and a radius, because what matters about it is that it is everywhere inside
/// that radius and nowhere outside it.
/// </summary>
/// <remarks>
/// A boulder is handed to the Engine as a sphere, so a hull that clips one is
/// deflected along the face it touched rather than caught on a corner. That is
/// the whole reason to prefer a round proxy where the play is a brush past. A
/// round proxy has no meaningful turn, so it carries no heading of its own.
/// </remarks>
internal sealed record Boulder(
    ObstacleId Id,
    PlanarVector Position,
    double Mass,
    double Friction,
    double Radius)
    : ObstacleDefinition(Id, Position, 0.0, Mass, Friction)
{
    private const double MinimumRadius = 0.0;

    internal Boulder Validate()
    {
        ValidatePlacement();

        if (!double.IsFinite(Radius) || Radius <= MinimumRadius)
        {
            throw new ArgumentOutOfRangeException(nameof(Radius));
        }

        return this;
    }
}
