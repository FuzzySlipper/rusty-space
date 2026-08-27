namespace Rusty.Space.Product.Flight;

internal sealed class FlightAccumulator
{
    private const uint RealtimeUpdateKind = 1;
    private const uint DemandStepUpdateKind = 2;
    private const uint ExternalStepUpdateKind = 3;
    private const ulong NanosecondsPerSecond = 1_000_000_000;
    private const ulong StepsPerSecond = 60;
    private const ulong MaximumRetainedSteps = 4;
    private const ulong MaximumRetainedScaledNanoseconds =
        MaximumRetainedSteps * NanosecondsPerSecond;

    private ulong? lastRealtimeObservation;
    private ulong retainedScaledNanoseconds;

    internal FlightStepPlan Prepare(uint kind, ulong observation)
    {
        return kind switch
        {
            RealtimeUpdateKind => PrepareRealtime(observation),
            DemandStepUpdateKind or ExternalStepUpdateKind => new FlightStepPlan(
                lastRealtimeObservation,
                retainedScaledNanoseconds,
                StepCount: 1),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    internal void Commit(FlightStepPlan plan)
    {
        lastRealtimeObservation = plan.LastRealtimeObservation;
        retainedScaledNanoseconds = plan.RetainedScaledNanoseconds;
    }

    internal void Clear()
    {
        lastRealtimeObservation = null;
        retainedScaledNanoseconds = 0;
    }

    private FlightStepPlan PrepareRealtime(ulong observation)
    {
        if (lastRealtimeObservation is not ulong previous)
        {
            return new FlightStepPlan(observation, retainedScaledNanoseconds, StepCount: 0);
        }

        if (observation < previous)
        {
            throw new ArgumentOutOfRangeException(nameof(observation));
        }

        ulong elapsedNanoseconds = observation - previous;
        ulong scaledElapsed = elapsedNanoseconds >= MaximumRetainedScaledNanoseconds / StepsPerSecond
            ? MaximumRetainedScaledNanoseconds
            : elapsedNanoseconds * StepsPerSecond;
        ulong scaledAvailable = Math.Min(
            MaximumRetainedScaledNanoseconds,
            retainedScaledNanoseconds + scaledElapsed);
        uint steps = checked((uint)(scaledAvailable / NanosecondsPerSecond));
        ulong remainder = scaledAvailable % NanosecondsPerSecond;
        return new FlightStepPlan(observation, remainder, steps);
    }
}

internal readonly record struct FlightStepPlan(
    ulong? LastRealtimeObservation,
    ulong RetainedScaledNanoseconds,
    uint StepCount);
