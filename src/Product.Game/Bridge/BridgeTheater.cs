using System;
using System.Collections.Generic;
using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.ShipSystems;

namespace Rusty.Space.Product.Bridge;

/// <summary>
/// Product owner for the bridge set's telemetry reactions. Reads the P0
/// telemetry stream and the fitted ship, stages nothing that flies, and writes
/// nothing back: every reaction is downstream presentation through named
/// Engine lanes (light updates, prop and needle fact transforms, audio
/// voices, lamp twins), each gated by its tuning enable so turning the set
/// off issues no Engine calls at all.
/// </summary>
/// <remarks>
/// <para>
/// Filters run on admitted simulated time with stated cutoffs; the same
/// admitted turns always stage the same set. Fault flicker is a deterministic
/// admitted-time shimmer inside a sag envelope, never random. Ship-frame
/// accelerations are staged interpretively into room axes (forward push
/// presses the eye back, lateral push sways it sideways, yaw acceleration
/// nudges yaw) because the room never travels with the hull; the mapping is
/// stated in tuning and means "the hull is being loaded", not a coordinate
/// truth.
/// </para>
/// <para>
/// The repeater is a physical load needle on the side housing, not a
/// billboard: structured billboards validate but render nothing in the
/// current webview host, which is an upstream gap recorded in the task
/// handoff. A needle is the visual direction's own instrument language, and
/// it rides the same staged-fact lane as every other set reaction.
/// </para>
/// </remarks>
internal sealed class BridgeTheater : IDisposable
{
    private const string HumClipPath = "audio/drive-hum.wav";
    private const string ThudClipPath = "audio/impact-thud.wav";

    /// <summary>
    /// The load needle's sweep: rest angle at no load, plus this many degrees
    /// at full meter. A compass-style sweep across the side housing face.
    /// </summary>
    private const double NeedleRestDegrees = -60.0;
    private const double NeedleSweepDegrees = 120.0;

    private readonly IAudioService audio;
    private readonly BridgeSet bridge;
    private readonly TheaterTuning tuning;
    private readonly AudioClip humClip;
    private readonly AudioClip thudClip;
    private readonly AudioVoice humVoice;
    private readonly AudioVoice thudVoice;
    private readonly AudioSourceDescriptor humBase;
    private readonly AudioSourceDescriptor thudBase;

    private double leanForward;
    private double leanLateral;
    private double leanYaw;
    private double propLateral;
    private double propForward;
    private double spool;
    private double load;
    private TimeSpan admittedTime;
    private ulong lastImpactCount;
    private bool released;

    internal BridgeTheater(
        IAudioService audio,
        BridgeSet bridge,
        TheaterTuning tuning)
    {
        this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        this.tuning = tuning.Validate();
        HumPitch = this.tuning.HumPitchBase;
        HumVolume = this.tuning.HumVolumeIdle;

        // A failed call keeps what it did (Engine #8736), so a half-built
        // theater releases the clips and voices it already opened.
        try
        {
            humClip = audio.OpenClip(new AudioClipRequest(HumClipPath));
            thudClip = audio.OpenClip(new AudioClipRequest(ThudClipPath));

            humBase = new AudioSourceDescriptor(
                humClip,
                AudioBus.Ambient,
                ToSingle(tuning.HumVolumeIdle),
                ToSingle(tuning.HumPitchBase),
                Looping: true,
                SpatialBlend: 0.0f,
                // The projection requires positive finite attenuation even for a
                // non-positional voice; it plays no spatial role here.
                Attenuation: 1.0f,
                Pan: 0.0f,
                AudioEmitterKind.Global2d,
                Vector3.Zero,
                Entity: 0,
                Offset: Vector3.Zero);
            thudBase = humBase with
            {
                Clip = thudClip,
                Bus = AudioBus.Sfx,
                Volume = 0.0f,
                Pitch = 1.0f,
                Looping = false,
            };
            humVoice = audio.CreateVoice(humBase);
            thudVoice = audio.CreateVoice(thudBase);
        }
        catch
        {
            Dispose();
            throw;
        }
        NeedleDegrees = NeedleRestDegrees;
    }

    internal TheaterTuning Tuning => tuning;

    internal HelmLean Lean { get; private set; } = HelmLean.Rest;

    internal TheaterLampState Lamps { get; private set; } = TheaterLampState.Rest;

    internal double FilteredSpool => spool;

    internal double FilteredLoad => load;

    internal double HumPitch { get; private set; }

    internal double HumVolume { get; private set; }

