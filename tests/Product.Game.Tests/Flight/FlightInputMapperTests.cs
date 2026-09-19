using System.Text;
using Rusty.Engine;
using Xunit;

namespace Rusty.Space.Product.Flight.Tests;

/// <summary>
/// The mapper turns Engine-admitted physical input into the closed flight
/// command vocabulary. These pin the coupling handles and the two rules every
/// control depends on: an analog stick is re-centered inside its deadzone, and
/// raw physical keys only speak when no semantic input arrived.
/// </summary>
public class FlightInputMapperTests
{
    [Fact]
    public void TheCoupleAndUncoupleKeysWindTrimInOppositeDirections()
    {
        FlightInputMapper mapper = new();

        double inboard = mapper.Prepare(new[] { Digital("space.flight.couple", active: true) })
            .Command.CouplingTrim;
        double outboard = mapper.Prepare(new[] { Digital("space.flight.uncouple", active: true) })
            .Command.CouplingTrim;

        Assert.Equal(1.0, inboard, 9);
        Assert.Equal(-1.0, outboard, 9);
    }

    [Fact]
    public void ATrimStickInsideTheDeadzoneLeavesTheSettingAlone()
    {
        // A controller stick at rest is not exactly centered, and coupling
        // integrates over time, so an undezioned wobble would creep the setting
        // away from wherever the player left it.
        FlightInputMapper mapper = new();

        double resting = mapper.Prepare(new[] { Axis("space.flight.coupling-trim", 0.1f) })
            .Command.CouplingTrim;
        double deflected = mapper.Prepare(new[] { Axis("space.flight.coupling-trim", 1.0f) })
            .Command.CouplingTrim;

        Assert.Equal(0.0, resting, 9);
        Assert.Equal(1.0, deflected, 9);
    }

    [Fact]
    public void KeysWindTrimAheadOfTheControllerStick()
    {
        FlightInputMapper mapper = new();

        double commanded = mapper.Prepare(new[]
        {
            Digital("space.flight.uncouple", active: true),
            Axis("space.flight.coupling-trim", 1.0f),
        }).Command.CouplingTrim;

        Assert.Equal(-1.0, commanded, 9);
    }

    [Fact]
    public void TheAttitudeHoldSwitchTogglesOnEachPressAndHoldsItsSetting()
    {
        FlightInputMapper mapper = new();

        Assert.False(Turn(mapper, Digital("space.flight.stabilizer", active: true))
            .Command.StabilizerEnabled);

        // A press is one-shot, so the switch reports on the turn it is thrown
        // and the setting stands on the turns between throws.
        Assert.False(Turn(mapper).Command.StabilizerEnabled);

        Assert.True(Turn(mapper, Digital("space.flight.stabilizer", active: true))
            .Command.StabilizerEnabled);
    }

    [Fact]
    public void TheEmergencyUncoupleIsReportedWhileHeldAndClearOnceReleased()
    {
        FlightInputMapper mapper = new();

        Assert.True(mapper
            .Prepare(new[] { Digital("space.flight.emergency-uncouple", active: true) })
            .Command.EmergencyUncouple);
        Assert.False(mapper
            .Prepare(new[] { Digital("space.flight.emergency-uncouple", active: false) })
            .Command.EmergencyUncouple);
    }

    [Fact]
    public void RawKeysStillFlyTheShipWhenNoSemanticInputArrives()
    {
        FlightInputMapper mapper = new();

        FlightCommand thrusting = mapper.Prepare(new[] { RawKey("KeyW", pressed: true) }).Command;
        FlightCommand trimmedOut = mapper.Prepare(new[] { RawKey("KeyQ", pressed: true) }).Command;

        Assert.Equal(1.0, thrusting.Throttle, 9);
        Assert.Equal(-1.0, trimmedOut.CouplingTrim, 9);
    }

    [Fact]
    public void ASemanticTurnDoesNotAlsoFireTheRawKeyFallback()
    {
        // The raw path exists for hosts that have not declared Space's
        // mappings. Where the mappings exist it must not add a second voice.
        FlightInputMapper mapper = new();

        FlightCommand command = mapper.Prepare(new[]
        {
            Digital("space.flight.thrust", active: true),
            RawKey("KeyQ", pressed: true),
        }).Command;

        Assert.Equal(1.0, command.Throttle, 9);
        Assert.Equal(0.0, command.CouplingTrim, 9);
    }

    /// <summary>
    /// One admitted turn: read the input, then commit it as the coordinator
    /// does, so the next turn starts from what this one left behind.
    /// </summary>
    private static FlightInputPlan Turn(
        FlightInputMapper mapper,
        params ProductInputEvent[] events)
    {
        FlightInputPlan plan = mapper.Prepare(events);
        mapper.Commit(plan);
        return plan;
    }

    private static ProductInputEvent Digital(string intent, bool active) => new()
    {
        Kind = InputEventKind.MappedDigital,
        Phase = InputPhase.Pressed,
        X = active ? 1f : 0f,
        Intent = Encoding.UTF8.GetBytes(intent),
    };

    private static ProductInputEvent Axis(string intent, float value) => new()
    {
        Kind = InputEventKind.MappedAxis,
        Phase = InputPhase.Pressed,
        X = value,
        Intent = Encoding.UTF8.GetBytes(intent),
    };

    private static ProductInputEvent RawKey(string label, bool pressed) => new()
    {
        Kind = InputEventKind.Key,
        Edge = pressed ? InputEdge.Pressed : InputEdge.Released,
        X = pressed ? 1f : 0f,
        Label = Encoding.UTF8.GetBytes(label),
    };
}
