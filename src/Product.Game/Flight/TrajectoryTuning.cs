using System;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// How far ahead the navigation view draws the hull's line, and how coarsely.
/// </summary>
/// <remarks>
/// The horizon is a legibility choice, not a simulation choice: long enough that
/// a player can see a current they are about to meet, short enough that the line
/// is still a fair reading of a present that keeps changing. Each point is
/// advanced on the Engine's kinematic lane over whole fixed steps, so the line is
/// walked on the same clock the hull moves on.
/// </remarks>
internal sealed record TrajectoryTuning(int SampleCount, int TicksPerSample)
{
    private const int MinimumSamples = 1;
    private const int MaximumSamples = 64;
    private const int MinimumTicksPerSample = 1;
    private const int MaximumTicksPerSample = 64;

    internal TrajectoryTuning Validate()
    {
        if (SampleCount is < MinimumSamples or > MaximumSamples)
        {
            throw new ArgumentOutOfRangeException(nameof(SampleCount));
        }

        if (TicksPerSample is < MinimumTicksPerSample or > MaximumTicksPerSample)
        {
            throw new ArgumentOutOfRangeException(nameof(TicksPerSample));
        }

        return this;
    }
}
