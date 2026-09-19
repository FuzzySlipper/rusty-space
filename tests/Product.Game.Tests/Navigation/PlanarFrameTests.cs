using System.Numerics;
using Rusty.Space.Product.Navigation;
using Xunit;

namespace Rusty.Space.Product.Navigation.Tests;

/// <summary>
/// Pins the planar frame convention with cases, in both senses, so an off-center
/// force added later cannot silently agree with the wrong one of them.
/// </summary>
public class PlanarFrameTests
{
    private const double ComponentTolerance = 5;
    private const double ScalarTolerance = 9;

    [Theory]
    [InlineData(0.0)]
    [InlineData(Math.PI / 6.0)]
    [InlineData(Math.PI / 2.0)]
    [InlineData(Math.PI)]
    [InlineData(-Math.PI / 3.0)]
    [InlineData(-2.5)]
    public void EngineAttitudeFacesThePlanarForward(double headingRadians)
    {
        PlanarVector forward = PlanarFrame.Forward(headingRadians);

        Vector3 nose = Vector3.Transform(Vector3.UnitX, PlanarFrame.ToEngineAttitude(headingRadians));

        Assert.Equal(forward.X, nose.X, ComponentTolerance);
        Assert.Equal(0.0, nose.Y, ComponentTolerance);
        Assert.Equal(forward.Z, nose.Z, ComponentTolerance);
    }

    [Theory]
    [InlineData(Math.PI / 6.0)]
    [InlineData(Math.PI / 2.0)]
    [InlineData(-Math.PI / 3.0)]
    public void ABodyReadBackCarriesTheHeadingMirroredInZ(double headingRadians)
    {
        // ToEngineAttitude turns a shape to face the planar heading; an Engine
        // body holding that heading stands at the negated yaw. A silhouette
        // authored against the drawn ship is mirrored on the body, and this
        // records that the two are not the same angle.
        Quaternion attitude = PlanarFrame.ToEngineAttitude(headingRadians);

        Assert.Equal(-headingRadians, PlanarFrame.EngineYawOf(attitude), ScalarTolerance);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(Math.PI / 4.0)]
    [InlineData(-1.0)]
    public void RightIsAQuarterTurnTowardWhichTheHeadingTurns(double headingRadians)
    {
        PlanarVector forward = PlanarFrame.Forward(headingRadians);
        PlanarVector right = PlanarFrame.Right(headingRadians);

        Assert.Equal(0.0, forward.Dot(right), ScalarTolerance);
        Assert.Equal(1.0, right.Magnitude, ScalarTolerance);

        // A quarter turn further along the heading's own opening lands on right.
        Assert.Equal(
            right.X,
            PlanarFrame.Forward(headingRadians + (Math.PI / 2.0)).X,
            ComponentTolerance);
        Assert.Equal(
            right.Z,
            PlanarFrame.Forward(headingRadians + (Math.PI / 2.0)).Z,
            ComponentTolerance);
    }

    [Fact]
    public void AThrusterAtTheBowDrivingTowardStarboardYawsTowardStarboard()
    {
        PlanarVector offset = PlanarFrame.Forward(0.0);
        PlanarVector force = PlanarFrame.Right(0.0);

        double torque = PlanarFrame.YawTorque(offset, force);

        // Positive increases the heading, and the heading opens toward right.
        Assert.Equal(1.0, torque, ScalarTolerance);
    }

    [Fact]
    public void AStarboardEngineDrivingForwardYawsToPort()
    {
        PlanarVector offset = PlanarFrame.Right(0.0);
        PlanarVector force = PlanarFrame.Forward(0.0);

        double torque = PlanarFrame.YawTorque(offset, force);

        Assert.Equal(-1.0, torque, ScalarTolerance);
    }

    [Fact]
    public void BowAndSternOffsetsGiveEqualAndOppositeTorque()
    {
        PlanarVector force = new(0.4, -0.7);
        PlanarVector bow = new(1.5, 0.0);
        PlanarVector stern = new(-1.5, 0.0);

        double bowTorque = PlanarFrame.YawTorque(bow, force);
        double sternTorque = PlanarFrame.YawTorque(stern, force);

        Assert.Equal(-sternTorque, bowTorque, ScalarTolerance);
        Assert.NotEqual(0.0, bowTorque);
    }

    [Fact]
    public void PortAndStarboardOffsetsGiveEqualAndOppositeTorque()
    {
        PlanarVector force = new(0.4, -0.7);
        PlanarVector starboard = new(0.0, 1.5);
        PlanarVector port = new(0.0, -1.5);

        double starboardTorque = PlanarFrame.YawTorque(starboard, force);
        double portTorque = PlanarFrame.YawTorque(port, force);

        Assert.Equal(-portTorque, starboardTorque, ScalarTolerance);
        Assert.NotEqual(0.0, starboardTorque);
    }

    [Fact]
    public void TorqueScalesWithTheLeverArm()
    {
        PlanarVector offset = new(0.0, 1.0);
        PlanarVector force = new(2.0, 0.0);

        Assert.Equal(
            2.0 * PlanarFrame.YawTorque(offset, force),
            PlanarFrame.YawTorque(offset.Scale(2.0), force),
            ScalarTolerance);
    }

    [Fact]
    public void AForceThroughTheCenterOfMassProducesNoTorque()
    {
        PlanarVector offset = new(0.6, -0.25);
        PlanarVector force = offset.Scale(3.0);

        Assert.Equal(0.0, PlanarFrame.YawTorque(offset, force), ScalarTolerance);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(Math.PI / 6.0)]
    [InlineData(-Math.PI / 3.0)]
    public void HeadingOfRecoversTheHeadingADirectionWasTakenFrom(double headingRadians)
    {
        Assert.Equal(
            headingRadians,
            PlanarFrame.HeadingOf(PlanarFrame.Forward(headingRadians)),
            ScalarTolerance);
    }
}
