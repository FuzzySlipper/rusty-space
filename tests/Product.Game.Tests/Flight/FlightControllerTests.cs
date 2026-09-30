using Rusty.Space.Product.Navigation;
using Xunit;

namespace Rusty.Space.Product.Flight.Tests;

public class FlightControllerTests
{
    private readonly FlightTuning tuning = new(12.0, 6.0, 2.1, TimeSpan.FromSeconds(0.25));
    private static FlightBodyState Rest => new(PlanarVector.Zero, 0, PlanarVector.Zero, 0);

    [Fact]
    public void TheControllerHandsTheWholeThrottleDemandToTheInstalledDrive()
    {
        FlightController controller = new(tuning);
        FlightControlOutput output = controller.Resolve(Rest, FlightCommand.Neutral with { Throttle = 0.5 }, 2.0);
        Assert.Equal(new PlanarVector(3.0, 0.0), output.Drive.Force);
        Assert.Equal(0.5, output.DriveEffort);
        Assert.Equal(PlanarVector.Zero, controller.Resolve(Rest, FlightCommand.Neutral, 2.0).Drive.Force);
    }

    [Fact]
    public void AHeadingChangeChangesTheDemandDirection()
    {
        FlightController controller = new(tuning);
        FlightControlOutput output = controller.Resolve(Rest with { HeadingRadians = Math.PI / 2 },
            FlightCommand.Neutral with { Throttle = 1 }, 2);
        Assert.Equal(0.0, output.Drive.Force.X, 9);
        Assert.Equal(6.0, output.Drive.Force.Z, 9);
    }

    [Fact]
    public void TheSpeedCeilingRemovesOnlyAccelerationAlongExistingVelocity()
    {
        FlightController controller = new(tuning);
        FlightBodyState atCeiling = Rest with { LinearVelocity = new PlanarVector(12, 0) };
        FlightCommand powered = FlightCommand.Neutral with { Throttle = 1 };
        Assert.Equal(PlanarVector.Zero, controller.Resolve(atCeiling, powered, 2).Drive.Force);
        FlightControlOutput crossing = controller.Resolve(atCeiling with { HeadingRadians = Math.PI / 2 }, powered, 2);
        Assert.Equal(6.0, crossing.Drive.Force.Z, 9);
    }

    [Fact]
    public void TheControllerDoesNotClampTheDemandBeforeThePartsSeeIt()
    {
        FlightController controller = new(tuning);
        FlightControlOutput output = controller.Resolve(Rest with { AngularVelocity = -2.1 },
            FlightCommand.Neutral with { Turn = 1 }, 2);
        Assert.Equal(2 * 4.2 / 0.25, output.Steering.YawTorque, 9);
    }

    [Fact]
    public void AttitudeHoldIsARequestAndReleasingItLetsRotationCoast()
    {
        FlightController controller = new(tuning);
        FlightBodyState spinning = Rest with { AngularVelocity = 1.4 };
        Assert.True(controller.Resolve(spinning, FlightCommand.Neutral, 2).Steering.YawTorque < 0);
        Assert.Equal(0, controller.Resolve(spinning,
            FlightCommand.Neutral with { StabilizerEnabled = false }, 2).Steering.YawTorque);
        Assert.True(controller.Resolve(Rest,
            FlightCommand.Neutral with { StabilizerEnabled = false, Turn = 1 }, 2).Steering.YawTorque > 0);
    }

    [Fact]
    public void SteeringDemandsAreSymmetricAndInvalidInertiaCannotProduceTorque()
    {
        FlightController controller = new(tuning);
        double port = controller.Resolve(Rest, FlightCommand.Neutral with { Turn = -0.5 }, 2).Steering.YawTorque;
        double starboard = controller.Resolve(Rest, FlightCommand.Neutral with { Turn = 0.5 }, 2).Steering.YawTorque;
        Assert.Equal(-port, starboard);
        Assert.Equal(0, controller.Resolve(Rest, FlightCommand.Neutral with { Turn = 1 }, 0).Steering.YawTorque);
    }
}
