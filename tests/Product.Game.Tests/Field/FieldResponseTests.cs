using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Field.Tests;

/// <summary>
/// The field response is the sailing handle: it acts on slip against the local
/// flow, never on velocity through space, and it can be wound down to nothing.
/// </summary>
public class FieldResponseTests
{
    private static readonly PlanarVector Flow = new(0.0, 1.75);

    [Fact]
    public void WithNoCouplingTheFieldIsDeclinedEntirely()
    {
        // SpaceTuning currently ships at zero coupling; the sailing response is
        // off until a ship system winds it in.
        FieldResponse response = new(SpaceTuning.Defaults.Field);

        FlightWrench wrench = response.Resolve(
            Ship(PlanarVector.Zero),
            new FieldSample(Flow, 1.0, FieldFlowGradient.Zero, PlanarVector.Zero));

        Assert.Equal(FlightWrench.Zero, wrench);
    }

    [Fact]
    public void AWoundInCouplingCarriesAParkedShipWithTheLocalFlow()
    {
        FieldResponse response = new(Coupled(0.6));

        FlightWrench wrench = response.Resolve(
            Ship(PlanarVector.Zero),
            new FieldSample(Flow, 1.0, FieldFlowGradient.Zero, PlanarVector.Zero));

        Assert.True(wrench.Force.Dot(Flow) > 0.0);
        Assert.Equal(0.0, wrench.TorqueY, 12);
    }

    [Fact]
    public void AShipAlreadyMatchingTheLocalFlowFeelsNoSlipResponse()
    {
        FieldResponse response = new(Coupled(1.0));

        FlightWrench wrench = response.Resolve(
            Ship(Flow),
            new FieldSample(Flow, 1.0, FieldFlowGradient.Zero, PlanarVector.Zero));

        Assert.Equal(0.0, wrench.Force.X, 12);
        Assert.Equal(0.0, wrench.Force.Z, 12);
    }

    [Fact]
    public void DrivingAgainstTheFlowIsResisted()
    {
        FieldResponse response = new(Coupled(1.0));
        PlanarVector fasterThanFlow = Flow.Scale(3.0);

        FlightWrench wrench = response.Resolve(
            Ship(fasterThanFlow),
            new FieldSample(Flow, 1.0, FieldFlowGradient.Zero, PlanarVector.Zero));

        Assert.True(wrench.Force.Dot(Flow) < 0.0);
    }

    [Fact]
    public void TheResponseGrowsProportionallyWithCoupling()
    {
        FieldSample sample = new(Flow, 1.0, FieldFlowGradient.Zero, PlanarVector.Zero);
        FlightBodyState ship = Ship(PlanarVector.Zero);

        double light = new FieldResponse(Coupled(0.3)).Resolve(ship, sample).Force.Magnitude;
        double heavy = new FieldResponse(Coupled(0.6)).Resolve(ship, sample).Force.Magnitude;

        Assert.Equal(2.0 * light, heavy, 9);
    }

    private static FieldTuning Coupled(double coupling) => SpaceTuning.Defaults.Field with
    {
        Coupling = coupling,
    };

    private static FlightBodyState Ship(PlanarVector velocity) => new(
        PlanarVector.Zero,
        0.0,
        velocity,
        0.0);
}
