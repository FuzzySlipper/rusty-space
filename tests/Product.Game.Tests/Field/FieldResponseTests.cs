using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Field.Tests;

/// <summary>
/// The field response is the sailing handle: it acts on slip against the local
/// flow, never on velocity through space, and the ship can wind it down to
/// nothing. Coupling arrives as the ship's live setting and mass as the ship's
/// real mass, so neither is a second copy of a constant.
/// </summary>
public class FieldResponseTests
{
    private const double ShipMass = 2.0;
    private const double FullyCoupled = 1.0;
    private static readonly PlanarVector Flow = new(0.0, 1.75);

    [Fact]
    public void WithNoCouplingTheFieldIsDeclinedEntirely()
    {
        // At zero coupling the wake, the gradient response, and turbulence all
        // contribute nothing, so a ship coasting through the field keeps the
        // velocity it arrived with.
        FieldResponse response = new(SpaceTuning.Defaults.Field);

        FlightWrench wrench = response.Resolve(
            Ship(PlanarVector.Zero),
            new FieldSample(Flow, 1.0, FieldFlowGradient.Zero, PlanarVector.Zero),
            coupling: 0.0,
            mass: ShipMass);

        Assert.Equal(FlightWrench.Zero, wrench);
    }

    [Fact]
    public void AWoundInCouplingCarriesAParkedShipWithTheLocalFlow()
    {
        FieldResponse response = new(SpaceTuning.Defaults.Field);

        FlightWrench wrench = response.Resolve(
            Ship(PlanarVector.Zero),
            new FieldSample(Flow, 1.0, FieldFlowGradient.Zero, PlanarVector.Zero),
            coupling: 0.6,
            mass: ShipMass);

        Assert.True(wrench.Force.Dot(Flow) > 0.0);
        Assert.Equal(0.0, wrench.TorqueY, 12);
    }

    [Fact]
    public void AShipAlreadyMatchingTheLocalFlowFeelsNoSlipResponse()
    {
        FieldResponse response = new(SpaceTuning.Defaults.Field);

        FlightWrench wrench = response.Resolve(
            Ship(Flow),
            new FieldSample(Flow, 1.0, FieldFlowGradient.Zero, PlanarVector.Zero),
            coupling: FullyCoupled,
            mass: ShipMass);

        Assert.Equal(0.0, wrench.Force.X, 12);
        Assert.Equal(0.0, wrench.Force.Z, 12);
    }

    [Fact]
    public void DrivingAgainstTheFlowIsResisted()
    {
        FieldResponse response = new(SpaceTuning.Defaults.Field);
        PlanarVector fasterThanFlow = Flow.Scale(3.0);

        FlightWrench wrench = response.Resolve(
            Ship(fasterThanFlow),
            new FieldSample(Flow, 1.0, FieldFlowGradient.Zero, PlanarVector.Zero),
            coupling: FullyCoupled,
            mass: ShipMass);

        Assert.True(wrench.Force.Dot(Flow) < 0.0);
    }

    [Fact]
    public void TheResponseGrowsProportionallyWithCoupling()
    {
        FieldSample sample = new(Flow, 1.0, FieldFlowGradient.Zero, PlanarVector.Zero);
        FlightBodyState ship = Ship(PlanarVector.Zero);
        FieldResponse response = new(SpaceTuning.Defaults.Field);

        double light = response.Resolve(ship, sample, 0.3, ShipMass).Force.Magnitude;
        double heavy = response.Resolve(ship, sample, 0.6, ShipMass).Force.Magnitude;

        Assert.Equal(2.0 * light, heavy, 9);
    }

    [Fact]
    public void TheResponseScalesWithTheShipsRealMassRatherThanAnAssumedOne()
    {
        // Every force source scales by the mass the body actually has, so a
        // hull that gains or loses mass moves the field response with it.
        FieldSample sample = new(Flow, 1.0, FieldFlowGradient.Zero, PlanarVector.Zero);
        FlightBodyState ship = Ship(PlanarVector.Zero);
        FieldResponse response = new(SpaceTuning.Defaults.Field);

        double light = response.Resolve(ship, sample, 0.6, ShipMass).Force.Magnitude;
        double heavier = response.Resolve(ship, sample, 0.6, ShipMass * 2.0).Force.Magnitude;

        Assert.Equal(2.0 * light, heavier, 9);
    }

    [Fact]
    public void AFieldWithNoIntensityCouplesNothingHoweverWoundIn()
    {
        FieldResponse response = new(SpaceTuning.Defaults.Field);

        FlightWrench wrench = response.Resolve(
            Ship(PlanarVector.Zero),
            new FieldSample(Flow, 0.0, FieldFlowGradient.Zero, PlanarVector.Zero),
            coupling: FullyCoupled,
            mass: ShipMass);

        Assert.Equal(0.0, wrench.Force.X, 12);
        Assert.Equal(0.0, wrench.Force.Z, 12);
    }

    private static FlightBodyState Ship(PlanarVector velocity) => new(
        PlanarVector.Zero,
        0.0,
        velocity,
        0.0);
}
