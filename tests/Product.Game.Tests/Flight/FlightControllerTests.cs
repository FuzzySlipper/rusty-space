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
    private const int SettledSpoolSteps = 400;
    private static readonly TimeSpan FixedStep = TimeSpan.FromSeconds(1.0 / 60.0);

    private readonly FlightTuning tuning = SpaceTuning.Defaults.Flight;

    [Fact]
    public void ThrottleSpoolsTowardTheCommandedThrustInsteadOfJumpingToIt()
    {
        FlightController controller = new(tuning);

        FlightControlOutput output = controller.Advance(
            Coast(headingRadians: 0.0),
            Command(1.0, 0.0),
            momentOfInertia: 2.0,
            FixedStep);

        double expectedSpool = tuning.MaximumThrust * (FixedStep.TotalSeconds / tuning.ThrottleResponse.TotalSeconds);
        Assert.Equal(expectedSpool, output.ThrottleLevel, Tolerance);
        Assert.Equal(expectedSpool / tuning.MaximumThrust, output.DriveEffort, Tolerance);
    }

    [Fact]
    public void ReleasingThrustEndsThePushOnTheSameTurnItIsReleased()
    {
        FlightController controller = AtFullThrust();

        FlightControlOutput output = controller.Advance(
            Coast(headingRadians: 0.0),
            Command(0.0, 0.0),
            momentOfInertia: 2.0,
            FixedStep);

        Assert.Equal(0.0, output.ThrottleLevel, Tolerance);
        Assert.Equal(0.0, output.Drive.Force.X, Tolerance);
        Assert.Equal(0.0, output.Drive.Force.Z, Tolerance);
    }

    [Fact]
    public void DriveFollowsTheHeadingSoATurnedShipPushesSomewhereElse()
    {
        FlightController controller = AtFullThrust(headingRadians: Math.PI / 2.0);

        FlightControlOutput north = controller.Advance(
            Coast(headingRadians: Math.PI / 2.0),
            Command(1.0, 0.0),
            momentOfInertia: 2.0,
            FixedStep);

        Assert.Equal(0.0, north.Drive.Force.X, 9);
        Assert.Equal(tuning.MaximumThrust, north.Drive.Force.Z, 9);
        Assert.False(north.DriveSaturated);
    }

    [Fact]
    public void DriveStopsAddingPushAlongTheVelocityOnceTheShipReachesMaximumSpeed()
    {
        FlightController controller = AtFullThrust(headingRadians: 0.0);
        FlightBodyState atCeiling = Coast(headingRadians: 0.0) with
        {
            LinearVelocity = new PlanarVector(tuning.MaximumSpeed, 0.0),
        };

        FlightControlOutput output = controller.Advance(
            atCeiling,
            Command(1.0, 0.0),
            momentOfInertia: 2.0,
            FixedStep);

        Assert.Equal(0.0, output.Drive.Force.X, Tolerance);
        Assert.True(output.DriveSaturated);
    }

    [Fact]
    public void TheSpeedCeilingOnlyRemovesPushAlongTheVelocity()
    {
        FlightController controller = AtFullThrust(headingRadians: Math.PI / 2.0);
        FlightBodyState atCeiling = Coast(headingRadians: Math.PI / 2.0) with
        {
            LinearVelocity = new PlanarVector(tuning.MaximumSpeed, 0.0),
        };

        FlightControlOutput output = controller.Advance(
            atCeiling,
            Command(1.0, 0.0),
            momentOfInertia: 2.0,
            FixedStep);

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

        FlightControlOutput output = controller.Advance(
            Coast(headingRadians: 0.0) with { AngularVelocity = -tuning.MaximumTurnRate },
            Command(0.0, 1.0),
            inertia,
            FixedStep);

        double authority = inertia * tuning.MaximumTurnRate / tuning.SteeringResponse.TotalSeconds;
        Assert.Equal(authority, output.Steering.YawTorque, Tolerance);
        Assert.Equal(1.0, output.SteeringEffort, 9);
        Assert.True(output.SteeringSaturated);
    }

    [Fact]
    public void SteeringIsSymmetricAboutNeutral()
    {
        FlightController controller = new(tuning);

        FlightControlOutput starboard = controller.Advance(
            Coast(headingRadians: 0.0),
            Command(0.0, 0.5),
            momentOfInertia: 2.0,
            FixedStep);
        FlightControlOutput port = controller.Advance(
            Coast(headingRadians: 0.0),
            Command(0.0, -0.5),
            momentOfInertia: 2.0,
            FixedStep);

        Assert.Equal(-port.Steering.YawTorque, starboard.Steering.YawTorque, Tolerance);
        Assert.Equal(port.SteeringEffort, starboard.SteeringEffort, Tolerance);
    }

    [Fact]
    public void AnUnusableMomentOfInertiaProducesNoSteeringInsteadOfNaN()
    {
        FlightController controller = new(tuning);

        FlightControlOutput output = controller.Advance(
            Coast(headingRadians: 0.0),
            Command(0.0, 1.0),
            momentOfInertia: 0.0,
            FixedStep);

        Assert.Equal(0.0, output.Steering.YawTorque, Tolerance);
        Assert.Equal(0.0, output.SteeringEffort, Tolerance);
        Assert.False(output.SteeringSaturated);
    }

    [Fact]
    public void TheSpoolTravelsOverTheAdmittedDurationRatherThanATurnCount()
    {
        // A host admitted at a coarser fixed rate hands the spool more time per
        // turn, so it travels further, and no admitted rate overshoots the
        // commanded thrust.
        FlightController fineSpool = new(tuning);
        FlightController coarseSpool = new(tuning);

        double fine = fineSpool.Advance(
            Coast(headingRadians: 0.0),
            Command(1.0, 0.0),
            momentOfInertia: 2.0,
            TimeSpan.FromSeconds(1.0 / 60.0)).ThrottleLevel;
        double coarse = coarseSpool.Advance(
            Coast(headingRadians: 0.0),
            Command(1.0, 0.0),
            momentOfInertia: 2.0,
            TimeSpan.FromSeconds(1.0 / 30.0)).ThrottleLevel;

        Assert.True(fine > 0.0);
        Assert.True(coarse > fine);
        Assert.True(coarse <= tuning.MaximumThrust);
    }

    [Fact]
    public void ACatchUpTurnAdvancesTheSpoolOncePerFixedStep()
    {
        // Every admitted step is one fixed step of simulated time, so four
        // substeps inside one turn travel four times and land where four
        // separate turns would land.
        FlightController controller = new(tuning);
        for (int substep = 0; substep < CatchUpSubsteps; substep++)
        {
            controller.Advance(
                Coast(headingRadians: 0.0),
                Command(1.0, 0.0),
                momentOfInertia: 2.0,
                FixedStep);
        }

        double response = FixedStep.TotalSeconds / tuning.ThrottleResponse.TotalSeconds;
        double expected = 0.0;
        for (int substep = 0; substep < CatchUpSubsteps; substep++)
        {
            expected += (tuning.MaximumThrust - expected) * response;
        }

        Assert.Equal(expected, controller.ThrottleLevel, Tolerance);
    }

    [Fact]
    public void TheSpoolAnAdvanceReportsIsTheSpoolTheOwnerNowHolds()
    {
        // The level reported for a substep and the controller's own state are
        // one value, so an admitted interval can neither be counted twice nor
        // go unpublished until the end of the turn.
        FlightController controller = new(tuning);

        FlightControlOutput first = controller.Advance(
            Coast(headingRadians: 0.0),
            Command(1.0, 0.0),
            momentOfInertia: 2.0,
            FixedStep);
        Assert.Equal(first.ThrottleLevel, controller.ThrottleLevel, Tolerance);

        FlightControlOutput second = controller.Advance(
            Coast(headingRadians: 0.0),
            Command(1.0, 0.0),
            momentOfInertia: 2.0,
            FixedStep);
        Assert.True(second.ThrottleLevel > first.ThrottleLevel);
        Assert.Equal(second.ThrottleLevel, controller.ThrottleLevel, Tolerance);
    }

    [Fact]
    public void ADisengagedStabilizerLetsReleasedRotationCarryOn()
    {
        FlightController controller = new(tuning);

        FlightControlOutput coasting = controller.Advance(
            Spinning(headingRadians: 0.0, angularVelocity: 1.4),
            new FlightCommand(
                Throttle: 0.0,
                Turn: 0.0,
                CouplingTrim: 0.0,
                StabilizerEnabled: false,
                EmergencyUncouple: false),
            momentOfInertia: 2.0,
            FixedStep);

        Assert.Equal(0.0, coasting.Steering.YawTorque, 12);
        Assert.Equal(0.0, coasting.SteeringEffort, 12);
    }

    [Fact]
    public void AnEngagedStabilizerHoldsAttitudeAgainstExistingRotation()
    {
        FlightController controller = new(tuning);

        FlightControlOutput holding = controller.Advance(
            Spinning(headingRadians: 0.0, angularVelocity: 1.4),
            Command(0.0, 0.0),
            momentOfInertia: 2.0,
            FixedStep);

        Assert.True(holding.Steering.YawTorque < 0.0);
        Assert.True(holding.SteeringEffort > 0.0);
    }

    [Fact]
    public void SteeringStillAnswersADemandWithTheStabilizerDisengaged()
    {
        FlightController controller = new(tuning);

        FlightControlOutput turning = controller.Advance(
            Spinning(headingRadians: 0.0, angularVelocity: 0.0),
            new FlightCommand(
                Throttle: 0.0,
                Turn: 1.0,
                CouplingTrim: 0.0,
                StabilizerEnabled: false,
                EmergencyUncouple: false),
            momentOfInertia: 2.0,
            FixedStep);

        Assert.True(turning.Steering.YawTorque > 0.0);
    }

    /// <summary>
    /// A controller whose spool has already reached commanded thrust, for tests
    /// about what a settled actuator pushes rather than how long the spool takes
    /// to get there. The spool is first order, so a few seconds of admitted
    /// steps is what lands it on full.
    /// </summary>
    private FlightController AtFullThrust(double headingRadians = 0.0)
    {
        FlightController controller = new(tuning);
        for (int step = 0; step < SettledSpoolSteps; step++)
        {
            controller.Advance(
                Coast(headingRadians),
                Command(1.0, 0.0),
                momentOfInertia: 2.0,
                FixedStep);
        }

        Assert.Equal(tuning.MaximumThrust, controller.ThrottleLevel, 9);
        return controller;
    }

    private static FlightBodyState Coast(double headingRadians) => new(
        PlanarVector.Zero,
        headingRadians,
        PlanarVector.Zero,
        0.0);

    private static FlightBodyState Spinning(
        double headingRadians,
        double angularVelocity) => new(
            PlanarVector.Zero,
            headingRadians,
            PlanarVector.Zero,
            angularVelocity);

    private static FlightCommand Command(double throttle, double turn) => new(
        throttle,
        turn,
        CouplingTrim: 0.0,
        StabilizerEnabled: true,
        EmergencyUncouple: false);
}
