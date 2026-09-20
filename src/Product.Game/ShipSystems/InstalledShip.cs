using Rusty.Space.Product.Field;
using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// The hull's installed hardware: what is fitted, where each piece pushes, how
/// heavy the fit makes the ship, and what each actuator actually managed.
/// </summary>
/// <remarks>
/// <para>
/// The ship is one body and stays one body. What the Engine integrates is a hull;
/// what this owner holds is the machinery that decides how that hull responds to
/// being pushed: an emitter that turns flow into force at the point it is
/// mounted, a drive that puts thrust on the keel where it hangs, and a pair of
/// heading effectors on opposite sides of it. Each is a logical effector with a
/// mount and an actuator, never a second body, never a joint, and never a second
/// authority over where the ship goes.
/// </para>
/// <para>
/// This is where the ship's several centers live. A force applied away from the
/// center of mass turns the ship as well as pushing it, and which center each
/// source acts at is what gives one fit its character and another its problems:
/// an emitter hung forward makes the bow swing into the flow, a drive hung off
/// the keel makes the throttle a steering input, and a heading effector pair with
/// one tired side cannot pull evenly in both directions.
/// </para>
/// <para>
/// The owner never integrates the ship and never issues an Engine action. It
/// answers one question per admitted fixed step — what the hardware delivered —
/// and reports what the fit weighs, and its owner turns that into the single
/// action the Engine admits.
/// </para>
/// </remarks>
internal sealed class InstalledShip
{
    // The emitter's actuator gates a coupling demand that already runs from zero
    // to one, so its stop is a fully open coupling.
    private const double FullCouplingGate = 1.0;
    private const double MinimumLoad = 0.0;
    private const double FullLoad = 1.0;
    private const double NoDemand = 0.0;
    private const double HalfPair = 2.0;
    private const double NoTurnImbalance = 0.0;

    private readonly FieldEmitterDefinition emitterDefinition;
    private readonly StabilizerDefinition portDefinition;
    private readonly StabilizerDefinition starboardDefinition;
    private readonly InstalledPart emitter;
    private readonly InstalledPart drive;
    private readonly InstalledPart portStabilizer;
    private readonly InstalledPart starboardStabilizer;
    private readonly PlanarVector steeringAuthorityCenterLocal;
    private readonly PlanarVector stabilizationCenterLocal;
    private readonly double driveAuthority;

    internal InstalledShip(ShipLoadout loadout, double driveAuthority)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        if (!double.IsFinite(driveAuthority) || driveAuthority <= NoDemand)
        {
            throw new ArgumentOutOfRangeException(nameof(driveAuthority));
        }

        this.driveAuthority = driveAuthority;
        LoadoutName = loadout.Name;
        emitterDefinition = loadout.Emitter;
        portDefinition = loadout.PortStabilizer;
        starboardDefinition = loadout.StarboardStabilizer;

        emitter = new InstalledPart(
            loadout.Emitter,
            new ActuatorResponse(loadout.Emitter.Response, FullCouplingGate));
        drive = new InstalledPart(
            loadout.Drive,
            new ActuatorResponse(loadout.Drive.Response, driveAuthority));
        portStabilizer = new InstalledPart(
            loadout.PortStabilizer,
            new ActuatorResponse(loadout.PortStabilizer.Response, portDefinition.DeliveredAuthority));
        starboardStabilizer = new InstalledPart(
            loadout.StarboardStabilizer,
            new ActuatorResponse(
                loadout.StarboardStabilizer.Response,
                starboardDefinition.DeliveredAuthority));

        steeringAuthorityCenterLocal = (loadout.PortStabilizer.Mount + loadout.StarboardStabilizer.Mount)
            .Scale(1.0 / HalfPair);
        stabilizationCenterLocal = StabilizationCenterLocal(portDefinition, starboardDefinition);

