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
/// <para>
/// Being struck is the same arrangement from the other end. Whether the hull
/// touched anything, what it touched, and how hard are the Engine's facts about a
/// body. What that does to a mount, and what the mount has to live with, is
/// decided here — which is the only way an impact can reach the flight of a hull
/// that stays one body.
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
    private readonly InstalledPart[] parts;
    private readonly PlanarVector steeringAuthorityCenterLocal;
    private readonly double driveAuthority;

    internal InstalledShip(ShipLoadout loadout, double driveAuthority, DamageTuning damage)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(damage);
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
            new ActuatorResponse(loadout.Emitter.Response, FullCouplingGate),
            damage);
        drive = new InstalledPart(
            loadout.Drive,
            new ActuatorResponse(loadout.Drive.Response, driveAuthority),
            damage);
        portStabilizer = new InstalledPart(
            loadout.PortStabilizer,
            new ActuatorResponse(loadout.PortStabilizer.Response, portDefinition.DeliveredAuthority),
            damage,
            trimOffsetWhenLatched: JammedVane(portDefinition, damage));
        starboardStabilizer = new InstalledPart(
            loadout.StarboardStabilizer,
            new ActuatorResponse(
                loadout.StarboardStabilizer.Response,
                starboardDefinition.DeliveredAuthority),
            damage,
            trimOffsetWhenLatched: JammedVane(starboardDefinition, damage));
        parts = [emitter, drive, portStabilizer, starboardStabilizer];

        steeringAuthorityCenterLocal = (loadout.PortStabilizer.Mount + loadout.StarboardStabilizer.Mount)
            .Scale(1.0 / HalfPair);

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
    /// The fitted hardware answering to an identity, or nothing for an identity
    /// this hull does not carry. A contact names the part it landed on by id, and
    /// anything that wants to say where on the hull that is asks here.
    /// </summary>
    internal InstalledPart? PartWithId(PartId id)
    {
        foreach (InstalledPart part in parts)
        {
            if (part.Id == id)
            {
                return part;
            }
        }

        return null;
    }

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
        PlanarFrame.Rotate(StabilizationCenterLocal(), headingRadians);

    /// <summary>
    /// What the port side of the effector pair can put on the keel right now:
    /// its rating as much of it as its health leaves standing.
    /// </summary>
    internal double PortAuthority => portDefinition.DeliveredAuthority * portStabilizer.DeliveryFraction;

    /// <summary>The starboard side's, for the same reason.</summary>
    internal double StarboardAuthority => starboardDefinition.DeliveredAuthority * starboardStabilizer.DeliveryFraction;

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
        // What the gate holds is one thing and what is left inside it to catch
        // with is another: a dented emitter closes as far as it is told and
        // catches a fraction of what an unmarked one would.
        double coupling = gate * emitterDefinition.CouplingGain * emitter.DeliveryFraction;

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
            : demandedDrive.Scale(
                deliveredDriveMagnitude * drive.DeliveryFraction / demandedDriveMagnitude);

        // The heading effector pair shares a yaw demand in proportion to the
        // leverage each side has, so a mirror-mounted pair pulls evenly on both
        // sides of the keel. A side that cannot deliver its share shortfalls the
        // turn; a worn side adds its own pull on top of whatever was asked, and a
        // side knocked out of trim by a contact adds its jammed vane on top of
        // both, which is what makes a hit something the player has to fly against
        // rather than a number reported somewhere else.
        double portArm = portDefinition.LateralOffset;
        double starboardArm = starboardDefinition.LateralOffset;
        double armsSquared = (portArm * portArm) + (starboardArm * starboardArm);
        double portDemand = (demandedHeadingTorque * portArm / armsSquared)
            + (portDefinition.PullAt(load) / portArm)
            + portStabilizer.TrimOffset;
        double starboardDemand = (demandedHeadingTorque * starboardArm / armsSquared)
            + (starboardDefinition.PullAt(load) / starboardArm)
            + starboardStabilizer.TrimOffset;
        double portDelivered = portStabilizer.Advance(
            portDemand,
            portDefinition.DampingRatioAt(load),
            portDemand / PortAuthority,
            step);
        double starboardDelivered = starboardStabilizer.Advance(
            starboardDemand,
            starboardDefinition.DampingRatioAt(load),
            starboardDemand / StarboardAuthority,
            step);

        double portWork = portArm * portDelivered * portStabilizer.DeliveryFraction;
        double starboardWork = starboardArm * starboardDelivered * starboardStabilizer.DeliveryFraction;
        double work = Math.Abs(portWork) + Math.Abs(starboardWork);

        return new ShipEffort(
            DriveForce: driveForce,
            HeadingTorque: portWork + starboardWork,
            WearPull: portDefinition.PullAt(load) + starboardDefinition.PullAt(load),
            Coupling: coupling,
            HeadingEffort: Math.Max(
                Math.Abs(portDemand) / PortAuthority,
                Math.Abs(starboardDemand) / StarboardAuthority),
            HeadingSaturated: portStabilizer.Saturated || starboardStabilizer.Saturated,
            HeadingAsymmetry: work <= NoDemand
                ? NoTurnImbalance
                : (portWork - starboardWork) / work);
    }

    /// <summary>
    /// Takes a contact the Engine resolved against the hull and turns it into
    /// something the hardware remembers. The impulse arrives in the hull's own
    /// frame, and which part takes it is decided by which mount the struck side
    /// exposes most: a bow-first arrival goes to whatever is hung forward, and a
    /// starboard quarter arrives at the starboard effector.
    /// </summary>
    /// <remarks>
    /// This owner never decides whether two things have touched, where they
    /// touched, or how hard. All of that is the Engine's answer about a body, and
    /// it arrives here already resolved. What is added is product meaning: which
    /// of the fitted parts is the one that has to live with it, and what living
    /// with it costs.
    /// </remarks>
    internal HullDamage TakeImpact(PlanarVector localImpulse, double impulse)
    {
        PlanarVector struckSide = StruckSideOf(localImpulse);
        InstalledPart stricken = MostExposedTo(struckSide);
        double healthLost = stricken.TakeImpact(impulse);
        return new HullDamage(stricken.Id, struckSide, healthLost, stricken.OutOfTrim);
    }

    /// <summary>
    /// Holds a patch on the hardware for one admitted fixed step. The crew works
    /// on whatever is latched rather than being told which piece to start with,
    /// and a patch let go of before the latch releases is abandoned.
    /// </summary>
    internal void AdvanceRepairs(bool patchHeld, TimeSpan step)
    {
        foreach (InstalledPart part in parts)
        {
            part.AdvanceRepair(patchHeld, step);
        }
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

    /// <summary>
    /// The point the pair's surviving authority actually acts between, weighted by
    /// what each side can put on the keel today rather than by what it was rated
    /// for when it was fitted. A matched pair holds the ship from the centerline;
    /// a pair with one side dented holds it from wherever the strong side is,
    /// which is a different place to be pushed from.
    /// </summary>
    private PlanarVector StabilizationCenterLocal()
    {
        double portAuthority = PortAuthority;
        double starboardAuthority = StarboardAuthority;
        return (portDefinition.Mount.Scale(portAuthority)
            + starboardDefinition.Mount.Scale(starboardAuthority))
            .Scale(1.0 / (portAuthority + starboardAuthority));
    }

    /// <summary>
    /// How far a side's vane sits off center once it is jammed: a fraction of what
    /// that side can deliver, toward whichever side of the keel it is mounted on,
    /// so mirrored hardware that fails on opposite sides pulls opposite ways.
    /// </summary>
    private static double JammedVane(StabilizerDefinition side, DamageTuning damage) =>
        Math.Sign(side.Mount.Z) * side.DeliveredAuthority * damage.TrimOffsetFraction;

    /// <summary>
    /// Which side of the hull a contact came in on. The Engine's impulse is the
    /// push it gave the hull, which points out of the hull and away from whatever
    /// produced it, so the struck side lies the other way. A contact that pushed
    /// with nothing — a hull come to rest against a face, feeling it without being
    /// moved by it — has no side to name and is reported from the bow.
    /// </summary>
    private static PlanarVector StruckSideOf(PlanarVector localImpulse)
    {
        double reach = localImpulse.Magnitude;
        return reach <= NoDemand
            ? PlanarVector.UnitX
            : localImpulse.Scale(-1.0 / reach);
    }

    /// <summary>
    /// The fitted part the struck side exposes most: the one whose mount faces
    /// furthest along it. Mounts are measured from the center of mass, so which
    /// of them is most in the way of a contact arriving from a given direction is
    /// a matter of which way they face rather than how far out they sit.
    /// </summary>
    private InstalledPart MostExposedTo(PlanarVector struckSide)
    {
        InstalledPart exposed = parts[0];
        double bestFacing = double.NegativeInfinity;
        foreach (InstalledPart part in parts)
        {
            double facing = FacingOf(part.Definition.Mount, struckSide);
            if (facing > bestFacing)
            {
                bestFacing = facing;
                exposed = part;
            }
        }

        return exposed;
    }

    private static double FacingOf(PlanarVector mount, PlanarVector struckSide)
    {
        double reach = mount.Magnitude;
        return reach <= NoDemand ? NoDemand : mount.Scale(1.0 / reach).Dot(struckSide);
    }
}
