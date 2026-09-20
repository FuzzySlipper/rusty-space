using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Field.Tests;

/// <summary>
/// The orbital well is deliberately not orbital mechanics: a bounded inward
/// pull plus a steady swirl, both fading with distance, and nothing at the
/// singular centre.
/// </summary>
public class OrbitalGravityTests
{
    private readonly OrbitalGravityTuning tuning = SpaceTuning.Defaults.Orbital;

    [Fact]
    public void ThePullBendsTowardTheVisibleBody()
    {
        OrbitalGravity gravity = new(tuning);
        PlanarVector inward = (tuning.Center - PlanarVector.Zero).Scale(1.0 / tuning.Center.X);

        FlightWrench wrench = gravity.Resolve(PlanarVector.Zero, mass: 2.0);

        Assert.True(wrench.Force.Dot(inward) > 0.0);
    }

    [Fact]
    public void ThePullFadesWithDistance()
    {
        OrbitalGravity gravity = new(tuning);

        double near = gravity.Resolve(tuning.Center - new PlanarVector(tuning.Radius, 0.0), 2.0)
            .Force.Magnitude;
        double far = gravity.Resolve(tuning.Center - new PlanarVector(tuning.Radius * 4.0, 0.0), 2.0)
            .Force.Magnitude;

        Assert.True(near > far);
    }

    [Fact]
    public void TheWellAddsATurnAroundTheBodyRatherThanOnlyAStraightTow()
    {
        OrbitalGravity gravity = new(tuning);

        FlightWrench wrench = gravity.Resolve(
            tuning.Center + new PlanarVector(0.0, tuning.Radius),
            mass: 2.0);

        // Straight toward the centre would be purely -Z; the swirl adds the
        // sideways component that lets a coasting ship bend into an arc.
        Assert.True(wrench.Force.X > 0.0);
        Assert.True(wrench.Force.Z < 0.0);
        Assert.Equal(0.0, wrench.YawTorque, 12);
    }

    [Fact]
    public void AtTheCentreOfTheWellThereIsNoSingularYank()
    {
        OrbitalGravity gravity = new(tuning);

        Assert.Equal(FlightWrench.Zero, gravity.Resolve(tuning.Center, mass: 2.0));
    }

    [Fact]
    public void ThePullStaysBoundedWhereTheFalloffIsStrongest()
    {
        OrbitalGravity gravity = new(tuning);

        FlightWrench wrench = gravity.Resolve(
            tuning.Center - new PlanarVector(0.05, 0.0),
            mass: 2.0);

        Assert.True(double.IsFinite(wrench.Force.X));
        Assert.True(wrench.Force.Magnitude <= tuning.MaximumForce + 1e-9);
    }
}
