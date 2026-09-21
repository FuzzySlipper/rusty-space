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
    FlightWrench OrbitalPull)
{
    internal static FlightForces Zero { get; } = new(
        FlightWrench.Zero,
        FlightWrench.Zero,
        FlightWrench.Zero,
        FlightWrench.Zero,
        FlightWrench.Zero,
        FlightWrench.Zero);

    /// <summary>
    /// The single join of every source. What the effector pair's wear pulls for
    /// itself, and what a vane jammed off centre holds the hull against, are
    /// already inside <see cref="Steering"/>: they arrive as work those actuators
    /// reached. A source with an owner that puts it on the hull has no business in
    /// this sum a second time, and the second copy would reach the hull without the
    /// response, damping, and stop the hardware that produced it is supposed to
    /// pass it through.
    /// </summary>
    internal FlightWrench Total => MainDrive
        + Steering
        + Field
        + GentleCurrent
        + SwiftCurrent
        + OrbitalPull;
}
