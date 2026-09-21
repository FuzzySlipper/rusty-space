using Rusty.Space.Product.Field;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// The environment owners a flight resolves its ship against: the authored
/// stellar field, and the two drift bands.
/// </summary>
/// <remarks>
/// A grouping of read access, not an owner of anything: the flight resolves
/// against these, and the navigation view reads the same instances so what the
/// player sees and what the hull feels are the same environment. There is no
/// second copy of the field anywhere in the product.
/// </remarks>
internal readonly record struct FlightEnvironment(
    StellarField Field,
    DriftCurrent GentleCurrent,
    DriftCurrent SwiftCurrent);