    /// <summary>
    /// The load needle's sweep in degrees, from <see cref="NeedleRestDegrees"/>
    /// at no load across <see cref="NeedleSweepDegrees"/> at full meter.
    /// </summary>
    internal double NeedleDegrees { get; private set; }
    internal string FaultText { get; private set; } = "SYSTEMS NOMINAL";

    internal ulong ImpactCount => lastImpactCount;

    /// <summary>
    /// Advances every filtered channel on admitted time, then stages the
    /// enabled reactions. Filter states always track (pure product math);
    /// Engine calls happen only for enabled reactions on a published turn, so
    /// a disabled theater is silent and a quiet turn stages nothing new.
    /// </summary>
    internal void Advance(
        FlightTelemetrySnapshot telemetry,
        InstalledShip ship,
        ulong impactCount,
        TimeSpan admitted,
        bool published)
    {
        ArgumentNullException.ThrowIfNull(ship);

        leanForward = CloseTowards(leanForward, telemetry.ForwardAcceleration, admitted, tuning.CameraLeanCutoffHz);
        leanLateral = CloseTowards(leanLateral, telemetry.LateralAcceleration, admitted, tuning.CameraLeanCutoffHz);
        leanYaw = CloseTowards(leanYaw, telemetry.YawAcceleration, admitted, tuning.CameraLeanCutoffHz);
        propLateral = CloseTowards(propLateral, telemetry.LateralAcceleration, admitted, tuning.PropCutoffHz);
        propForward = CloseTowards(propForward, telemetry.ForwardAcceleration, admitted, tuning.PropCutoffHz);
        spool = CloseTowards(spool, telemetry.DriveEffort, admitted, tuning.SpoolCutoffHz);
        load = CloseTowards(load, telemetry.FieldLoad, admitted, tuning.LoadCutoffHz);
        if (admitted > TimeSpan.Zero)
        {
            admittedTime += admitted;
        }

        string? fault = FirstFault(ship);
        FaultText = fault ?? "SYSTEMS NOMINAL";
        bool faulted = fault is not null;
        Lamps = new TheaterLampState(faulted, spool > tuning.SpoolReadyThreshold);
        Lean = tuning.CameraReactions
            ? new HelmLean(
                new Vector3(
                    ToSingle(-Clamp(leanForward * tuning.CameraLeanGainMPerMSS, tuning.CameraLeanClampM)),
                    0.0f,
                    ToSingle(Clamp(leanLateral * tuning.CameraLeanGainMPerMSS * tuning.CameraLateralShare, tuning.CameraLeanClampM))),
                Clamp(leanYaw * tuning.CameraYawGainDegPerRadSS, tuning.CameraYawClampDeg))
            : HelmLean.Rest;

        bool struck = impactCount != lastImpactCount && telemetry.CollisionMagnitude > 0.0;
        lastImpactCount = impactCount;
        if (!published)
        {
            return;
        }

        HumPitch = tuning.HumPitchBase + (tuning.HumPitchSpan * spool);
        HumVolume = tuning.HumVolumeIdle + (tuning.HumVolumeSpan * spool);
        if (tuning.AudioReactions)
        {
            audio.UpdateVoice(new AudioVoiceUpdateRequest(
                humVoice,
                humBase with { Volume = ToSingle(HumVolume), Pitch = ToSingle(HumPitch) }));
            if (struck)
            {
                double strength = Math.Clamp(telemetry.CollisionMagnitude / 8.0, 0.15, 1.0);
                audio.UpdateVoice(new AudioVoiceUpdateRequest(
                    thudVoice,
                    thudBase with
                    {
                        Volume = ToSingle(strength),
                        Pitch = ToSingle(0.8 + (0.4 * strength)),
                    }));
                audio.ControlVoice(new AudioVoiceControlRequest(thudVoice, AudioVoiceControl.Retrigger));
            }
        }

        if (tuning.LightingReactions)
        {
            bridge.UpdatePracticalLights(OverheadLevel(faulted), HelmLevel(faulted));
        }

        if (tuning.PropReactions)
        {
            bridge.SetPropSway(Sway());
        }

        NeedleDegrees = NeedleRestDegrees
            + (NeedleSweepDegrees * Math.Clamp(load / tuning.LoadMeterMax, 0.0, 1.0));
        if (tuning.RepeaterReactions)
        {
            bridge.SetNeedleRotation(NeedleRotation());
        }
    }