        AddedMass = loadout.Emitter.Mass
            + loadout.Drive.Mass
            + loadout.PortStabilizer.Mass
            + loadout.StarboardStabilizer.Mass;
        AddedYawInertia = (loadout.Emitter.Mass * loadout.Emitter.DistanceSquaredFromCenter)
            + (loadout.Drive.Mass * loadout.Drive.DistanceSquaredFromCenter)
            + (loadout.PortStabilizer.Mass * loadout.PortStabilizer.DistanceSquaredFromCenter)
            + (loadout.StarboardStabilizer.Mass * loadout.StarboardStabilizer.DistanceSquaredFromCenter);
    }

    /// <summary>Which authored fit this hull is flying.</summary>
    internal string LoadoutName { get; }

    /// <summary>
    /// The hull's mass plus everything mounted to it. The Engine is handed this,
    /// not the bare hull's, so the ship accelerates at the weight it carries.
    /// </summary>
    internal double AddedMass { get; }

    /// <summary>
    /// The turn inertia the fit adds to the hull's own: every mounted mass at the
    /// square of its distance from the center of mass, which is why an outboard
    /// fit turns slower than the same weight on the centerline.
    /// </summary>
    internal double AddedYawInertia { get; }

    internal InstalledPart Emitter => emitter;

    internal InstalledPart MainDrive => drive;

    internal InstalledPart PortStabilizer => portStabilizer;

    internal InstalledPart StarboardStabilizer => starboardStabilizer;

    /// <summary>
    /// Where the main drive puts thrust on the keel, in the plane's world axes,
    /// measured from the center of mass the hull is simulated about.
    /// </summary>
    internal PlanarVector MainThrustCenter(double headingRadians) =>
        PlanarFrame.Rotate(drive.Definition.Mount, headingRadians);

    /// <summary>
    /// Where the field and every drift band actually push on the hull: the
    /// emitter's mount. Fitted forward of the center of mass, the same flow that
    /// drives the ship also swings the bow around to face it.
    /// </summary>
    internal PlanarVector FieldCouplingCenter(double headingRadians) =>
        PlanarFrame.Rotate(emitterDefinition.Mount, headingRadians);

    /// <summary>
    /// The point between the two heading effectors their combined yaw is credited
    /// to, for anything that wants to name where the ship's steering authority
    /// sits.
    /// </summary>
    internal PlanarVector SteeringAuthorityCenter(double headingRadians) =>
        PlanarFrame.Rotate(steeringAuthorityCenterLocal, headingRadians);

    /// <summary>
    /// Where the attitude hold is actually acting. A matched pair acts on the
    /// centerline; a pair with one side unable to deliver holds the ship from
    /// wherever the surviving hardware is.
    /// </summary>
    internal PlanarVector StabilizationCenter(double headingRadians) =>
        PlanarFrame.Rotate(stabilizationCenterLocal, headingRadians);

    /// <summary>
    /// Travels every actuator over one admitted fixed step and reports what the
    /// hardware reached. The demands are the controller's and the coupling
    /// actuator's; the returned values are the ship's, and they are what reaches
    /// the Engine.
    /// </summary>
    internal ShipEffort Advance(
        PlanarVector demandedDrive,
        double demandedHeadingTorque,
        double commandedCoupling,
        FieldSample field,
        TimeSpan step)
    {
        double load = Load(commandedCoupling, field);

        // The emitter answers the coupling trim with its own hardware: a gain
        // that decides how much of the flow a given setting actually catches,
        // and an actuator that decides how fast it gets there.
        double gate = emitter.Advance(commandedCoupling, commandedCoupling, step);
        double coupling = gate * emitterDefinition.CouplingGain;

        // The drive answers the throttle spool. It is handed the demand as the
        // controller resolved it so a push trimmed off at maximum speed keeps
        // that direction, and it returns how much of it the hardware reached.
        double demandedDriveMagnitude = demandedDrive.Magnitude;
        double deliveredDriveMagnitude = drive.Advance(
            demandedDriveMagnitude,
            demandedDriveMagnitude / driveAuthority,
            step);
        PlanarVector driveForce = demandedDriveMagnitude <= NoDemand
            ? PlanarVector.Zero
            : demandedDrive.Scale(deliveredDriveMagnitude / demandedDriveMagnitude);

        // The heading effector pair shares a yaw demand in proportion to the
        // leverage each side has, so a mirror-mounted pair pulls evenly on both
        // sides of the keel. A side that cannot deliver its share shortfalls the
        // turn; a worn side adds its own pull on top of whatever was asked.
        double portArm = portDefinition.LateralOffset;
        double starboardArm = starboardDefinition.LateralOffset;
        double armsSquared = (portArm * portArm) + (starboardArm * starboardArm);
        double portDemand = (demandedHeadingTorque * portArm / armsSquared)
            + (portDefinition.PullAt(load) / portArm);
        double starboardDemand = (demandedHeadingTorque * starboardArm / armsSquared)
            + (starboardDefinition.PullAt(load) / starboardArm);
        double portDelivered = portStabilizer.Advance(
            portDemand,
            portDefinition.DampingRatioAt(load),
            portDemand / portDefinition.DeliveredAuthority,
            step);
        double starboardDelivered = starboardStabilizer.Advance(
            starboardDemand,
            starboardDefinition.DampingRatioAt(load),
            starboardDemand / starboardDefinition.DeliveredAuthority,
            step);

        double portWork = portArm * portDelivered;
        double starboardWork = starboardArm * starboardDelivered;
        double work = Math.Abs(portWork) + Math.Abs(starboardWork);

        return new ShipEffort(
            DriveForce: driveForce,
            HeadingTorque: portWork + starboardWork,
            WearPull: portDefinition.PullAt(load) + starboardDefinition.PullAt(load),
            Coupling: coupling,
            HeadingEffort: Math.Max(
                Math.Abs(portDemand) / portDefinition.DeliveredAuthority,
                Math.Abs(starboardDemand) / starboardDefinition.DeliveredAuthority),
            HeadingSaturated: portStabilizer.Saturated || starboardStabilizer.Saturated,
            HeadingAsymmetry: work <= NoDemand
                ? NoTurnImbalance
                : (portWork - starboardWork) / work);
    }

    internal void Reset()
    {
        emitter.Reset();
        drive.Reset();
        portStabilizer.Reset();
        starboardStabilizer.Reset();
    }

    /// <summary>
    /// How hard the hull is working its emitter: the coupling it is trimmed to,
    /// scaled by how much flow there is there to catch. Load is what worn
    /// hardware responds to, so it is deliberately the environment's doing
    /// rather than anything random.
    /// </summary>
    private static double Load(double commandedCoupling, FieldSample field) =>
        Math.Clamp(commandedCoupling * field.Intensity, MinimumLoad, FullLoad);

    private static PlanarVector StabilizationCenterLocal(
        StabilizerDefinition port,
        StabilizerDefinition starboard)
    {
        double portAuthority = port.DeliveredAuthority;
        double starboardAuthority = starboard.DeliveredAuthority;
        return (port.Mount.Scale(portAuthority) + starboard.Mount.Scale(starboardAuthority))
            .Scale(1.0 / (portAuthority + starboardAuthority));
    }
}
