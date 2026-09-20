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
    private const double FullUncoupleTrimIntent = -1.0;
    // Product policy: re-center a small stick wobble, then preserve the full
    // range outside the deadzone so full deflection still means full travel.
    // Steering and coupling trim are both analog sticks and share the policy;
    // without it a resting stick would creep the coupling off its setting.
    private const double AnalogAxisDeadzone = 0.15;
    private const double AnalogAxisRange = FullCommandIntent - AnalogAxisDeadzone;

    // These are semantic, product-owned input identities. The Engine maps
    // physical controls to them before an admitted update reaches Space.
    private static ReadOnlySpan<byte> ThrustIntent => "space.flight.thrust"u8;
    private static ReadOnlySpan<byte> LeftTurnIntentId => "space.flight.turn-left"u8;
    private static ReadOnlySpan<byte> RightTurnIntentId => "space.flight.turn-right"u8;
    private static ReadOnlySpan<byte> ControllerLeftTurnIntent => "space.flight.turn-left-controller"u8;
    private static ReadOnlySpan<byte> ControllerRightTurnIntent => "space.flight.turn-right-controller"u8;
    private static ReadOnlySpan<byte> AnalogThrustIntent => "space.flight.thrust-analog"u8;
    private static ReadOnlySpan<byte> AnalogTurnIntent => "space.flight.turn-analog"u8;
    private static ReadOnlySpan<byte> AnalogCouplingTrimIntent => "space.flight.coupling-trim"u8;
    private static ReadOnlySpan<byte> CoupleIntent => "space.flight.couple"u8;
    private static ReadOnlySpan<byte> UncoupleIntent => "space.flight.uncouple"u8;
    private static ReadOnlySpan<byte> EmergencyUncoupleIntent => "space.flight.emergency-uncouple"u8;
    private static ReadOnlySpan<byte> StabilizerIntent => "space.flight.stabilizer"u8;
    private static ReadOnlySpan<byte> ResetIntent => "space.flight.reset"u8;
    private static ReadOnlySpan<byte> AbortIntent => "space.flight.abort"u8;

    private FlightInputState state = FlightInputState.Neutral;

    /// <summary>
    /// Folds one admitted turn's events into the held control state and reports
    /// what they amount to. Held input lives here and moves as it is read: the
    /// Engine has already admitted these events, so there is no acceptance
    /// transaction left to run afterwards.
    /// </summary>
    internal FlightTurnInput Apply(ReadOnlySpan<ProductInputEvent> input)
    {
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
                state = FlightInputState.Neutral;
                continue;
            }

            if (inputEvent.Kind == InputEventKind.MappedAxis)
            {
                ReadOnlySpan<byte> axisIntent = inputEvent.Intent.Span;
                if (axisIntent.SequenceEqual(AnalogThrustIntent))
                {
                    hasSemanticFlightInput = true;
                    state = state with { AnalogThrust = NormalizeAnalogThrust(inputEvent.X) };
                }
                else if (axisIntent.SequenceEqual(AnalogTurnIntent))
                {
                    hasSemanticFlightInput = true;
                    state = state with { AnalogTurn = NormalizeAnalogAxis(inputEvent.X) };
                }
                else if (axisIntent.SequenceEqual(AnalogCouplingTrimIntent))
                {
                    hasSemanticFlightInput = true;
                    state = state with { AnalogCouplingTrim = NormalizeAnalogAxis(inputEvent.X) };
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
                state = state with { KeyboardThrustHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(LeftTurnIntentId))
            {
                hasSemanticFlightInput = true;
                state = state with { KeyboardLeftHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(RightTurnIntentId))
            {
                hasSemanticFlightInput = true;
                state = state with { KeyboardRightHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(ControllerLeftTurnIntent))
            {
                hasSemanticFlightInput = true;
                state = state with { ControllerLeftHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(ControllerRightTurnIntent))
            {
                hasSemanticFlightInput = true;
                state = state with { ControllerRightHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(CoupleIntent))
            {
                hasSemanticFlightInput = true;
                state = state with { CoupleHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(UncoupleIntent))
            {
                hasSemanticFlightInput = true;
                state = state with { UncoupleHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(EmergencyUncoupleIntent))
            {
                hasSemanticFlightInput = true;
                state = state with { EmergencyUncoupleHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(StabilizerIntent))
            {
                hasSemanticFlightInput = true;
                // The stabilizer is a switch, not a held key: each press flips
                // the attitude hold. Engine press phases are one-shot, so no
                // release mapping is needed to stop a repeated flip.
                if (IsPressed(inputEvent))
                {
                    state = state with
                    {
                        StabilizerEnabled = !state.StabilizerEnabled,
                    };
                }
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
                    state = FlightInputState.Neutral;
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
                    state = state with { KeyboardThrustHeld = pressed };
                }
                else if (label.SequenceEqual("KeyA"u8))
                {
                    state = state with { KeyboardLeftHeld = pressed };
                }
                else if (label.SequenceEqual("KeyD"u8))
                {
                    state = state with { KeyboardRightHeld = pressed };
                }
                else if (label.SequenceEqual("KeyQ"u8))
                {
                    state = state with { UncoupleHeld = pressed };
                }
                else if (label.SequenceEqual("KeyE"u8))
                {
                    state = state with { CoupleHeld = pressed };
                }
                else if (label.SequenceEqual("KeyX"u8))
                {
                    state = state with { EmergencyUncoupleHeld = pressed };
                }
                else if (label.SequenceEqual("KeyT"u8))
                {
                    // Raw physical edges repeat while a key goes down, so the
                    // flip is guarded by the held flag the semantic path gets
                    // for free from its press phase.
                    if (pressed && !state.StabilizerKeyHeld)
                    {
                        state = state with
                        {
                            StabilizerEnabled = !state.StabilizerEnabled,
                        };
                    }

                    state = state with { StabilizerKeyHeld = pressed };
                }
                else if (label.SequenceEqual("KeyR"u8))
                {
                    if (pressed && !state.ResetHeld)
                    {
                        resetRequested = true;
                    }

                    state = state with { ResetHeld = pressed };
                }
                else if (label.SequenceEqual("KeyF"u8))
                {
                    if (pressed && !state.FaultHeld)
                    {
                        faultRequested = true;
                    }

                    state = state with { FaultHeld = pressed };
                }
            }
        }

        return new FlightTurnInput(ToCommand(state), resetRequested, faultRequested);
    }

    internal void Reset() => state = FlightInputState.Neutral;

    private static bool IsDigitalActive(ProductInputEvent inputEvent) => inputEvent.X > 0.0f;

    private static double NormalizeAnalogThrust(float value) => Math.Clamp(
        (double)value,
        NeutralCommandIntent,
        FullCommandIntent);

    private static double NormalizeAnalogAxis(float value)
    {
        double clamped = Math.Clamp((double)value, LeftTurnIntent, FullCommandIntent);
        double magnitude = Math.Abs(clamped);
        if (magnitude <= AnalogAxisDeadzone)
        {
            return NeutralCommandIntent;
        }

        double remappedMagnitude = (magnitude - AnalogAxisDeadzone) / AnalogAxisRange;
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
        // Q and E wind the coupling actuator at full rate; the controller stick
        // does the same whenever neither key is held. Both are intents: the
        // actuator decides how fast the level actually follows.
        double digitalTrim = (value.CoupleHeld ? FullCommandIntent : NeutralCommandIntent)
            + (value.UncoupleHeld ? FullUncoupleTrimIntent : NeutralCommandIntent);
        return new FlightCommand(
            throttle,
            turn,
            digitalTrim != NeutralCommandIntent ? digitalTrim : value.AnalogCouplingTrim,
            value.StabilizerEnabled,
            value.EmergencyUncoupleHeld);
    }

    private static double DigitalTurn(bool leftHeld, bool rightHeld) => leftHeld == rightHeld
        ? NeutralCommandIntent
        : leftHeld ? LeftTurnIntent : FullCommandIntent;
}

internal readonly record struct FlightInputState(
    double AnalogThrust,
    double AnalogTurn,
    double AnalogCouplingTrim,
    bool KeyboardThrustHeld,
    bool KeyboardLeftHeld,
    bool KeyboardRightHeld,
    bool ControllerLeftHeld,
    bool ControllerRightHeld,
    bool CoupleHeld,
    bool UncoupleHeld,
    bool EmergencyUncoupleHeld,
    bool StabilizerEnabled,
    bool StabilizerKeyHeld,
    bool ResetHeld,
    bool FaultHeld)
{
    /// <summary>
    /// What the controls read before anything is touched. The attitude hold is
    /// something a stock hull does for itself rather than something the pilot
    /// must remember to switch on, so it starts engaged.
    /// </summary>
    internal static FlightInputState Neutral { get; } = new(
        AnalogThrust: 0.0,
        AnalogTurn: 0.0,
        AnalogCouplingTrim: 0.0,
        KeyboardThrustHeld: false,
        KeyboardLeftHeld: false,
        KeyboardRightHeld: false,
        ControllerLeftHeld: false,
        ControllerRightHeld: false,
        CoupleHeld: false,
        UncoupleHeld: false,
        EmergencyUncoupleHeld: false,
        StabilizerEnabled: true,
        StabilizerKeyHeld: false,
        ResetHeld: false,
        FaultHeld: false);
}

/// <summary>
/// What one admitted turn's input amounted to: the command the pilot is asking
/// for now, plus the one-shot actions the turn carried.
/// </summary>
internal readonly record struct FlightTurnInput(
    FlightCommand Command,
    bool ResetRequested,
    bool FaultRequested);
