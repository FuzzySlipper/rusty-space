using Rusty.Space.Product.ShipSystems;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// One contact with the world, whole: what the Engine's contacts pushed the hull
/// with, and what the hardware fitted to it made of that.
/// </summary>
/// <remarks>
/// The two halves are kept together because a report that names one without the
/// other is only half an explanation. <see cref="Impact"/> is the Engine's fact
/// about a body — the push, the size of it, and the authored obstacle on the far
/// end. <see cref="Damage"/> is this product's answer: which mount took it, what
/// it cost that part, and whether anything is now held where it was not told to
/// be. Nothing was struck means nothing was answered, so the answer is absent
/// rather than invented.
/// </remarks>
internal readonly record struct HullStrike(HullImpact Impact, HullDamage? Damage);
