using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Flight.Tests;

/// <summary>
/// Pins the flight controller's actuator behavior: thrust that spools, coast
/// that begins instantly, the speed ceiling, and steering authority that is
/// finite and symmetric.
/// </summary>
public class FlightControllerTests
{
    private const double Tolerance = 1e-9;
    private const int CatchUpSubsteps = 4;
    private static readonly TimeSpan FixedStep = TimeSpan.FromSeconds(1.0 / 60.0);

    private readonly FlightTuning tuning = SpaceTuning.Defaults.Flight;

    [Fact]
    public void ThrottleSpoolsTowardTheCommandedThrustInsteadOfJumpingToIt()
    {
        FlightController controller = new(tuning);

        FlightControlOutput output = controller.Prepare(
            Coast(headingRadians: 0.0),
            new FlightCommand(Throttle: 1.0, Turn: 0.0),
            momentOfInertia: 2.0,
            FixedStep,
            currentThrottleLevel: 0.0);

        double expectedSpool = tuning.MaximumThrust * (FixedStep.TotalSeconds / tuning.ThrottleResponse.TotalSeconds);
        Assert.Equal(expectedSpool, output.ThrottleLevel, Tolerance);
        Assert.Equal(expectedSpool / tuning.MaximumThrust, output.DriveEffort, Tolerance);
    }

    [Fact]
    public void ReleasingThrustEndsThePushOnTheSameTurnItIsReleased()
    {
        FlightController controller = new(tuning);

        FlightControlOutput output = controller.Prepare(
            Coast(headingRadians: 0.0),
            new FlightCommand(Throttle: 0.0, Turn: 0.0),
            momentOfInertia: 2.0,
            FixedStep,
            currentThrottleLevel: tuning.MaximumThrust);

        Assert.Equal(0.0, output.ThrottleLevel, Tolerance);
        Assert.Equal(0.0, output.Drive.Force.X, Tolerance);
        Assert.Equal(0.0, output.Drive.Force.Z, Tolerance);
    }

    [Fact]
    public void DriveFollowsTheHeadingSoATurnedShipPushesSomewhereElse()
    {
        FlightController controller = new(tuning);

        FlightControlOutput north = controller.Prepare(
            Coast(headingRadians: Math.PI / 2.0),
            new FlightCommand(Throttle: 1.0, Turn: 0.0),
            momentOfInertia: 2.0,
            FixedStep,
            currentThrottleLevel: tuning.MaximumThrust);

        Assert.Equal(0.0, north.Drive.Force.X, 9);
        Assert.Equal(tuning.MaximumThrust, north.Drive.Force.Z, 9);
        Assert.False(north.DriveSaturated);
    }

    [Fact]
    public void DriveStopsAddingPushAlongTheVelocityOnceTheShipReachesMaximumSpeed()
    {
        FlightController controller = new(tuning);
        FlightBodyState atCeiling = Coast(headingRadians: 0.0) with
        {
            LinearVelocity = new PlanarVector(tuning.MaximumSpeed, 0.0),
        };

        FlightControlOutput output = controller.Prepare(
            atCeiling,
            new FlightCommand(Throttle: 1.0, Turn: 0.0),
            momentOfInertia: 2.0,
            FixedStep,
            currentThrottleLevel: tuning.MaximumThrust);

        Assert.Equal(0.0, output.Drive.Force.X, Tolerance);
        Assert.True(output.DriveSaturated);
    }

    [Fact]
    public void TheSpeedCeilingOnlyRemovesPushAlongTheVelocity()
    {
        FlightController controller = new(tuning);
        FlightBodyState atCeiling = Coast(headingRadians: Math.PI / 2.0) with
        {
            LinearVelocity = new PlanarVector(tuning.MaximumSpeed, 0.0),
        };

        FlightControlOutput output = controller.Prepare(
            atCeiling,
            new FlightCommand(Throttle: 1.0, Turn: 0.0),
            momentOfInertia: 2.0,
            FixedStep,
            currentThrottleLevel: tuning.MaximumThrust);

        // A ship pinned at the ceiling in one direction can still be pushed
        // into a new one; only the component that would take it further past
        // the ceiling is taken away.
        Assert.Equal(tuning.MaximumThrust, output.Drive.Force.Z, 9);
        Assert.True(output.Drive.Force.X < 1e-12);
        Assert.True(output.DriveSaturated);
    }

