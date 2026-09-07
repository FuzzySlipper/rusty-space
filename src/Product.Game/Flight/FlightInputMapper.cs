using Rusty.Engine;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// Interprets Engine-admitted physical input into the closed Space flight command vocabulary.
/// Engine owns input binding, order, and clear admission; this owner retains only product
/// command meaning so a successful reset can deliberately forget held movement input.
/// </summary>
internal sealed class FlightInputMapper
{
    private const double NeutralCommandIntent = 0.0;
    private const double FullCommandIntent = 1.0;
    private const double LeftTurnIntent = -1.0;
    // Product policy: re-center a small stick wobble, then preserve the full
    // range outside the deadzone so full deflection still means full steering.
    private const double AnalogSteeringDeadzone = 0.15;
    private const double AnalogSteeringRange = FullCommandIntent - AnalogSteeringDeadzone;

    // These are semantic, product-owned input identities. The Engine maps
    // physical controls to them before an admitted update reaches Space.
    private static ReadOnlySpan<byte> ThrustIntent => "space.flight.thrust"u8;
    private static ReadOnlySpan<byte> LeftTurnIntentId => "space.flight.turn-left"u8;
    private static ReadOnlySpan<byte> RightTurnIntentId => "space.flight.turn-right"u8;
    private static ReadOnlySpan<byte> ControllerLeftTurnIntent => "space.flight.turn-left-controller"u8;
    private static ReadOnlySpan<byte> ControllerRightTurnIntent => "space.flight.turn-right-controller"u8;
    private static ReadOnlySpan<byte> AnalogThrustIntent => "space.flight.thrust-analog"u8;
    private static ReadOnlySpan<byte> AnalogTurnIntent => "space.flight.turn-analog"u8;
    private static ReadOnlySpan<byte> ResetIntent => "space.flight.reset"u8;
    private static ReadOnlySpan<byte> AbortIntent => "space.flight.abort"u8;

    private FlightInputState state = FlightInputState.Neutral;

    internal FlightInputPlan Prepare(ReadOnlySpan<ProductInputEvent> input)
    {
        FlightInputState stagedState = state;
        bool resetRequested = false;
        bool faultRequested = false;

        // A mapped event is the authoritative Engine input path. Raw physical
        // key facts remain a compatibility fallback for hosts that have not
        // yet declared Space's mappings; they never override semantic input
        // from the same admitted turn.
        bool hasSemanticFlightInput = false;
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.Kind == InputEventKind.Clear)
            {
                stagedState = FlightInputState.Neutral;
                continue;
            }

            if (inputEvent.Kind == InputEventKind.MappedAxis)
            {
                ReadOnlySpan<byte> axisIntent = inputEvent.Intent.Span;
                if (axisIntent.SequenceEqual(AnalogThrustIntent))
                {
                    hasSemanticFlightInput = true;
                    stagedState = stagedState with { AnalogThrust = NormalizeAnalogThrust(inputEvent.X) };
                }
                else if (axisIntent.SequenceEqual(AnalogTurnIntent))
                {
                    hasSemanticFlightInput = true;
                    stagedState = stagedState with { AnalogTurn = NormalizeAnalogTurn(inputEvent.X) };
                }

                continue;
            }

            if (inputEvent.Kind != InputEventKind.MappedDigital)
            {
                continue;
            }

