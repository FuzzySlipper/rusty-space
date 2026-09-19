using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Field.Tests;

/// <summary>
/// The stellar field is a smooth, deterministic, purely spatial read of local
/// space weather. These pin that it stays that way: bounded, wake-shaped, and
/// zero where no wake is.
/// </summary>
public class StellarFieldTests
{
    private readonly FieldTuning tuning = SpaceTuning.Defaults.Field;

    [Fact]
    public void AheadOfTheBodyThereIsOnlyTheBackgroundFlow()
    {
        StellarField field = new(tuning);

        FieldSample sample = field.Sample(new PlanarVector(
            tuning.PlanetPosition.X + tuning.WakeCenterBehindPlanet,
            0.0));

        Assert.Equal(tuning.StellarFlow.X, sample.FlowVelocity.X, 12);
        Assert.Equal(tuning.StellarFlow.Z, sample.FlowVelocity.Z, 12);
        Assert.Equal(tuning.StellarIntensity, sample.Intensity, 12);
        Assert.Equal(0.0, sample.Turbulence.X, 12);
        Assert.Equal(0.0, sample.Turbulence.Z, 12);
    }

    [Fact]
    public void InTheWakeBothTheFlowAndTheIntensityBuild()
    {
        StellarField field = new(tuning);

        FieldSample sample = field.Sample(new PlanarVector(
            tuning.PlanetPosition.X - tuning.WakeCenterBehindPlanet,
            0.0));

        Assert.True(sample.Intensity > tuning.StellarIntensity);
        Assert.True(sample.FlowVelocity.Z > tuning.StellarFlow.Z);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(14.0, 0.0)]
    [InlineData(9.0, 3.5)]
    [InlineData(-40.0, 18.0)]
    [InlineData(60.0, -60.0)]
    public void IntensityAlwaysStaysWithinItsDeclaredRange(double x, double z)
    {
        StellarField field = new(tuning);

        double intensity = field.Sample(new PlanarVector(x, z)).Intensity;

        Assert.InRange(intensity, 0.0, 1.0);
        Assert.True(double.IsFinite(intensity));
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(9.0, 0.0)]
    [InlineData(-13.0, 2.0)]
    public void TheFieldIsDeterministicForTheSamePosition(double x, double z)
    {
        StellarField field = new(tuning);
        PlanarVector position = new(x, z);

        FieldSample first = field.Sample(position);
        FieldSample second = field.Sample(position);

        Assert.Equal(first.Intensity, second.Intensity, 15);
        Assert.Equal(first.FlowVelocity.X, second.FlowVelocity.X, 15);
        Assert.Equal(first.FlowVelocity.Z, second.FlowVelocity.Z, 15);
        Assert.Equal(first.Turbulence.X, second.Turbulence.X, 15);
        Assert.Equal(first.Turbulence.Z, second.Turbulence.Z, 15);
    }

    [Fact]
    public void TurbulenceIsContinuousAcrossASmallStepOfPosition()
    {
        // Turbulence the solver would treat as an impulse arrives as a
        // discontinuity; a nudge in position must never produce a jump.
        StellarField field = new(tuning);
        PlanarVector near = new(9.0, 1.0);
        PlanarVector slightlyOn = new(9.001, 1.0);

        PlanarVector first = field.Sample(near).Turbulence;
        PlanarVector second = field.Sample(slightlyOn).Turbulence;

        Assert.True((second - first).Magnitude < 1e-3);
    }
}
