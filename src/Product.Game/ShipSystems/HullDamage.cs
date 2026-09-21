using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// What one contact did to the hardware it landed on: which part took it, which
/// side of the hull it found, what the hit cost in health, and whether anything
/// is now held away from where it was told to be.
/// </summary>
/// <remarks>
/// <see cref="StruckSide"/> is a unit direction in the hull's own frame, pointing
/// at the side that was hit — the side a report and a marker both want, since
/// the push itself points back out of the hull and away from what caused it.
/// </remarks>
internal readonly record struct HullDamage(
    PartId Part,
    PlanarVector StruckSide,
    double HealthLost,
    bool KnockedOutOfTrim);
