using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.ShipSystems;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// Product-owned flight meaning around one Engine-owned Dynamics body.
/// </summary>
internal sealed class SpaceFlight : IDisposable
{
    private const bool AxisFree = false;
    private const bool AxisLocked = true;
    private const uint NoSteps = 0;
    private const uint SingleSubstep = 1;
    private const uint FirstSubstep = 0;
    private const ulong SequenceIncrement = 1;
    private const double NeutralCommandIntent = 0.0;
    private const float NoDamping = 0.0f;
    private const float HullFriction = 0.5f;
    private const float NoRestitution = 0.0f;
    private const uint AllCollisionGroups = uint.MaxValue;
    private const bool HullEnabled = true;
    private const bool HullAsleep = false;
    private const bool HullUsesContinuousCollision = false;

    private readonly IDynamicsService dynamics;
    private readonly DynamicsWorld world;
    private readonly FlightController controller;
    private readonly FieldCoupling coupling;
    private readonly FlightTelemetry telemetry = new();
    private readonly StellarField field;
    private readonly HullForceModel forceModel;
    private readonly TrajectoryProjection trajectory;
    private readonly FlightBodyTuning bodyTuning;
    private readonly InstalledShip ship;
    private readonly FlightInputMapper inputMapper = new();
    private DynamicsBody body = null!;
    private FlightCommand command = FlightCommand.Neutral;
    private FlightReadout readout;
    private FlightForces contributions = FlightForces.Zero;
    private FlightForces firstSubstepContributions = FlightForces.Zero;
    private FieldSample lastFieldSample;
    private FlightPath projectedPath = FlightPath.None;
    private ulong fixedStepCount;
    private ulong updateSequence;
    private ulong resetCount;
    private bool disposed;

    internal SpaceFlight(
        IDynamicsService dynamics,
        IKinematicService kinematic,
        FlightTuning flightTuning,
        CouplingTuning couplingTuning,
        FlightBodyTuning bodyTuning,
        ShipLoadout shipLoadout,
        FieldTuning fieldTuning,
        OrbitalGravityTuning orbitalTuning,
        DriftCurrentTuning gentleCurrentTuning,
        DriftCurrentTuning swiftCurrentTuning,
        TrajectoryTuning trajectoryTuning)
    {
        this.dynamics = dynamics ?? throw new ArgumentNullException(nameof(dynamics));
        this.bodyTuning = bodyTuning;
        ship = new InstalledShip(shipLoadout, flightTuning.MaximumThrust);
        controller = new FlightController(flightTuning);
        coupling = new FieldCoupling(couplingTuning);
        field = new StellarField(fieldTuning);
        DriftCurrent gentle = new(gentleCurrentTuning);
        DriftCurrent swift = new(swiftCurrentTuning);
        // The same environment instances the hull is resolved against, handed out
        /// so the navigation view reads exactly what the ship feels rather than a
        /// second copy of the same field.
        Environment = new FlightEnvironment(field, gentle, swift);
        // One rule turns a hull state into push, used both for the substep the
        // Engine integrates and for the line the view draws ahead of the ship.
        forceModel = new HullForceModel(
            new FieldResponse(fieldTuning),
            gentle,
            swift,
            new OrbitalGravity(orbitalTuning));
        trajectory = new TrajectoryProjection(kinematic, forceModel, field, trajectoryTuning);
        world = this.dynamics.CreateWorld(new DynamicsWorldConfig(Vector3.Zero));

        DynamicsBody? initialBody = null;
        try
        {
            initialBody = CreateSpawnBody();
            FitHull(initialBody);
            readout = MapReadout(this.dynamics.Read(new DynamicsReadRequest(initialBody)));
            body = initialBody;
            initialBody = null;
        }
        catch
        {
            initialBody?.Dispose();
            world.Dispose();
            throw;
        }
    }

    internal FlightReadout Readout => readout;

    /// <summary>
    /// The hardware mounted to this hull: what is fitted, where it pushes, and
    /// what its actuators reached on the last turn.
    /// </summary>
    internal InstalledShip Ship => ship;

    /// <summary>
    /// The environment this flight's hull is resolved against, handed to the view
    /// so both read one field.
    /// </summary>
    internal FlightEnvironment Environment { get; }

    internal FlightCommand LastCommand => command;

    /// <summary>
    /// The per-source push of the most recent admitted turn's last substep, kept
    /// intact so the sum is never the only thing the product can report.
    /// </summary>
    internal FlightForces Contributions => contributions;