    [Fact]
    public void SteeringTorqueIsLimitedByActuatorAuthority()
    {
        FlightController controller = new(tuning);
        const double inertia = 2.0;

        FlightControlOutput output = controller.Prepare(
            Coast(headingRadians: 0.0) with { AngularVelocity = -tuning.MaximumTurnRate },
            new FlightCommand(Throttle: 0.0, Turn: 1.0),
            inertia,
            FixedStep,
            currentThrottleLevel: 0.0);

        double authority = inertia * tuning.MaximumTurnRate / tuning.SteeringResponse.TotalSeconds;
        Assert.Equal(authority, output.Steering.TorqueY, Tolerance);
        Assert.Equal(1.0, output.SteeringEffort, 9);
        Assert.True(output.SteeringSaturated);
    }

    [Fact]
    public void SteeringIsSymmetricAboutNeutral()
    {
        FlightController controller = new(tuning);

        FlightControlOutput starboard = controller.Prepare(
            Coast(headingRadians: 0.0),
            new FlightCommand(Throttle: 0.0, Turn: 0.5),
            momentOfInertia: 2.0,
            FixedStep,
            currentThrottleLevel: 0.0);
        FlightControlOutput port = controller.Prepare(
            Coast(headingRadians: 0.0),
            new FlightCommand(Throttle: 0.0, Turn: -0.5),
            momentOfInertia: 2.0,
            FixedStep,
            currentThrottleLevel: 0.0);

        Assert.Equal(-port.Steering.TorqueY, starboard.Steering.TorqueY, Tolerance);
        Assert.Equal(port.SteeringEffort, starboard.SteeringEffort, Tolerance);
    }

    [Fact]
    public void AnUnusableMomentOfInertiaProducesNoSteeringInsteadOfNaN()
    {
        FlightController controller = new(tuning);

        FlightControlOutput output = controller.Prepare(
            Coast(headingRadians: 0.0),
            new FlightCommand(Throttle: 0.0, Turn: 1.0),
            momentOfInertia: 0.0,
            FixedStep,
            currentThrottleLevel: 0.0);

        Assert.Equal(0.0, output.Steering.TorqueY, Tolerance);
        Assert.Equal(0.0, output.SteeringEffort, Tolerance);
        Assert.False(output.SteeringSaturated);
    }

    [Fact]
    public void ANonPositiveStepIsRejectedRatherThanSilentlySpoolingWrong()
    {
        FlightController controller = new(tuning);

        Assert.Throws<ArgumentOutOfRangeException>(() => controller.Prepare(
            Coast(headingRadians: 0.0),
            new FlightCommand(Throttle: 1.0, Turn: 0.0),
            momentOfInertia: 2.0,
            TimeSpan.Zero,
            currentThrottleLevel: 0.0));
    }

    [Fact]
    public void ACatchUpTurnAdvancesTheSpoolOncePerFixedStep()
    {
        // Every admitted step is one fixed step of simulated time, so four
        // substeps inside one turn travel four times and land where four
        // separate turns would land.
        FlightController controller = new(tuning);
        double level = controller.ThrottleLevel;
        for (int substep = 0; substep < CatchUpSubsteps; substep++)
        {
            level = controller.Prepare(
                Coast(headingRadians: 0.0),
                new FlightCommand(Throttle: 1.0, Turn: 0.0),
                momentOfInertia: 2.0,
                FixedStep,
                currentThrottleLevel: level).ThrottleLevel;
        }

        double response = FixedStep.TotalSeconds / tuning.ThrottleResponse.TotalSeconds;
        double expected = 0.0;
        for (int substep = 0; substep < CatchUpSubsteps; substep++)
        {
            expected += (tuning.MaximumThrust - expected) * response;
        }

        Assert.Equal(expected, level, Tolerance);
    }

    [Fact]
    public void OnlyCommitPublishesTheSpoolSoOneSubstepCannotBeCountedTwice()
    {
        FlightController controller = new(tuning);

        FlightControlOutput output = controller.Prepare(
            Coast(headingRadians: 0.0),
            new FlightCommand(Throttle: 1.0, Turn: 0.0),
            momentOfInertia: 2.0,
            FixedStep,
            currentThrottleLevel: 0.0);

        // Preparing is pure about the spool; the level moves only when the turn
        // commits, and committing the same output again cannot advance it.
        Assert.Equal(0.0, controller.ThrottleLevel, Tolerance);

        controller.Commit(output);
        controller.Commit(output);
        Assert.Equal(output.ThrottleLevel, controller.ThrottleLevel, Tolerance);
    }

    private static FlightBodyState Coast(double headingRadians) => new(
        PlanarVector.Zero,
        headingRadians,
        PlanarVector.Zero,
        0.0);
}