            ReadOnlySpan<byte> intent = inputEvent.Intent.Span;
            if (intent.SequenceEqual(ThrustIntent))
            {
                hasSemanticFlightInput = true;
                stagedState = stagedState with { KeyboardThrustHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(LeftTurnIntentId))
            {
                hasSemanticFlightInput = true;
                stagedState = stagedState with { KeyboardLeftHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(RightTurnIntentId))
            {
                hasSemanticFlightInput = true;
                stagedState = stagedState with { KeyboardRightHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(ControllerLeftTurnIntent))
            {
                hasSemanticFlightInput = true;
                stagedState = stagedState with { ControllerLeftHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(ControllerRightTurnIntent))
            {
                hasSemanticFlightInput = true;
                stagedState = stagedState with { ControllerRightHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(ResetIntent))
            {
                hasSemanticFlightInput = true;
                // Press mappings are one-shot Engine facts, unlike the raw
                // fallback's physical key edges. Do not retain a semantic
                // reset as held: the launcher intentionally has no release
                // mapping for this action.
                if (IsPressed(inputEvent))
                {
                    resetRequested = true;
                }
            }
            else if (intent.SequenceEqual(AbortIntent))
            {
                hasSemanticFlightInput = true;
                if (IsPressed(inputEvent))
                {
                    faultRequested = true;
                }
            }
        }

        if (!hasSemanticFlightInput)
        {
            foreach (ProductInputEvent inputEvent in input)
            {
                if (inputEvent.Kind == InputEventKind.Clear)
                {
                    stagedState = FlightInputState.Neutral;
                    continue;
                }

                if (inputEvent.Kind != InputEventKind.Key
                    || inputEvent.Edge is not (InputEdge.Pressed or InputEdge.Released))
                {
                    continue;
                }

                bool pressed = inputEvent.Edge == InputEdge.Pressed;
                ReadOnlySpan<byte> label = inputEvent.Label.Span;
                if (label.SequenceEqual("KeyW"u8))
                {
                    stagedState = stagedState with { KeyboardThrustHeld = pressed };
                }
                else if (label.SequenceEqual("KeyA"u8))
                {
                    stagedState = stagedState with { KeyboardLeftHeld = pressed };
                }
                else if (label.SequenceEqual("KeyD"u8))
                {
                    stagedState = stagedState with { KeyboardRightHeld = pressed };
                }
                else if (label.SequenceEqual("KeyR"u8))
                {
                    if (pressed && !stagedState.ResetHeld)
                    {
                        resetRequested = true;
                    }

                    stagedState = stagedState with { ResetHeld = pressed };
                }
                else if (label.SequenceEqual("KeyF"u8))
                {
                    if (pressed && !stagedState.FaultHeld)
                    {
                        faultRequested = true;
                    }

                    stagedState = stagedState with { FaultHeld = pressed };
                }
            }
        }

        return new FlightInputPlan(stagedState, ToCommand(stagedState), resetRequested, faultRequested);
    }

    internal void Commit(FlightInputPlan plan) => state = plan.State;

    internal void Reset() => state = FlightInputState.Neutral;

    private static bool IsDigitalActive(ProductInputEvent inputEvent) => inputEvent.X > 0.0f;

    private static double NormalizeAnalogThrust(float value) => Math.Clamp(
        (double)value,
        NeutralCommandIntent,
        FullCommandIntent);

    private static double NormalizeAnalogTurn(float value)
    {
        double clamped = Math.Clamp((double)value, LeftTurnIntent, FullCommandIntent);
        double magnitude = Math.Abs(clamped);
        if (magnitude <= AnalogSteeringDeadzone)
        {
            return NeutralCommandIntent;
        }

        double remappedMagnitude = (magnitude - AnalogSteeringDeadzone) / AnalogSteeringRange;
        return Math.CopySign(Math.Clamp(remappedMagnitude, NeutralCommandIntent, FullCommandIntent), clamped);
    }

    private static bool IsPressed(ProductInputEvent inputEvent) => inputEvent.Phase == InputPhase.Pressed
        && IsDigitalActive(inputEvent);

    private static FlightCommand ToCommand(FlightInputState value)
    {
        // Keyboard thrust combines with the trigger and therefore reaches full
        // output while W is held. Keyboard steering is exclusive while A/D is
        // held; otherwise bumpers take priority over the analog stick.
        double throttle = Math.Clamp(
            value.AnalogThrust + (value.KeyboardThrustHeld ? FullCommandIntent : NeutralCommandIntent),
            NeutralCommandIntent,
            FullCommandIntent);
        bool keyboardSteeringHeld = value.KeyboardLeftHeld || value.KeyboardRightHeld;
        bool controllerSteeringHeld = value.ControllerLeftHeld || value.ControllerRightHeld;
        double turn = keyboardSteeringHeld
            ? DigitalTurn(value.KeyboardLeftHeld, value.KeyboardRightHeld)
            : controllerSteeringHeld
                ? DigitalTurn(value.ControllerLeftHeld, value.ControllerRightHeld)
                : value.AnalogTurn;
        return new FlightCommand(
            throttle,
            turn);
    }

    private static double DigitalTurn(bool leftHeld, bool rightHeld) => leftHeld == rightHeld
        ? NeutralCommandIntent
        : leftHeld ? LeftTurnIntent : FullCommandIntent;
}

internal readonly record struct FlightInputState(
    double AnalogThrust,
    double AnalogTurn,
    bool KeyboardThrustHeld,
    bool KeyboardLeftHeld,
    bool KeyboardRightHeld,
    bool ControllerLeftHeld,
    bool ControllerRightHeld,
    bool ResetHeld,
    bool FaultHeld)
{
    internal static FlightInputState Neutral { get; } = new(
        0.0,
        0.0,
        false,
        false,
        false,
        false,
        false,
        false,
        false);
}

internal readonly record struct FlightInputPlan(
    FlightInputState State,
    FlightCommand Command,
    bool ResetRequested,
    bool FaultRequested);
