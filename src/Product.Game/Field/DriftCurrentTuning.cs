using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Field;

/// <summary>
/// One readable band of moving space-weather: a finite river with a smooth
/// lateral falloff, soft ends, and a gentle deterministic swell along its
/// length. Wide/slow/minor versus narrow/fast/powerful bands share this
/// shape and differ only in tuning.
/// </summary>
internal sealed record DriftCurrentTuning(
    PlanarVector Center,
    PlanarVector Direction,
    double Width,
    double Length,
    double FlowSpeed,
    double ResponseGain,
    double WaveAmplitude,
    double WaveFrequency,
    double MaximumForce)
{
    private const double MinimumNonNegativeMagnitude = 0.0;
    private const double MinimumPositiveMagnitude = 0.0;
    private const double MinimumDirectionMagnitude = 1e-6;
    private const double MaximumWaveAmplitude = 1.0;

    internal DriftCurrentTuning Validate()
    {
        ValidateFinite(Center, nameof(Center));
        ValidateFinite(Direction, nameof(Direction));
        if (Direction.Magnitude < MinimumDirectionMagnitude)
        {
            throw new ArgumentOutOfRangeException(nameof(Direction));
        }

        ValidatePositiveFinite(Width, nameof(Width));
        ValidatePositiveFinite(Length, nameof(Length));
        ValidateNonNegativeFinite(FlowSpeed, nameof(FlowSpeed));
        ValidateNonNegativeFinite(ResponseGain, nameof(ResponseGain));
        if (!double.IsFinite(WaveAmplitude)
            || WaveAmplitude < MinimumNonNegativeMagnitude
            || WaveAmplitude > MaximumWaveAmplitude)
        {
            throw new ArgumentOutOfRangeException(nameof(WaveAmplitude));
        }

        ValidateNonNegativeFinite(WaveFrequency, nameof(WaveFrequency));
        ValidatePositiveFinite(MaximumForce, nameof(MaximumForce));
        return this;
    }

    private static void ValidateFinite(PlanarVector value, string parameterName)
    {
        if (!double.IsFinite(value.X) || !double.IsFinite(value.Z))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateNonNegativeFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < MinimumNonNegativeMagnitude)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= MinimumPositiveMagnitude)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
