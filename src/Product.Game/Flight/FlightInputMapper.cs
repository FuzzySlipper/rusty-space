using Rusty.Engine;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// Interprets Engine-admitted physical input into the closed Space flight command vocabulary.
/// Engine owns input binding, order, and clear admission; this owner retains only product
/// command meaning so a successful reset can deliberately forget held movement input.
/// </summary>
internal sealed class FlightInputMapper
{
    private const uint PhysicalKeyInputKind = 1;
    private const uint ClearInputKind = 7;
    private const uint PressedInputEdge = 1;
    private const uint ReleasedInputEdge = 2;
    private const double NeutralCommandIntent = 0.0;
    private const double FullCommandIntent = 1.0;
    private const double LeftTurnIntent = -1.0;

    private FlightInputState state = FlightInputState.Neutral;

    internal FlightInputPlan Prepare(ReadOnlySpan<ProductInputEvent> input)
    {
        FlightInputState stagedState = state;
        bool resetRequested = false;
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.Kind == ClearInputKind)
            {
                stagedState = FlightInputState.Neutral;
                continue;
            }

            if (inputEvent.Kind != PhysicalKeyInputKind
                || inputEvent.Edge is not (PressedInputEdge or ReleasedInputEdge))
            {
                continue;
            }

            bool pressed = inputEvent.Edge == PressedInputEdge;
            ReadOnlySpan<byte> label = inputEvent.Label.Span;
            if (label.SequenceEqual("KeyW"u8))
            {
                stagedState = stagedState with { ThrustHeld = pressed };
            }
            else if (label.SequenceEqual("KeyA"u8))
            {
                stagedState = stagedState with { LeftHeld = pressed };
            }
            else if (label.SequenceEqual("KeyD"u8))
            {
                stagedState = stagedState with { RightHeld = pressed };
            }
            else if (label.SequenceEqual("KeyR"u8))
            {
                if (pressed && !stagedState.ResetHeld)
                {
                    resetRequested = true;
                }

                stagedState = stagedState with { ResetHeld = pressed };
            }
        }

        return new FlightInputPlan(stagedState, ToCommand(stagedState), resetRequested);
    }

    internal void Commit(FlightInputPlan plan) => state = plan.State;

    internal void Reset() => state = FlightInputState.Neutral;

    private static FlightCommand ToCommand(FlightInputState value)
    {
        double turn = value.LeftHeld == value.RightHeld
            ? NeutralCommandIntent
            : value.LeftHeld ? LeftTurnIntent : FullCommandIntent;
        return new FlightCommand(
            value.ThrustHeld ? FullCommandIntent : NeutralCommandIntent,
            turn);
    }
}

internal readonly record struct FlightInputState(
    bool ThrustHeld,
    bool LeftHeld,
    bool RightHeld,
    bool ResetHeld)
{
    internal static FlightInputState Neutral { get; } = new(false, false, false, false);
}

internal readonly record struct FlightInputPlan(
    FlightInputState State,
    FlightCommand Command,
    bool ResetRequested);
