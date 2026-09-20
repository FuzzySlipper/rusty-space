using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Flight.Tests;

/// <summary>
/// The flight spine's contract with the one integrator it is allowed to use:
/// every admitted fixed step becomes exactly one step at the admitted duration,
/// and the world and hull it opened are put down once, and only once, when the
/// flight is released. The service below is the whole seam: Space decides what
/// to ask for, and what it asks is the claim.
/// </summary>
public class SpaceFlightTests
{
    private const float BodyMass = 2.0f;
    private const float BodyInertia = 2.0f;
    private const float RateTolerance = 1e-6f;

    [Fact]
    public void EachAdmittedFixedStepBecomesOneStepAtTheAdmittedDuration()
    {
        AdmittedDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);

        flight.Admit(Update(admittedSteps: 4, fixedDeltaSeconds: 1.0 / 60.0));

        Assert.Equal(4, dynamics.Steps.Count);
        Assert.All(dynamics.Steps, step =>
        {
            Assert.Equal(1U, step.Steps);
            Assert.Equal(1.0f / 60.0f, step.StepSeconds, RateTolerance);
        });
    }

    [Fact]
    public void AHostAdmittedAtAnotherRateIsPassedThroughToTheIntegrator()
    {
        // The product restates no rate of its own, so a host running the
        // lifecycle at half the rate halves the step it is asked to integrate
        // rather than getting a product constant it never declared.
        AdmittedDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);

        flight.Admit(Update(admittedSteps: 2, fixedDeltaSeconds: 1.0 / 30.0));

        Assert.Equal(2, dynamics.Steps.Count);
        Assert.All(dynamics.Steps, step => Assert.Equal(1.0f / 30.0f, step.StepSeconds, RateTolerance));
    }

    [Fact]
    public void ATurnAdmittedWithNoStepsAsksTheIntegratorForNothing()
    {
        AdmittedDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);

        flight.Admit(Update(admittedSteps: 0, fixedDeltaSeconds: 1.0 / 60.0));

        Assert.Empty(dynamics.Steps);
    }

    [Fact]
    public void EachSubstepAsksForPushAgainstTheStateThatSubstepMeets()
    {
        // A catch-up turn does not freeze one answer and apply it four times:
        // every substep carries a force for the body state that substep is
        // about to integrate, taken from the read-back between substeps.
        AdmittedDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);
        flight.Admit(Update(admittedSteps: 2, fixedDeltaSeconds: 1.0 / 60.0));

        // The hull picks up speed as the field drags it, so the second substep
        // meets a different slip against the same flow and is pushed
        // differently. A frozen pre-turn answer would have looked identical.
        Assert.Equal(2, dynamics.Steps.Count);
        Assert.True(dynamics.Reads >= 3);
        Assert.NotEqual(
            dynamics.Steps[0].Actions.Span[0].Force,
            dynamics.Steps[1].Actions.Span[0].Force);
    }

    [Fact]
    public void ReleasingTheFlightPutsItsWorldAndHullDownExactlyOnce()
    {
        AdmittedDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);

        Assert.Equal(1, dynamics.BodyCreates);

        flight.Dispose();
        flight.Dispose();

        Assert.Equal(1, dynamics.BodyReleases);
        Assert.Equal(1, dynamics.WorldReleases);
    }

    [Fact]
    public void AReleasedFlightRefusesToAdmitAnotherTurn()
    {
        AdmittedDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);
        flight.Dispose();

        Assert.Throws<ObjectDisposedException>(() => flight.Admit(Update(1, 1.0 / 60.0)));
        Assert.Empty(dynamics.Steps);
    }

    [Fact]
    public void AResetPutsTheOldHullDownAndSpawnsAFreshOne()
    {
        AdmittedDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);

        flight.ResetFlight();

        Assert.Equal(2, dynamics.BodyCreates);
        Assert.Equal(1, dynamics.BodyReleases);
    }

    private static SpaceFlight Flight(AdmittedDynamics dynamics) => new(
        dynamics,
        SpaceTuning.Defaults.Flight,
        SpaceTuning.Defaults.Coupling,
        SpaceTuning.Defaults.FlightBody,
        SpaceTuning.Defaults.Field,
        SpaceTuning.Defaults.Orbital,
        SpaceTuning.Defaults.GentleCurrent,
        SpaceTuning.Defaults.SwiftCurrent);

    private static ProductUpdate Update(uint admittedSteps, double fixedDeltaSeconds) => new(
        new ProductUpdateFacts(
            ProductUpdateMode.Realtime,
            ProductLifecycleState.Running,
            Generation: 1UL,
            ControlRevision: 0UL,
            ObservedHostTimeNanoseconds: 0UL,
            SimulationStep: 0UL,
            FixedStepHz: 60U,
            AdmittedStepCount: admittedSteps,
            DroppedStepCount: 0UL,
            FixedDeltaSeconds: fixedDeltaSeconds),
        ReadOnlySpan<ProductInputEvent>.Empty);

    /// <summary>
    /// The smallest Dynamics service that records what the product asked the
    /// integrator for and hands out lease handles a test can watch being
    /// released. Anything Space never calls is refused loudly, so a change that
    /// starts reaching for another path is a test failure rather than a silent
    /// widening of the seam.
    /// </summary>
    private sealed class AdmittedDynamics : IDynamicsService
    {
        private const float SlipPerRead = 0.5f;

        internal List<DynamicsStepRequest> Steps { get; } = [];
        internal int Reads { get; private set; }
        internal int BodyCreates { get; private set; }
        internal int BodyReleases { get; private set; }
        internal int WorldReleases { get; private set; }

        public DynamicsWorld CreateWorld(DynamicsWorldConfig arg0) => new(default, RecordWorldRelease);

        public DynamicsBody CreateBody(DynamicsCreateBodyRequest arg0)
        {
            BodyCreates++;
            return new DynamicsBody(default, RecordBodyRelease);
        }

        public DynamicsStepReceipt Step(DynamicsStepRequest arg0)
        {
            Steps.Add(arg0);
            return new DynamicsStepReceipt((ulong)Steps.Count, 1U, 0U);
        }

        public DynamicsReadout Read(DynamicsReadRequest arg0) => new(
            new Transform(Vector3.Zero, Quaternion.Identity, Vector3.One),
            new Vector3(SlipPerRead * ++Reads, 0.0f, 0.0f),
            Vector3.Zero,
            Sleeping: false,
            new MassProperties(
                Available: true,
                Mass: BodyMass,
                PrincipalInertia: new Vector3(BodyInertia, BodyInertia, BodyInertia),
                Policy: DynamicsMassPolicyKind.DeriveFromShapeAndMass,
                CenterOfMass: Vector3.Zero,
                PrincipalInertiaLocalFrame: Quaternion.Identity),
            ContactCount: 0U,
            FirstContact: default);

        public DynamicsBody CreateSphereBody(DynamicsCreateSphereBodyRequest arg0)
            => throw new NotSupportedException();

        public DynamicsBody CreateCuboidBody(DynamicsCreateCuboidBodyRequest arg0)
            => throw new NotSupportedException();

        public DynamicsBody CreateSphereBodyWithProperties(DynamicsCreateSphereBodyPropertiesRequest arg0)
            => throw new NotSupportedException();

        public DynamicsBody CreateCapsuleBody(DynamicsCreateCapsuleBodyRequest arg0)
            => throw new NotSupportedException();

        public void BindWorldCollision(DynamicsWorldCollisionBindingRequest arg0)
            => throw new NotSupportedException();

        public DynamicsRebaseWorldOriginReceipt RebaseWorldOrigin(DynamicsRebaseWorldOriginRequest arg0)
            => throw new NotSupportedException();

        public DynamicsStepAndReadLeaseReceipt StepAndRead(DynamicsStepAndReadRequest arg0)
            => throw new NotSupportedException();

        public void Reset(DynamicsResetRequest arg0) => throw new NotSupportedException();

        public void UpdateBody(DynamicsUpdateBodyRequest arg0) => throw new NotSupportedException();

        public DynamicsWorldReadout ReadWorld(DynamicsWorldReadRequest arg0)
            => throw new NotSupportedException();

        public DynamicsBodyAtReceipt ReadBodyAt(DynamicsBodyAtRequest arg0)
            => throw new NotSupportedException();

        public DynamicsContactAtReceipt ReadContactAt(DynamicsContactAtRequest arg0)
            => throw new NotSupportedException();

        public DynamicsBody ReplaceBody(DynamicsReplaceBodyRequest arg0)
            => throw new NotSupportedException();

        public DynamicsBody ReplaceCuboidBody(DynamicsReplaceCuboidBodyRequest arg0)
            => throw new NotSupportedException();

        public DynamicsBody ReplaceSphereBody(DynamicsReplaceSphereBodyRequest arg0)
            => throw new NotSupportedException();

        public DynamicsBody ReplaceCapsuleBody(DynamicsReplaceCapsuleBodyRequest arg0)
            => throw new NotSupportedException();

        private void RecordWorldRelease() => WorldReleases++;

        private void RecordBodyRelease() => BodyReleases++;
    }
}