    /// <summary>
    /// The per-source push of the most recent admitted turn's first substep.
    /// Read against <see cref="Contributions"/> to see how far the sources moved
    /// while a catch-up turn was running.
    /// </summary>
    internal FlightForces FirstSubstepContributions => firstSubstepContributions;

    /// <summary>
    /// How much of the field and of every drift band the hull currently feels,
    /// as the coupling actuator last left it.
    /// </summary>
    internal double Coupling => coupling.Level;

    internal FieldSample LastFieldSample => lastFieldSample;

    internal FlightTelemetrySnapshot Telemetry => telemetry.Current;

    /// <summary>
    /// Where the hull is on its way to from here, sampled forward on the Engine's
    /// kinematic lane. Rebuilt from the ship's real state every admitted turn.
    /// </summary>
    internal FlightPath ProjectedPath => projectedPath;

    internal ulong FixedStepCount => fixedStepCount;

    internal ulong UpdateSequence => updateSequence;

    internal ulong ResetCount => resetCount;

    internal FlightAdmission Admit(ProductUpdate update)
    {
        ThrowIfDisposed();

        FlightTurnInput input = inputMapper.Apply(update.Input);
        if (input.ResetRequested)
        {
            ResetFlight();
            return new FlightAdmission(
                true, fixedStepCount, updateSequence, TimeSpan.Zero, input.FaultRequested);
        }

        // The Engine owns update admission and fixed-step timing; its facts
        // name the admitted steps for this turn. Every admitted step is one
        // fixed step of simulated time, so the product resolves its push and
        // steps once per fixed step rather than once per turn. A catch-up turn
        // therefore re-decides steering, field, drift, and the well against the
        // state each substep actually acts on, instead of applying the pre-turn
        // answer four times. The Engine stays the only integrator.
        uint stepCount = update.Facts.AdmittedStepCount;
        if (stepCount == NoSteps)
        {
            command = input.Command;
            return new FlightAdmission(
                false, fixedStepCount, updateSequence, TimeSpan.Zero, input.FaultRequested);
        }

        // The Engine admitted these steps at its own rate, and that admitted
        // duration is the only clock the turn runs on.
        AdmittedTurn turn = AdmittedTurn.FromFacts(update.Facts);
        ulong nextFixedStepCount = checked(fixedStepCount + stepCount);
        ulong nextUpdateSequence = checked(updateSequence + SequenceIncrement);
        FlightBodyState turnStart = ToBodyState(readout);
        FlightReadout currentReadout = readout;
        FlightForces turnStartForces = FlightForces.Zero;
        FlightForces forces = FlightForces.Zero;
        FlightControlOutput output = default;
        ShipEffort effort = default;
        FieldSample fieldSample = field.Sample(turnStart.Position);
        for (uint stepIndex = 0; stepIndex < stepCount; stepIndex++)
        {
            FlightBodyState bodyState = ToBodyState(currentReadout);
            fieldSample = field.Sample(bodyState.Position);
            // Each actuator owns its own level and moves over the one admitted
            // fixed step this substep integrates, so a turn that catches up four
            // steps has had four steps of travel and no interval is counted
            // twice.
            FlightControlOutput substepOutput = controller.Advance(
                bodyState,
                input.Command,
                currentReadout.YawInertia,
                turn.FixedStep);
            // The coupling actuator travels on the same per-substep clock for
            // the same reason.
            coupling.Advance(input.Command, turn.FixedStep);
            // Every actuator the hull carries answers the demand the controllers
            // resolved for this substep, and what they reached — not what was
            // asked — is what pushes the ship this step.
            ShipEffort substepEffort = ship.Advance(
                substepOutput.Drive.Force,
                substepOutput.Steering.YawTorque,
                coupling.Level,
                fieldSample,
                turn.FixedStep);
            FlightForces substepForces = forceModel.Resolve(
                bodyState,
                ship,
                fieldSample,
                substepEffort,
                currentReadout.Mass);
            if (stepIndex == FirstSubstep)
            {
                turnStartForces = substepForces;
            }

            // Every product-meaning push joins once, per substep, into the
            // single DynamicsAction force for that substep.
            dynamics.Step(new DynamicsStepRequest(
                world,
                turn.DynamicsStepSeconds,
                SingleSubstep,
                new[] { ToDynamicsAction(substepForces.Total) }));
            currentReadout = MapReadout(dynamics.Read(new DynamicsReadRequest(body)));
            output = substepOutput;
            effort = substepEffort;
            forces = substepForces;
        }

        telemetry.Capture(
            turnStart,
            currentReadout,
            forces,
            output,
            effort,
            coupling.Level,
            nextFixedStepCount,
            stepCount,
            turn.FixedStep);
        // The line the navigation view draws is walked from the state this turn
        // actually left the ship in, with the hardware's last reached effort held,
        // so what the player reads ahead is the same rule that moved the hull.
        projectedPath = trajectory.Project(
            ToBodyState(currentReadout),
            ship,
            effort,
            currentReadout.Mass,
            turn.FixedStep);
        contributions = forces;
        firstSubstepContributions = turnStartForces;
        lastFieldSample = fieldSample;
        command = input.Command;
        readout = currentReadout;
        fixedStepCount = nextFixedStepCount;
        updateSequence = nextUpdateSequence;
        return new FlightAdmission(
            true, fixedStepCount, updateSequence, turn.Duration, input.FaultRequested);
    }

