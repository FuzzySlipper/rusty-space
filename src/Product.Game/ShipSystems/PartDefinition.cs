using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// What any installed part contributes just by being fitted: an identity, the
/// behavior it serves, where it sits on the hull, and how heavy it is.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Mount"/> is a local offset from the hull's center of mass in the
/// ship's own frame: <c>+X</c> toward the bow, <c>+Z</c> toward starboard. It is
/// not a position in the world and it is never a second body. The part pushes on
/// the hull through the single body the Engine already simulates, and its mount
/// decides how much of that push becomes a turn.
/// </para>
/// <para>
/// <see cref="Mass"/> is the part's own, added to the hull's by
/// <see cref="InstalledShip"/> and handed to the Engine's mass lane. The parallel
/// term it adds to the ship's turn inertia falls out of where it is mounted, so
/// an aft-heavy fit turns slower than the same mass slung on the centerline.
/// </para>
/// <para>
/// <see cref="Health"/> is the fraction of its rating the part arrives with:
/// <c>1.0</c> for hardware off the line, less for a piece that came off
/// something. It is authored with the fit, and what wear does over time is the
/// health phase's to decide.
/// </para>
/// <para>
/// Every role names the actuator it answers demands through. A part is a machine
/// that takes time to get where it is told, and what its actuator is asked for
/// follows from the role: an emitter answers the coupling trim, a stabilizer
/// answers a heading demand, a drive answers the throttle.
/// </para>
/// </remarks>
internal record PartDefinition(PartId Id, PartRole Role, PlanarVector Mount, double Mass, double Health)
{
    private const double MinimumPositiveMass = 0.0;
    private const double MinimumHealth = 0.0;
    private const double FullHealth = 1.0;

    /// <summary>
    /// The lever arm this part has about the center of mass: its distance from
    /// the keel line, which is what turns a push along the keel into a yaw.
    /// </summary>
    internal double LateralOffset => Math.Abs(Mount.Z);

    /// <summary>
    /// Squared distance from the center of mass, for the parallel-axis term this
    /// part adds to the ship's turn inertia.
    /// </summary>
    internal double DistanceSquaredFromCenter => Mount.MagnitudeSquared;

    protected PartDefinition ValidateIdentity()
    {
        ArgumentNullException.ThrowIfNull(Id.Value);

        if (!double.IsFinite(Mass) || Mass < MinimumPositiveMass)
        {
            throw new ArgumentOutOfRangeException(nameof(Mass));
        }

        if (!double.IsFinite(Mount.X) || !double.IsFinite(Mount.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(Mount));
        }

        if (!double.IsFinite(Health) || Health <= MinimumHealth || Health > FullHealth)
        {
            throw new ArgumentOutOfRangeException(nameof(Health));
        }

        return this;
    }
}