    internal void Reset()
    {
        leanForward = 0.0;
        leanLateral = 0.0;
        leanYaw = 0.0;
        propLateral = 0.0;
        propForward = 0.0;
        spool = 0.0;
        load = 0.0;
        admittedTime = TimeSpan.Zero;
        lastImpactCount = 0;
        Lean = HelmLean.Rest;
        Lamps = TheaterLampState.Rest;
        FaultText = "SYSTEMS NOMINAL";
        HumPitch = tuning.HumPitchBase;
        HumVolume = tuning.HumVolumeIdle;
        NeedleDegrees = NeedleRestDegrees;
        // Restaging neutral presentation is itself gated: with every reaction
        // off, resetting stages nothing, so the enable flags mean zero Engine
        // calls from either entry point.
        if (tuning.PropReactions)
        {
            bridge.SetPropSway(Quaternion.Identity);
        }

        if (tuning.RepeaterReactions)
        {
            bridge.SetNeedleRotation(NeedleRotation());
        }

        if (tuning.LightingReactions)
        {
            bridge.UpdatePracticalLights(1.0f, 1.0f);
        }

        if (tuning.AudioReactions)
        {
            audio.UpdateVoice(new AudioVoiceUpdateRequest(humVoice, humBase));
        }
    }

    public void Dispose()
    {
        if (released)
        {
            return;
        }

        released = true;
        List<Exception> errors = [];
        void Release(IDisposable? resource)
        {
            try
            {
                resource?.Dispose();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        Release(thudVoice);
        Release(humVoice);
        Release(thudClip);
        Release(humClip);
        if (errors.Count > 0)
        {
            throw new AggregateException(errors);
        }
    }

    private static double CloseTowards(double current, double target, TimeSpan admitted, double cutoffHz)
    {
        double fraction = TheaterTuning.FilterFraction(admitted, cutoffHz);
        return current + ((target - current) * fraction);
    }

    private static double Clamp(double value, double magnitude) => Math.Clamp(value, -magnitude, magnitude);

    private static float ToSingle(double value) => checked((float)value);

    private static string? FirstFault(InstalledShip ship)
    {
        foreach (InstalledPart part in new[]
                 {
                     ship.Emitter, ship.MainDrive, ship.PortStabilizer, ship.StarboardStabilizer,
                 })
        {
            if (part.OutOfTrim)
            {
                return part.Id.Value;
            }
        }

        return null;
    }

    private Quaternion Sway()
    {
        Quaternion roll = Quaternion.CreateFromAxisAngle(
            Vector3.UnitZ, ToSingle(Clamp(propLateral * tuning.PropGainRadPerMSS, tuning.PropClampRad)));
        Quaternion pitch = Quaternion.CreateFromAxisAngle(
            Vector3.UnitX, ToSingle(Clamp(propForward * tuning.PropGainRadPerMSS * tuning.PropForwardShare, tuning.PropClampRad)));
        return roll * pitch;
    }

    /// <summary>
    /// The needle's rotation about the housing face normal (room X): the thin
    /// module sweeps its face like a compass needle as the filtered load
    /// climbs the meter.
    /// </summary>
    private Quaternion NeedleRotation() => Quaternion.CreateFromAxisAngle(
        Vector3.UnitX, ToSingle(NeedleDegrees * Math.PI / 180.0));

    private float OverheadLevel(bool faulted) => ApplyFault(DimLevel(load, tuning.OverheadDimMin), faulted);

    private float HelmLevel(bool faulted) => ApplyFault(DimLevel(load, tuning.HelmDimMin), faulted);

    private float DimLevel(double filteredLoad, double dimMin)
    {
        double dimmed = 1.0 - ((1.0 - dimMin) * Math.Clamp(filteredLoad / tuning.DimLoadFull, 0.0, 1.0));
        return ToSingle(dimmed);
    }

    /// <summary>
    /// Deterministic admitted-time shimmer inside the sag envelope: the same
    /// admitted turns always stage the same flicker, so a brownout reads as
    /// state rather than noise.
    /// </summary>
    private float ApplyFault(float level, bool faulted)
    {
        if (!faulted)
        {
            return level;
        }

        double shimmer = 0.75
            + (0.25 * Math.Sin(2.0 * Math.PI * tuning.FaultFlickerHz * admittedTime.TotalSeconds));
        return ToSingle(level * tuning.BrownoutSag * shimmer);
    }
}

/// <summary>
/// The seated camera's filtered offset for one turn: a small positional
/// displacement in room axes plus a yaw nudge. Rest is the unmoved helm pose.
/// </summary>
internal sealed record HelmLean(Vector3 PositionOffset, double YawOffsetDeg)
{
    internal static HelmLean Rest { get; } = new(Vector3.Zero, 0.0);
}

/// <summary>
/// Which strip lamps burn this turn. The presentation publishes the matching
/// lamp module visible and its resting twin hidden.
/// </summary>
internal sealed record TheaterLampState(bool FaultLit, bool ReadyLit)
{
    internal static TheaterLampState Rest { get; } = new(false, false);
}
