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

        // Space's controls are declared named intents (Product.Game.csproj),
        // and the Engine maps physical controls to them before an admitted
        // turn reaches the product. Physical labels are not a second
        // vocabulary this owner speaks.
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
                        state = state with { AnalogThrust = NormalizeAnalogThrust(inputEvent.X) };
                }
                else if (axisIntent.SequenceEqual(AnalogTurnIntent))
                {
                        state = state with { AnalogTurn = NormalizeAnalogAxis(inputEvent.X) };
                }
                else if (axisIntent.SequenceEqual(AnalogCouplingTrimIntent))
                {
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
                state = state with { KeyboardThrustHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(LeftTurnIntentId))
            {
                state = state with { KeyboardLeftHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(RightTurnIntentId))
            {
                state = state with { KeyboardRightHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(ControllerLeftTurnIntent))
            {
                state = state with { ControllerLeftHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(ControllerRightTurnIntent))
            {
                state = state with { ControllerRightHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(CoupleIntent))
            {
                state = state with { CoupleHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(UncoupleIntent))
            {
                state = state with { UncoupleHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(EmergencyUncoupleIntent))
            {
                state = state with { EmergencyUncoupleHeld = IsDigitalActive(inputEvent) };
            }
            else if (intent.SequenceEqual(StabilizerIntent))
            {
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
                // A press mapping is a one-shot Engine fact: the launcher
                // intentionally declares no release for this action, so the
                // reset is reported for the turn it arrives in and not held.
                if (IsPressed(inputEvent))
                {
                    resetRequested = true;
                }
            }
            else if (intent.SequenceEqual(AbortIntent))
            {
                if (IsPressed(inputEvent))
                {
                    faultRequested = true;
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
    bool StabilizerEnabled)
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
        StabilizerEnabled: true);
}

/// <summary>
/// What one admitted turn's input amounted to: the command the pilot is asking
/// for now, plus the one-shot actions the turn carried.
/// </summary>
internal readonly record struct FlightTurnInput(
    FlightCommand Command,
    bool ResetRequested,
    bool FaultRequested);
