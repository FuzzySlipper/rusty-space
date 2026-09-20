using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Navigation;

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

    private readonly IDynamicsService dynamics;
    private readonly DynamicsWorld world;
    private readonly FlightController controller;
    private readonly FieldCoupling coupling;
    private readonly FlightTelemetry telemetry = new();
    private readonly StellarField field;
    private readonly FieldResponse fieldResponse;
    private readonly OrbitalGravity gravity;
    private readonly DriftCurrent gentleCurrent;
    private readonly DriftCurrent swiftCurrent;
    private readonly FlightBodyTuning bodyTuning;
    private readonly FlightInputMapper inputMapper = new();
    private DynamicsBody body = null!;
    private FlightCommand command = FlightCommand.Neutral;
    private FlightReadout readout;
    private FlightForces contributions = FlightForces.Zero;
    private FlightForces firstSubstepContributions = FlightForces.Zero;
    private FieldSample lastFieldSample;
    private ulong fixedStepCount;
    private ulong updateSequence;
    private ulong resetCount;
    private bool disposed;

    internal SpaceFlight(
        IDynamicsService dynamics,
        FlightTuning flightTuning,
        CouplingTuning couplingTuning,
        FlightBodyTuning bodyTuning,
        FieldTuning fieldTuning,
        OrbitalGravityTuning orbitalTuning,
        DriftCurrentTuning gentleCurrentTuning,
        DriftCurrentTuning swiftCurrentTuning)
    {
        this.dynamics = dynamics ?? throw new ArgumentNullException(nameof(dynamics));
        this.bodyTuning = bodyTuning;
        controller = new FlightController(flightTuning);
        coupling = new FieldCoupling(couplingTuning);
        field = new StellarField(fieldTuning);
        fieldResponse = new FieldResponse(fieldTuning);
        gravity = new OrbitalGravity(orbitalTuning);
        gentleCurrent = new DriftCurrent(gentleCurrentTuning);
        swiftCurrent = new DriftCurrent(swiftCurrentTuning);
        world = this.dynamics.CreateWorld(new DynamicsWorldConfig(Vector3.Zero));

        DynamicsBody? initialBody = null;
        try
        {
            initialBody = CreateSpawnBody();
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
            FlightForces substepForces = ResolveForces(
                bodyState,
                fieldSample,
                substepOutput,
                currentReadout.Mass,
                coupling.Level);
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
            forces = substepForces;
        }

        telemetry.Capture(
            turnStart,
            currentReadout,
            forces,
            output,
            coupling.Level,
            nextFixedStepCount,
            stepCount,
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
            controller.Reset();
            coupling.Reset();
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
    /// substep is about to act on. Control push comes from the controller output
    /// for that same substep, so no source is ever evaluated against a state
    /// from an earlier one.
    /// </summary>
    private FlightForces ResolveForces(
        FlightBodyState bodyState,
        FieldSample fieldSample,
        FlightControlOutput output,
        double mass,
        double couplingLevel) => new(
            MainDrive: output.Drive,
            Steering: output.Steering,
            Field: fieldResponse.Resolve(bodyState, fieldSample, couplingLevel, mass),
            GentleCurrent: gentleCurrent.Resolve(
                bodyState.Position,
                bodyState.LinearVelocity,
                mass,
                couplingLevel),
            SwiftCurrent: swiftCurrent.Resolve(
                bodyState.Position,
                bodyState.LinearVelocity,
                mass,
                couplingLevel),
            // The well is a mass well rather than a flow-coupled drive, so it is
            // the one source the coupling actuator does not reach.
            OrbitalPull: gravity.Resolve(bodyState.Position, mass),
            // No fault has biased the handling yet; the channel exists so a
            // damage response joins the table instead of replacing it.
            DamageBias: FlightWrench.Zero);

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
