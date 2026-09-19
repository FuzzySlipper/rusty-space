using Rusty.Space.Product.Navigation;
using Xunit;

namespace Rusty.Space.Product.Flight.Tests;

/// <summary>
/// Pins that heading-derived directions have exactly one meaning: the ship's
/// own frame. The sign convention is what later off-center force work stands
/// on, so it is stated as cases rather than left to be inferred.
/// </summary>
public class HeadingFrameTests
{
    private const double Tolerance = 1e-12;

    [Theory]
    [InlineData(0.0)]
    [InlineData(Math.PI / 6.0)]
    [InlineData(Math.PI / 2.0)]
    [InlineData(Math.PI)]
    [InlineData(-Math.PI / 3.0)]
    public void ForwardIsAUnitVectorAtEveryHeading(double headingRadians)
    {
        PlanarVector forward = Body(headingRadians).Forward;

        Assert.Equal(1.0, forward.Magnitude, 12);
    }

    [Fact]
    public void ForwardPointsAlongXAtZeroHeading()
    {
        Assert.Equal(1.0, Body(0.0).Forward.X, Tolerance);
        Assert.Equal(0.0, Body(0.0).Forward.Z, Tolerance);
    }

    [Fact]
    public void ForwardTurnsToPositiveZAtQuarterHeading()
    {
        Assert.Equal(0.0, Body(Math.PI / 2.0).Forward.X, 12);
        Assert.Equal(1.0, Body(Math.PI / 2.0).Forward.Z, Tolerance);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(Math.PI / 6.0)]
    [InlineData(Math.PI / 2.0)]
    [InlineData(-2.0)]
    public void LateralStaysPerpendicularToForward(double headingRadians)
    {
        FlightBodyState body = Body(headingRadians);

        Assert.Equal(0.0, body.Forward.Dot(body.Lateral), 12);
        Assert.Equal(1.0, body.Lateral.Magnitude, 12);
    }

    [Fact]
    public void ForwardAndLateralAreMirrorImagesAcrossTheHeading()
    {
        // A quarter turn either way from a heading must land on opposite
        // laterals; if these ever agree, the frame has collapsed.
        Assert.Equal(
            -Body(Math.PI / 2.0).Lateral.X,
            Body(-Math.PI / 2.0).Lateral.X,
            12);
    }

    [Fact]
    public void PlanarVectorsAddScaleAndProjectConsistently()
    {
        PlanarVector left = new(3.0, -4.0);
        PlanarVector right = new(-1.0, 2.0);

        Assert.Equal(2.0, (left + right).X, Tolerance);
        Assert.Equal(-2.0, (left + right).Z, Tolerance);
        Assert.Equal(4.0, (left - right).X, Tolerance);
        Assert.Equal(-6.0, (left - right).Z, Tolerance);
        Assert.Equal(-11.0, left.Dot(right), Tolerance);
        Assert.Equal(6.0, left.Scale(2.0).X, Tolerance);
        Assert.Equal(5.0, left.Magnitude, Tolerance);
    }

    private static FlightBodyState Body(double headingRadians) => new(
        PlanarVector.Zero,
        headingRadians,
        PlanarVector.Zero,
        0.0);
}