    internal void ResetFlight()
    {
        ThrowIfDisposed();
        ulong nextUpdateSequence = checked(updateSequence + SequenceIncrement);
        ulong nextResetCount = checked(resetCount + SequenceIncrement);
        DynamicsBody? candidate = null;
        try
        {
            candidate = CreateSpawnBody();
            // A reset builds a fresh hull, and a fresh hull is bare until the
            // fitted hardware is put on it: the same fit the spawn applies, or
            // the ship silently loses its mounted weight every time the player
            // puts it back on the line.
            FitHull(candidate);
            FlightReadout candidateReadout = MapReadout(
                dynamics.Read(new DynamicsReadRequest(candidate)));
            DynamicsBody previous = body;
            body = candidate;
            candidate = null;
            readout = candidateReadout;
            command = FlightCommand.Neutral;
            contributions = FlightForces.Zero;
            firstSubstepContributions = FlightForces.Zero;
            lastFieldSample = field.Sample(readout.Position);
            projectedPath = FlightPath.None;
            controller.Reset();
            coupling.Reset();
            ship.Reset();
            telemetry.Reset();
            inputMapper.Reset();
            updateSequence = nextUpdateSequence;
            resetCount = nextResetCount;
            previous.Dispose();
        }
        finally
        {
            candidate?.Dispose();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            body.Dispose();
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Resolves every environmental source against the body state a single
    /// substep is about to act on, and says where on the hull each of them acts.
    /// Control push comes from the hardware for that same substep, so no source
    /// is ever evaluated against a state from an earlier one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A push applied away from the center of mass turns the hull as well as
    /// driving it, and which center each source acts at is what gives a fit its
    /// character. Flow-coupled push arrives at the emitter's mount; main thrust
    /// at the drive's. The orbital well is the one source with no lever, because
    /// gravity pulls on the hull's mass where that mass is: at the center of mass
    /// itself.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The same push, with the turn it causes because it lands away from the
    /// center of mass.
    /// </summary>
    private static FlightWrench AtPoint(PlanarVector force, PlanarVector offsetFromCenter) =>
        new(force, PlanarFrame.YawTorque(offsetFromCenter, force));    /// <summary>
    /// Hands the Engine the mass and turn inertia the fitted hardware adds to the
    /// hull, through the body-update lane and with authored mass properties.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A hull is created with mass derived from its shape, so the fit is applied
    /// against what the Engine reports for it: the read supplies the hull's own
    /// inertia and mass, and every mounted part adds its share — its mass to the
    /// total, and that mass times the square of its distance from the center for
    /// the turn. That is what makes an outboard fit sluggish to yaw in a way a
    /// bare hull of the same weight is not.
    /// </para>
    /// <para>
    /// The authored center of mass is the hull's own origin. Mount offsets are
    /// measured from there and every turn they cause is already counted where the
    /// force is resolved, so moving the simulated center as well would bill the
    /// same leverage twice.
    /// </para>
    /// <para>
    /// The Engine's update replaces the whole property set rather than merging
    /// into it, so the update restates the hull's locks, its damping, its
    /// collision filtering, and the velocities it was read with. That is why the
    /// fit is applied here, against a body that has just been created, rather
    /// than at some later point: issued mid-flight it would carry the velocities
    /// of a read that is already a step behind the ship.
    /// </para>
    /// </remarks>
    private void FitHull(DynamicsBody hull)
    {
        DynamicsReadout current = dynamics.Read(new DynamicsReadRequest(hull));
        dynamics.UpdateBody(new DynamicsUpdateBodyRequest(hull, FittedProperties(current)));
    }

    private DynamicsBodyProperties FittedProperties(DynamicsReadout hull) => new(
        ToSingle(checked(hull.MassProperties.Mass + ship.AddedMass)),
        new DynamicsMassPolicy(
            DynamicsMassPolicyKind.Explicit,
            new DynamicsExplicitMassProperties(
                Vector3.Zero,
                new Vector3(
                    ToSingle(hull.MassProperties.PrincipalInertia.X),
                    ToSingle(hull.MassProperties.PrincipalInertia.Y + ship.AddedYawInertia),
                    ToSingle(hull.MassProperties.PrincipalInertia.Z)),
                Quaternion.Identity)),
        hull.LinearVelocity,
        hull.AngularVelocity,
        new AxisLocks(
            TranslationX: AxisFree,
            TranslationY: AxisLocked,
            TranslationZ: AxisFree,
            RotationX: AxisLocked,
            RotationY: AxisFree,
            RotationZ: AxisLocked),
        LinearDamping: NoDamping,
        AngularDamping: NoDamping,
        GravityScale: ToSingle(NeutralCommandIntent),
        Friction: HullFriction,
        Restitution: NoRestitution,
        CollisionGroups: AllCollisionGroups,
        CollisionMask: AllCollisionGroups,
        Enabled: HullEnabled,
        Sleeping: HullAsleep,
        ContinuousCollision: HullUsesContinuousCollision);

    private DynamicsBody CreateSpawnBody() => dynamics.CreateBody(new DynamicsCreateBodyRequest(
        world,
        new DynamicsBodyConfig(
            new Transform(
                new Vector3(
                    ToSingle(bodyTuning.SpawnPosition.X),
                    ToSingle(bodyTuning.SpawnHeight),
                    ToSingle(bodyTuning.SpawnPosition.Z)),
                PlanarFrame.ToEngineAttitude(bodyTuning.SpawnHeadingRadians),
                Vector3.One),
            new Vector3(
                ToSingle(bodyTuning.HalfExtents.X),
                ToSingle(bodyTuning.HalfHeight),
                ToSingle(bodyTuning.HalfExtents.Z)),
            ToSingle(bodyTuning.Mass),
            new DynamicsMassPolicy(
                DynamicsMassPolicyKind.DeriveFromShapeAndMass,
                default),
            new AxisLocks(
                TranslationX: AxisFree,
                TranslationY: AxisLocked,
                TranslationZ: AxisFree,
                RotationX: AxisLocked,
                RotationY: AxisFree,
                RotationZ: AxisLocked),
            GravityScale: ToSingle(NeutralCommandIntent))));

    private DynamicsAction ToDynamicsAction(FlightWrench wrench) => new(
        body,
        new Vector3(ToSingle(wrench.Force.X), ToSingle(NeutralCommandIntent), ToSingle(wrench.Force.Z)),
        new Vector3(
            ToSingle(NeutralCommandIntent),
            ToSingle(PlanarFrame.EngineYaw(wrench.YawTorque)),
            ToSingle(NeutralCommandIntent)),
        Vector3.Zero,
        Vector3.Zero,
        Wake: true);

    private static FlightBodyState ToBodyState(FlightReadout value) => new(
        value.Position,
        value.HeadingRadians,
        value.LinearVelocity,
        value.AngularVelocity);

    private static FlightReadout MapReadout(DynamicsReadout native) => new(
        new PlanarVector(native.Transform.Translation.X, native.Transform.Translation.Z),
        PlanarFrame.HeadingOf(native.Transform.Rotation),
        new PlanarVector(native.LinearVelocity.X, native.LinearVelocity.Z),
        PlanarFrame.HeadingRateOf(native.AngularVelocity.Y),
        native.MassProperties.Mass,
        native.MassProperties.PrincipalInertia.Y);

    private static float ToSingle(double value) => checked((float)value);

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(SpaceFlight));
        }
    }
}

/// <summary>
/// What one admitted turn did, including the simulated time it covered, so
/// what happens after a turn measures the same interval the Engine admitted
/// rather than a count restated at a product constant.
/// </summary>
internal readonly record struct FlightAdmission(
    bool Published,
    ulong FixedStepCount,
    ulong UpdateSequence,
    TimeSpan TurnDuration,
    bool FaultRequested);
