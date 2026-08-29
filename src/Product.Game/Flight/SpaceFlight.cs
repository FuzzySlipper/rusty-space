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
    private const ulong SequenceIncrement = 1;
    private const double NeutralCommandIntent = 0.0;
    private const float FixedStepSeconds = 1.0f / 60.0f;
    private const double FixedStepDurationSeconds = 1.0 / 60.0;
    private const double QuaternionDoubleFactor = 2.0;
    private const double QuaternionUnitMagnitude = 1.0;

    private readonly IDynamicsService dynamics;
    private readonly DynamicsWorld world;
    private readonly FlightController controller;
    private readonly StellarField field;
    private readonly FieldResponse fieldResponse;
    private readonly FlightBodyTuning bodyTuning;
    private readonly FlightInputMapper inputMapper = new();
    private DynamicsBody body = null!;
    private FlightCommand command = new(NeutralCommandIntent, NeutralCommandIntent);
    private FlightReadout readout;
    private ulong fixedStepCount;
    private ulong updateSequence;
    private ulong resetCount;
    private bool disposed;

    internal SpaceFlight(
        IDynamicsService dynamics,
        FlightTuning flightTuning,
        FlightBodyTuning bodyTuning,
        FieldTuning fieldTuning)
    {
        this.dynamics = dynamics ?? throw new ArgumentNullException(nameof(dynamics));
        this.bodyTuning = bodyTuning.Validate();
        controller = new FlightController(flightTuning);
        field = new StellarField(fieldTuning);
        fieldResponse = new FieldResponse(fieldTuning);
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

    internal ulong FixedStepCount => fixedStepCount;

    internal ulong UpdateSequence => updateSequence;

    internal ulong ResetCount => resetCount;

    internal FlightAdmission Admit(ProductUpdate update)
    {
        ThrowIfDisposed();

        FlightInputPlan input = inputMapper.Prepare(update.Input);
        if (input.ResetRequested)
        {
            ResetFlight();
            return new FlightAdmission(true, fixedStepCount, updateSequence, input.FaultRequested);
        }

        // The Engine owns update admission and fixed-step timing; its facts
        // name the admitted steps for this turn. The product steps its own
        // dynamics exactly that many times and publishes on admitted turns.
        uint stepCount = update.Facts.AdmittedStepCount;
        if (stepCount == NoSteps)
        {
            command = input.Command;
            inputMapper.Commit(input);
            return new FlightAdmission(false, fixedStepCount, updateSequence, input.FaultRequested);
        }

        ulong nextFixedStepCount = checked(fixedStepCount + stepCount);
        ulong nextUpdateSequence = checked(updateSequence + SequenceIncrement);
        FlightControlOutput output = PrepareControllerOutput(input.Command, stepCount);
        FlightWrench fieldWrench = fieldResponse.Resolve(
            ToBodyState(readout),
            field.Sample(readout.Position));
        FlightWrench totalWrench = Add(output.Wrench, fieldWrench);
        DynamicsAction action = ToDynamicsAction(totalWrench);

        dynamics.Step(new DynamicsStepRequest(
            world,
            FixedStepSeconds,
            stepCount,
            new[] { action }));
        FlightReadout nextReadout = MapReadout(dynamics.Read(new DynamicsReadRequest(body)));

        controller.Commit(output);
        command = input.Command;
        inputMapper.Commit(input);
        readout = nextReadout;
        fixedStepCount = nextFixedStepCount;
        updateSequence = nextUpdateSequence;
        return new FlightAdmission(true, fixedStepCount, updateSequence, input.FaultRequested);
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
            command = new FlightCommand(NeutralCommandIntent, NeutralCommandIntent);
            controller.Reset();
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

    private FlightControlOutput PrepareControllerOutput(FlightCommand stagedCommand, uint steps)
    {
        FlightControlOutput output = default;
        double stagedThrottle = controller.ThrottleLevel;
        FlightBodyState bodyState = ToBodyState(readout);
        TimeSpan fixedStep = TimeSpan.FromSeconds(FixedStepDurationSeconds);
        for (uint stepIndex = 0; stepIndex < steps; stepIndex++)
        {
            output = controller.Prepare(
                bodyState,
                stagedCommand,
                readout.YawInertia,
                fixedStep,
                stagedThrottle);
            stagedThrottle = output.ThrottleLevel;
        }

        return output;
    }

    private DynamicsBody CreateSpawnBody() => dynamics.CreateBody(new DynamicsCreateBodyRequest(
        world,
        new DynamicsBodyConfig(
            new Transform(
                new Vector3(
                    ToSingle(bodyTuning.SpawnPosition.X),
                    ToSingle(bodyTuning.SpawnHeight),
                    ToSingle(bodyTuning.SpawnPosition.Z)),
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, ToSingle(bodyTuning.SpawnHeadingRadians)),
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
        new Vector3(ToSingle(NeutralCommandIntent), ToSingle(wrench.TorqueY), ToSingle(NeutralCommandIntent)),
        Vector3.Zero,
        Vector3.Zero,
        Wake: true);

    private static FlightBodyState ToBodyState(FlightReadout value) => new(
        value.Position,
        value.HeadingRadians,
        value.LinearVelocity,
        value.AngularVelocity);

    private static FlightWrench Add(FlightWrench left, FlightWrench right) => new(
        left.Force + right.Force,
        left.TorqueY + right.TorqueY);

    private static FlightReadout MapReadout(DynamicsReadout native) => new(
        new PlanarVector(native.Transform.Translation.X, native.Transform.Translation.Z),
        HeadingRadians(native.Transform.Rotation),
        new PlanarVector(native.LinearVelocity.X, native.LinearVelocity.Z),
        native.AngularVelocity.Y,
        native.MassProperties.Mass,
        native.MassProperties.PrincipalInertia.Y);

    private static double HeadingRadians(Quaternion rotation)
    {
        double yawNumerator = QuaternionDoubleFactor
            * ((rotation.W * rotation.Y) + (rotation.X * rotation.Z));
        double yawDenominator = QuaternionUnitMagnitude - (QuaternionDoubleFactor
            * ((rotation.Y * rotation.Y) + (rotation.Z * rotation.Z)));
        return Math.Atan2(yawNumerator, yawDenominator);
    }

    private static float ToSingle(double value) => checked((float)value);

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(SpaceFlight));
        }
    }
}

internal readonly record struct FlightAdmission(
    bool Published,
    ulong FixedStepCount,
    ulong UpdateSequence,
    bool FaultRequested);
