using System;

namespace Rusty.Space.Product.Bridge;

/// <summary>
/// How the bridge theater answers the telemetry stream. Every reaction is a
/// low-pass filter with a stated cutoff plus a gain and a clamp, all stepped
/// on admitted simulated time — never wall-clock time — so a pause freezes
/// the set and a catch-up turn cannot invent motion.
/// </summary>
/// <remarks>
/// <para>
/// Cutoffs separate channels by intent: the camera leans against sustained
/// acceleration at 1 Hz so solver jitter never reaches the eye; the loose
/// prop answers a distinct quicker 2.5 Hz channel; spool is smoothed slower
/// still because a drive note should read as state, not as vibration, while
/// load tracks at 1.5 Hz so the needle and dimming answer promptly. The fault
/// flicker is deterministic in admitted time (an 11 Hz admitted-time sine
/// shimmer inside a sag envelope), never random: the same admitted turns
/// always stage the same flicker.
/// </para>
/// <para>
/// The enable flags are the acceptance switch: with every reaction off, the
/// theater issues no Engine calls at all — neither advancing nor resetting —
/// so turning the set off cannot move the hull by a single newton. The flight
/// model never sees this record. The helm camera publishes no pose updates
/// while stood at the chart; the chart camera's own framing is navigation,
/// not a theater reaction.
/// </para>
/// </remarks>
internal sealed record TheaterTuning(
    // Camera lean against sustained ship-frame acceleration. The lateral
    // share stages sideways sway at half the fore-aft gain: a sideways step
    // should read, not shove.
    double CameraLeanCutoffHz,
    double CameraLeanGainMPerMSS,
    double CameraLeanClampM,
    double CameraLateralShare,
    double CameraYawGainDegPerRadSS,
    double CameraYawClampDeg,
    // Loose prop sway on its own quicker channel. The forward share pitches
    // the slate at half the roll gain for the same reason.
    double PropCutoffHz,
    double PropGainRadPerMSS,
    double PropClampRad,
    double PropForwardShare,
    // Drive spool smoothing for the hum.
    double SpoolCutoffHz,
    double HumPitchBase,
    double HumPitchSpan,
    double HumVolumeIdle,
    double HumVolumeSpan,
    // Field-load smoothing for the needle, dimming, and lamps.
    double LoadCutoffHz,
    double LoadMeterMax,
    double OverheadDimMin,
    double HelmDimMin,
    double DimLoadFull,
    // Fault brownout: deterministic admitted-time flicker inside a sag.
    double FaultFlickerHz,
    double BrownoutSag,
    double SpoolReadyThreshold,
    // Acceptance switch: every reaction off means zero Engine calls.
    bool CameraReactions,
    bool LightingReactions,
    bool PropReactions,
    bool AudioReactions,
    bool RepeaterReactions)
{
    internal static TheaterTuning Defaults { get; } = new(
        CameraLeanCutoffHz: 1.0,
        CameraLeanGainMPerMSS: 0.02,
        CameraLeanClampM: 0.12,
        CameraLateralShare: 0.5,
        CameraYawGainDegPerRadSS: 0.8,
        CameraYawClampDeg: 3.0,
        PropCutoffHz: 2.5,
        PropGainRadPerMSS: 0.06,
        PropClampRad: 0.35,
        PropForwardShare: 0.5,
        SpoolCutoffHz: 0.8,
        HumPitchBase: 0.7,
        HumPitchSpan: 0.6,
        HumVolumeIdle: 0.12,
        HumVolumeSpan: 0.5,
        LoadCutoffHz: 1.5,
        LoadMeterMax: 8.0,
        OverheadDimMin: 0.45,
        HelmDimMin: 0.55,
        DimLoadFull: 6.0,
        FaultFlickerHz: 11.0,
        BrownoutSag: 0.5,
        SpoolReadyThreshold: 0.35,
        CameraReactions: true,
        LightingReactions: true,
        PropReactions: true,
        AudioReactions: true,
        RepeaterReactions: true);

    internal TheaterTuning Validate()
    {
        ValidateCutoff(CameraLeanCutoffHz, nameof(CameraLeanCutoffHz));
        ValidateFiniteNonNegative(CameraLeanGainMPerMSS, nameof(CameraLeanGainMPerMSS));
        ValidatePositive(CameraLeanClampM, nameof(CameraLeanClampM));
        ValidateFraction(CameraLateralShare, nameof(CameraLateralShare));
        ValidateFiniteNonNegative(CameraYawGainDegPerRadSS, nameof(CameraYawGainDegPerRadSS));
        ValidatePositive(CameraYawClampDeg, nameof(CameraYawClampDeg));
        ValidateCutoff(PropCutoffHz, nameof(PropCutoffHz));
        ValidateFiniteNonNegative(PropGainRadPerMSS, nameof(PropGainRadPerMSS));
        ValidatePositive(PropClampRad, nameof(PropClampRad));
        ValidateFraction(PropForwardShare, nameof(PropForwardShare));
        ValidateCutoff(SpoolCutoffHz, nameof(SpoolCutoffHz));
        // The projection requires pitch in [0.25, 4.0] and volume in [0, 1]:
        // the base plus its full span must stay inside both.
        if (HumPitchBase < 0.25 || HumPitchBase > 4.0 || HumPitchBase + HumPitchSpan > 4.0)
        {
            throw new ArgumentOutOfRangeException(nameof(HumPitchBase));
        }

        if (HumVolumeIdle < 0.0 || HumVolumeIdle > 1.0 || HumVolumeIdle + HumVolumeSpan > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(HumVolumeIdle));
        }

        ValidateFiniteNonNegative(HumPitchSpan, nameof(HumPitchSpan));
        ValidateFiniteNonNegative(HumVolumeSpan, nameof(HumVolumeSpan));
        ValidateCutoff(LoadCutoffHz, nameof(LoadCutoffHz));
        ValidatePositive(LoadMeterMax, nameof(LoadMeterMax));
        ValidateFraction(OverheadDimMin, nameof(OverheadDimMin));
        ValidateFraction(HelmDimMin, nameof(HelmDimMin));
        ValidatePositive(DimLoadFull, nameof(DimLoadFull));
        ValidateCutoff(FaultFlickerHz, nameof(FaultFlickerHz));
        ValidateFraction(BrownoutSag, nameof(BrownoutSag));
        // Drive effort runs 0..1, so a threshold above 1.0 would silently kill
        // the ready lamp. Dimming bottoms out at DimLoadFull while the needle
        // still has travel to LoadMeterMax: deliberate staging, stated here —
        // the room goes dark before the gauge pegs.
        if (SpoolReadyThreshold < 0.0 || SpoolReadyThreshold > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(SpoolReadyThreshold));
        }

        return this;
    }

    /// <summary>
    /// First-order low-pass step: how far a filtered channel closes toward its
    /// target over admitted seconds at the given cutoff. Zero time means no
    /// motion; the same admitted time always closes the same gap.
    /// </summary>
    internal static double FilterFraction(TimeSpan admitted, double cutoffHz)
    {
        double seconds = Math.Max(0.0, admitted.TotalSeconds);
        double tau = 1.0 / (2.0 * Math.PI * cutoffHz);
        return 1.0 - Math.Exp(-seconds / tau);
    }

    private static void ValidateCutoff(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidatePositive(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateFiniteNonNegative(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateFraction(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0.0 || value > 1.0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
