using System.Text;
using Rusty.Engine;
using Xunit;

namespace Rusty.Space.Product.Flight.Tests;

/// <summary>
/// The mapper turns Engine-admitted mapped input into the closed flight command
/// vocabulary. These pin the coupling handles and the rules every control
/// depends on: an analog stick is re-centered inside its deadzone, devices
/// compose rather than compete, and the declared named intents are the only
/// vocabulary a flight command can come from.
/// </summary>
public class FlightInputMapperTests
{
    [Fact]
    public void TheCoupleAndUncoupleKeysWindTrimInOppositeDirections()
    {
        FlightInputMapper mapper = new();

        double inboard = mapper.Apply(new[] { Digital("space.flight.couple", active: true) })
            .Command.CouplingTrim;
        // Held input is held state: the turn that lets E go and grabs Q carries
        // both edges, and only Q is left standing.
        double outboard = mapper.Apply(new[]
        {
            Digital("space.flight.couple", active: false),
            Digital("space.flight.uncouple", active: true),
        }).Command.CouplingTrim;

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

        double resting = mapper.Apply(new[] { Axis("space.flight.coupling-trim", 0.1f) })
            .Command.CouplingTrim;
        double deflected = mapper.Apply(new[] { Axis("space.flight.coupling-trim", 1.0f) })
            .Command.CouplingTrim;

        Assert.Equal(0.0, resting, 9);
        Assert.Equal(1.0, deflected, 9);
    }

    [Fact]
    public void KeysWindTrimAheadOfTheControllerStick()
    {
        FlightInputMapper mapper = new();

        double commanded = mapper.Apply(new[]
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
            .Apply(new[] { Digital("space.flight.emergency-uncouple", active: true) })
            .Command.EmergencyUncouple);
        Assert.False(mapper
            .Apply(new[] { Digital("space.flight.emergency-uncouple", active: false) })
            .Command.EmergencyUncouple);
    }

    [Fact]
    public void APhysicalKeyWithNoMappedIntentDoesNotFlyTheShip()
    {
        // Space's controls are declared in the product manifest and the Engine
        // maps physical controls onto them before an admitted turn. A raw key
        // fact is not a second vocabulary this mapper speaks, so a host that
        // sends one changes nothing.
        FlightInputMapper mapper = new();

        FlightCommand command = mapper.Apply(new[] { RawKey("KeyW", pressed: true) }).Command;

        Assert.Equal(0.0, command.Throttle, 9);
        Assert.Equal(0.0, command.CouplingTrim, 9);
    }

    [Fact]
    public void AKeyboardAndAGamepadSpeakInTheSameTurn()
    {
        // Devices compose into one command rather than one winning over the
        // other: the trigger drives while the stick steers, and a stray raw key
        // fact adds nothing to either.
        FlightInputMapper mapper = new();

        FlightCommand command = mapper.Apply(new[]
        {
            Digital("space.flight.thrust", active: true),
            Axis("space.flight.turn-analog", -1.0f),
            RawKey("KeyQ", pressed: true),
        }).Command;

        Assert.Equal(1.0, command.Throttle, 9);
        Assert.Equal(-1.0, command.Turn, 9);
        Assert.Equal(0.0, command.CouplingTrim, 9);
    }

    /// <summary>
    /// One admitted turn, read the way the coordinator reads it: held state
    /// moves as it is read, so the next turn starts from what this one left
    /// behind.
    /// </summary>
    private static FlightTurnInput Turn(
        FlightInputMapper mapper,
        params ProductInputEvent[] events) => mapper.Apply(events);

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
