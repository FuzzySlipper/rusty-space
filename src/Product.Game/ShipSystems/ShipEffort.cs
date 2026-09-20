using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// What the installed hardware actually delivered for one admitted fixed step,
/// as opposed to what the controller asked it for.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DriveForce"/> is the thrust the drive reached, in the plane's world
/// axes, and <see cref="HeadingTorque"/> is the yaw the effector pair reached, in
/// the heading-positive sense. Both are the actuator's own numbers: a demand that
/// outran the hardware shows up here as a shortfall rather than as a ship that
/// quietly ignores its controller.
/// </para>
/// <para>
/// <see cref="WearPull"/> is the part of that yaw that came from worn hardware
/// rather than from any demand — the standing pull a tired side puts on the bow
/// while it is loaded. It is reported separately so a player's instruments can
/// say which of the two the ship is fighting.
/// </para>
/// <para>
/// <see cref="Coupling"/> is the coupling the hull actually feels: the actuator's
/// level after the fitted emitter's gain and its own travel.
/// <see cref="HeadingEffort"/> is how close the busiest side of the effector pair
/// came to its rating, <see cref="HeadingSaturated"/> whether either of them
/// reached the stop, and <see cref="HeadingAsymmetry"/> how much the two sides
/// disagreed: <c>0</c> when both delivered the same turn, positive when the port
/// side did more of the work.
/// </para>
/// </remarks>
internal readonly record struct ShipEffort(
    PlanarVector DriveForce,
    double HeadingTorque,
    double WearPull,
    double Coupling,
    double HeadingEffort,
    bool HeadingSaturated,
    double HeadingAsymmetry);
