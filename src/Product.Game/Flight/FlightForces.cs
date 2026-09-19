namespace Rusty.Space.Product.Flight;

/// <summary>
/// One admitted turn's flight push, separated by the source that produced it.
/// Each owner names its own contribution; they are joined only where the single
/// <see cref="DynamicsAction"/> is built, so the product can always say which
/// source moved the ship.
/// </summary>
internal readonly record struct FlightForces(
    FlightWrench MainDrive,
    FlightWrench Steering,
    FlightWrench Field,
    FlightWrench GentleCurrent,
    FlightWrench SwiftCurrent,
    FlightWrench OrbitalPull,
    FlightWrench DamageBias)
{
    internal static FlightForces Zero { get; } = new(
        FlightWrench.Zero,
        FlightWrench.Zero,
        FlightWrench.Zero,
        FlightWrench.Zero,
        FlightWrench.Zero,
        FlightWrench.Zero,
        FlightWrench.Zero);

    /// <summary>
    /// The single join of every source. Damage bias is a named channel that no
    /// owner fills yet; it exists so a fault can add push without a second sum.
    /// </summary>
    internal FlightWrench Total => MainDrive
        + Steering
        + Field
        + GentleCurrent
        + SwiftCurrent
        + OrbitalPull
        + DamageBias;
}
