using System.Numerics;
using System.Text;
using Rusty.Engine;
using Rusty.Space.Product.Engine.Tests;
using Rusty.Space.Product.ShipSystems;
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
    private const double SpawnHeading = Math.PI / 2.0;
    private const double HeadingTolerance = 5;
    private const float BodyMass = 2.0f;
    private const float BodyInertia = 2.0f;
    private const float RateTolerance = 1e-6f;

    [Fact]
    public void AnAuthoredSpawnHeadingSurvivesTheRoundTripThroughDynamics()
    {
        // The service echoes back the attitude each body was created with, so
        // the heading below travels the production path: authored, converted to
        // an Engine rotation, handed to Dynamics, and read off the readout whose
        // heading every consumer steers and thrusts along.
        RecordingDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics, SpawnHeading);

        Assert.Equal(SpawnHeading, flight.Readout.HeadingRadians, HeadingTolerance);
    }

    [Fact]
    public void ATurnToStarboardCommandsTheYawAxisOppositeTheEngineSense()
    {
        // A right turn is a heading-positive demand, and the planar plane turns
        // the other way about +Y. A command crossing without the flip would spin
        // the ship one way while its nose, its thrust, and its readouts reported
        // the other, and no straight-line check would notice.
        RecordingDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);

        flight.Admit(TurningUpdate(1, 1.0 / 60.0));

        Assert.Single(dynamics.Steps);
        DynamicsAction action = dynamics.Steps[0].Actions.Span[0];
        Assert.True(
            action.Torque.Y < 0.0f,
            $"expected a negative Engine yaw torque for a starboard turn, got {action.Torque.Y}");
    }

    [Fact]
    public void EachAdmittedFixedStepBecomesOneStepAtTheAdmittedDuration()
    {
        RecordingDynamics dynamics = new();
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
        RecordingDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);

        flight.Admit(Update(admittedSteps: 2, fixedDeltaSeconds: 1.0 / 30.0));

        Assert.Equal(2, dynamics.Steps.Count);
        Assert.All(dynamics.Steps, step => Assert.Equal(1.0f / 30.0f, step.StepSeconds, RateTolerance));
    }

    [Fact]
    public void ATurnAdmittedWithNoStepsAsksTheIntegratorForNothing()
    {
        RecordingDynamics dynamics = new();
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
        RecordingDynamics dynamics = new();
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
        RecordingDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);

        // The chart the hull flies is opened in the same world, so its authored
        // bodies are released with the flight as well: everything the flight
        // opened in the Engine, exactly once, and the world last.
        int authoredObstacles = SpaceTuning.Defaults.Approach.Obstacles.Count;
        Assert.Equal(1 + authoredObstacles, dynamics.BodyCreates);

        flight.Dispose();
        flight.Dispose();

        Assert.Equal(1 + authoredObstacles, dynamics.BodyReleases);
        Assert.Equal(1, dynamics.WorldReleases);
    }

    [Fact]
    public void AReleasedFlightRefusesToAdmitAnotherTurn()
    {
        RecordingDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);
        flight.Dispose();

        Assert.Throws<ObjectDisposedException>(() => flight.Admit(Update(1, 1.0 / 60.0)));
        Assert.Empty(dynamics.Steps);
    }

    [Fact]
    public void AResetPutsTheOldHullDownAndSpawnsAFreshOne()
    {
        RecordingDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);

        flight.ResetFlight();

        // A reset replaces the hull and nothing else: the chart the hull flies is
        // left standing, so the only bodies opened and put down are the hull's.
        int chartBodies = SpaceTuning.Defaults.Approach.Obstacles.Count;
        Assert.Equal(2 + chartBodies, dynamics.BodyCreates);
        Assert.Equal(1, dynamics.BodyReleases);
    }

    private static SpaceFlight Flight(
        RecordingDynamics dynamics,
        double spawnHeadingRadians = 0.0,
        ShipLoadout? loadout = null) => new(
        dynamics,
        new RecordingKinematic(),
        SpaceTuning.Defaults.Flight,
        SpaceTuning.Defaults.Coupling,
        SpaceTuning.Defaults.FlightBody with { SpawnHeadingRadians = spawnHeadingRadians },
        loadout ?? SpaceTuning.Defaults.Ship,
        SpaceTuning.Defaults.Damage,
        SpaceTuning.Defaults.Field,
        SpaceTuning.Defaults.Orbital,
        SpaceTuning.Defaults.GentleCurrent,
        SpaceTuning.Defaults.SwiftCurrent,
        SpaceTuning.Defaults.Trajectory,
        SpaceTuning.Defaults.Approach);

    [Fact]
    public void ACouplingPointFittedForwardTurnsTheHullHarderThanOneOnTheCenter()
    {
        // The field pushes on the hull where the emitter is fitted. Move that
        // point forward along the keel and the same flow that drives the ship
        // also swings the bow into itself: an oversized coil wired in because it
        // was there buys speed and a hull that will not hold a line. Nothing here
        // is tuned differently except where the push lands and how much of the
        // flow it catches, so the difference in the yaw the Engine is asked for
        // is the lever and nothing else.
        RecordingDynamics onSpec = new();
        RecordingDynamics salvaged = new();
        SpaceFlight stock = Flight(onSpec, loadout: ShipLoadouts.Stock);
        SpaceFlight scavenged = Flight(salvaged, loadout: ShipLoadouts.OversizedScavengedEmitter);

        double straightAhead = HeadingYawCommand(stock, onSpec);
        double weathercock = HeadingYawCommand(scavenged, salvaged);

        Assert.True(
            straightAhead > 0.0,
            $"expected the field to turn the hull at all, got {straightAhead}");
        Assert.True(
            weathercock > straightAhead * 3.0,
            $"expected the forward coupling point to turn far harder: {weathercock} against {straightAhead}");
    }

    private static double HeadingYawCommand(SpaceFlight flight, RecordingDynamics dynamics)
    {
        for (uint turn = 1; turn <= 20; turn++)
        {
            flight.Admit(Update(turn, 1.0 / 60.0));
        }

        // The Engine turns the other way about +Y, so the heading-positive yaw the
        // product asked for is the negation of what was recorded.
        return -dynamics.Steps[^1].Actions.Span[0].Torque.Y;
    }

    [Fact]
    public void AContactTheEngineReportsReachesTheInstrumentsWithSizeAndName()
    {
        // The Engine is the one that says the hull arrived at something and how
        // hard. The spine's job is to carry that to the instruments and to name
        // the part of the hull that took it, without deciding anything of its own
        // about whether the two bodies have met.
        RecordingDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);

        // The hull is the first body the flight opens, so the obstacle authored
        // first on the chart answers to the handle after it.
        dynamics.ContactCount = 1U;
        dynamics.HullContact = new DynamicsContactFact(
            Present: true,
            Environment: false,
            Impulse: new Vector3(0.0f, 0.0f, -3.0f),
            ImpulseMagnitude: 3.0f);
        dynamics.WorldContacts.Add(new DynamicsContactAtReceipt(
            Present: true,
            Environment: false,
            First: new DynamicsBodyReference(1UL),
            Second: new DynamicsBodyReference(2UL),
            Impulse: new Vector3(0.0f, 0.0f, -3.0f),
            ImpulseMagnitude: 3.0f));

        flight.Admit(Update(admittedSteps: 1, fixedDeltaSeconds: 1.0 / 60.0));

        Assert.Equal(1UL, flight.ImpactCount);
        Assert.True(flight.LastStrike.Impact.Present);
        Assert.Equal(3.0, flight.Telemetry.CollisionMagnitude, 6);
        Assert.Equal(-3.0, flight.Telemetry.CollisionImpulse.Z, 6);
        Assert.Equal(SpaceTuning.Defaults.Ship.StarboardStabilizer.Id, flight.Telemetry.StruckPart);
        Assert.Equal(SpaceTuning.Defaults.Approach.Obstacles[0].Id, flight.LastStrike.Impact.Struck);

        // An arrival is counted once, not once per step spent against the rock.
        flight.Admit(Update(admittedSteps: 2, fixedDeltaSeconds: 1.0 / 60.0));

        Assert.Equal(1UL, flight.ImpactCount);
    }

    [Fact]
    public void AnArrivalStaysReadableAfterTheHullComesOffIt()
    {
        // A glancing contact can be over in the single frame that produced it, and
        // an instrument that reports it for that frame alone reports nothing a
        // player or a tuning pass can act on. So the last arrival stays what the
        // instruments report — same magnitude, same place, same part — until a
        // newer one replaces it or the hull is rebuilt.
        RecordingDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);
        dynamics.ContactCount = 1U;
        dynamics.HullContact = new DynamicsContactFact(
            Present: true,
            Environment: false,
            Impulse: new Vector3(0.0f, 0.0f, -3.0f),
            ImpulseMagnitude: 3.0f);
        dynamics.WorldContacts.Add(new DynamicsContactAtReceipt(
            Present: true,
            Environment: false,
            First: new DynamicsBodyReference(1UL),
            Second: new DynamicsBodyReference(2UL),
            Impulse: new Vector3(0.0f, 0.0f, -3.0f),
            ImpulseMagnitude: 3.0f));

        flight.Admit(Update(admittedSteps: 1, fixedDeltaSeconds: 1.0 / 60.0));

        Assert.True(flight.LastStrike.StillTouching);

        dynamics.ContactCount = 0U;
        dynamics.HullContact = default;
        dynamics.WorldContacts.Clear();
        flight.Admit(Update(admittedSteps: 3, fixedDeltaSeconds: 1.0 / 60.0));

        Assert.False(flight.LastStrike.StillTouching);
        Assert.True(flight.LastStrike.Impact.Present);
        Assert.Equal(3.0, flight.LastStrike.Impact.Magnitude, 6);
        Assert.Equal(SpaceTuning.Defaults.Approach.Obstacles[0].Id, flight.LastStrike.Impact.Struck);
        Assert.Equal(3.0, flight.Telemetry.CollisionMagnitude, 6);
        Assert.Equal(1UL, flight.ImpactCount);

        // Rebuilt, this hull has arrived at nothing. The next turn reports nothing
        // rather than the previous hull's business.
        flight.ResetFlight();

        Assert.False(flight.LastStrike.Impact.Present);

        flight.Admit(Update(admittedSteps: 1, fixedDeltaSeconds: 1.0 / 60.0));

        Assert.Equal(0.0, flight.Telemetry.CollisionMagnitude, 6);
        Assert.Null(flight.Telemetry.StruckPart);
    }

    [Fact]
    public void AContactsPushIsLeftWithTheEngineThatGaveIt()
    {
        // The product hands the integrator the push it resolved itself: drive,
        // vanes, and what the field takes off them. A contact impulse is not
        // handed back on top of the Engine's own resolution of the same contact,
        // which would double it and put the hull somewhere the chart never did.
        RecordingDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);
        dynamics.ContactCount = 1U;
        dynamics.HullContact = new DynamicsContactFact(
            Present: true,
            Environment: false,
            Impulse: new Vector3(0.0f, 0.0f, -12.0f),
            ImpulseMagnitude: 12.0f);
        dynamics.WorldContacts.Add(new DynamicsContactAtReceipt(
            Present: true,
            Environment: false,
            First: new DynamicsBodyReference(1UL),
            Second: new DynamicsBodyReference(2UL),
            Impulse: new Vector3(0.0f, 0.0f, -12.0f),
            ImpulseMagnitude: 12.0f));

        flight.Admit(Update(admittedSteps: 1, fixedDeltaSeconds: 1.0 / 60.0));

        Assert.True(flight.LastStrike.Impact.Present);
        Vector3 handed = dynamics.Steps[^1].Actions.Span[0].Force;

        Assert.Equal((float)flight.Contributions.Total.Force.X, handed.X, 5);
        Assert.Equal((float)flight.Contributions.Total.Force.Z, handed.Z, 5);
    }

    [Fact]
    public void AHoldOnThePatchLetsAJammedEffectorGoAgain()
    {
        // The crew's answer to a jammed effector is time spent on it, asked for
        // through the same input lane as every other control.
        RecordingDynamics dynamics = new();
        SpaceFlight flight = Flight(dynamics);
        dynamics.ContactCount = 1U;
        dynamics.HullContact = new DynamicsContactFact(
            Present: true,
            Environment: false,
            Impulse: new Vector3(0.0f, 0.0f, -9.0f),
            ImpulseMagnitude: 9.0f);
        dynamics.WorldContacts.Add(new DynamicsContactAtReceipt(
            Present: true,
            Environment: false,
            First: new DynamicsBodyReference(1UL),
            Second: new DynamicsBodyReference(2UL),
            Impulse: new Vector3(0.0f, 0.0f, -9.0f),
            ImpulseMagnitude: 9.0f));

        flight.Admit(Update(admittedSteps: 1, fixedDeltaSeconds: 1.0 / 60.0));

        Assert.True(flight.Ship.StarboardStabilizer.OutOfTrim);

        dynamics.ContactCount = 0U;
        dynamics.HullContact = default;
        int patchedTurns = (int)Math.Ceiling(
            SpaceTuning.Defaults.Damage.RepairTime.TotalSeconds * 60.0) + 4;
        for (int turn = 0; turn < patchedTurns; turn++)
        {
            flight.Admit(Turn(1, 1.0 / 60.0, [Digital("space.flight.repair")]));
        }

        Assert.True(flight.LastCommand.RepairHeld);
        Assert.False(flight.Ship.StarboardStabilizer.OutOfTrim);
    }

    private static ProductUpdate Update(uint admittedSteps, double fixedDeltaSeconds) =>
        Turn(admittedSteps, fixedDeltaSeconds, []);

    private static ProductUpdate TurningUpdate(uint admittedSteps, double fixedDeltaSeconds) =>
        Turn(admittedSteps, fixedDeltaSeconds,
            [Digital("space.flight.turn-right")]);

    private static ProductUpdate Turn(
        uint admittedSteps,
        double fixedDeltaSeconds,
        ProductInputEvent[] input) => new(
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
            input);

    private static ProductInputEvent Digital(string intent) => new()
    {
        Kind = InputEventKind.MappedDigital,
        Phase = InputPhase.Pressed,
        X = 1.0f,
        Intent = Encoding.UTF8.GetBytes(intent),
    };
}
