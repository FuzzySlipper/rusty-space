using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Engine.Tests;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.ShipSystems;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Bridge.Tests;

/// <summary>
/// What the theater answers the telemetry stream on recording doubles: each
/// filtered channel closes on admitted time, spool moves the hum, load dims
/// the practicals and the repeater, lateral push sways the prop and the helm
/// eye, impacts retrigger the thud once, faults light the fault lamp and sag
/// the room, and a disabled theater stages nothing at all. Flight behavior is
/// never touched: the theater holds no Dynamics handle, and silence with the
/// reactions off is asserted call by call.
/// </summary>
public class BridgeTheaterTests
{
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1.0 / 60.0);
    private const ulong HumVoice = 1UL;
    private const ulong ThudVoice = 2UL;
    private const ulong OverheadLight = 1UL;
    private const int PropFactIndex = 7;
    private const int NeedleFactIndex = 18;

    [Fact]
    public void AFilterClosesTheSameGapForTheSameAdmittedTime()
    {
        double oneStep = TheaterTuning.FilterFraction(Step, 1.0);
        double fourSteps = 1.0 - Math.Pow(1.0 - oneStep, 4.0);
        double oneTurnOfFourSteps = TheaterTuning.FilterFraction(
            TimeSpan.FromTicks(4 * Step.Ticks),
            1.0);

        Assert.Equal(fourSteps, oneTurnOfFourSteps, 9);
    }

    [Fact]
    public void NoAdmittedTimeMeansNoMotion()
    {
        Assert.Equal(0.0, TheaterTuning.FilterFraction(TimeSpan.Zero, 2.5));
    }

    [Fact]
    public void SpoolMovesTheHumPitchAndVolume()
    {
        RecordingEngine engine = new();
        using BridgeTheater theater = Stage(engine);

        for (int turn = 0; turn < 240; turn++)
        {
            theater.Advance(Spooling(1.0), StockShip(), 0UL, Step, published: true);
        }

        Assert.True(theater.FilteredSpool > 0.95);
        AudioSourceDescriptor hum = engine.Audio.VoiceDescriptors[HumVoice];
        TheaterTuning tuning = SpaceTuning.Defaults.Theater;
        Assert.Equal(tuning.HumPitchBase + tuning.HumPitchSpan, hum.Pitch, 2);
        Assert.Equal(tuning.HumVolumeIdle + tuning.HumVolumeSpan, hum.Volume, 2);
    }

    [Fact]
    public void LoadDimsThePracticalsAndSweepsTheNeedle()
    {
        RecordingEngine engine = new();
        SpaceTuning tuning = SpaceTuning.Defaults;
        using BridgeSet bridge = new(engine.Graphics.Service, engine.ImplicitSurfaces.Service, tuning.Bridge);
        using BridgeTheater theater = new(engine.Audio.Service, bridge, tuning.Theater);

        for (int turn = 0; turn < 240; turn++)
        {
            theater.Advance(Loaded(6.0), StockShip(), 0UL, Step, published: true);
        }

        Assert.True(theater.FilteredLoad > 5.0);
        Assert.True(engine.Graphics.LightLevels[OverheadLight] < 2.0f);
        Assert.True(engine.Graphics.LightLevels[OverheadLight] >= 2.0f * (float)tuning.Theater.OverheadDimMin);
        // Three-quarter meter sweeps the needle three quarters across.
        Assert.Equal(30.0, theater.NeedleDegrees, 0);
        Quaternion needle = bridge.Facts.ToArray()[NeedleFactIndex].Transform.Rotation;
        Assert.NotEqual(Quaternion.Identity, needle);
        // The needle sits on the housing face below the side display.
        Assert.Equal(bridge.NeedlePivot, bridge.Facts.ToArray()[NeedleFactIndex].Transform.Translation);
    }

    [Fact]
    public void LateralPushSwaysThePropAndLeansTheHelmEye()
    {
        RecordingEngine engine = new();
        using BridgeSet bridge = new(engine.Graphics.Service, engine.ImplicitSurfaces.Service, BridgeLayout.Defaults);
        using BridgeTheater theater = new(engine.Audio.Service, bridge, SpaceTuning.Defaults.Theater);

        for (int turn = 0; turn < 120; turn++)
        {
            theater.Advance(Pushed(lateral: 2.0), StockShip(), 0UL, Step, published: true);
        }

        Quaternion sway = bridge.Facts.ToArray()[PropFactIndex].Transform.Rotation;
        Assert.NotEqual(Quaternion.Identity, sway);
        // Converged lateral 2.0 at gain 0.02 with the half lateral share.
        Assert.Equal(0.02, theater.Lean.PositionOffset.Z, 3);
    }

    [Fact]
    public void LeanClampsEveryAxisAtHighAcceleration()
    {
        RecordingEngine engine = new();
        using BridgeTheater theater = Stage(engine);
        FlightTelemetrySnapshot slammed = new(
            0UL, 1U, 100.0, 100.0, 100.0, 0.0, 0.0, false, false, 0.6, 0.0, 0.0,
            PlanarVector.Zero, 0.0, null);

        for (int turn = 0; turn < 240; turn++)
        {
            theater.Advance(slammed, StockShip(), 0UL, Step, published: true);
        }

        TheaterTuning tuning = SpaceTuning.Defaults.Theater;
        Assert.Equal(-tuning.CameraLeanClampM, theater.Lean.PositionOffset.X, 6);
        Assert.Equal(tuning.CameraLeanClampM, theater.Lean.PositionOffset.Z, 6);
        Assert.Equal(tuning.CameraYawClampDeg, theater.Lean.YawOffsetDeg, 9);
    }

    [Fact]
    public void ExtremeLoadPegsTheNeedleAndFloorsTheDimming()
    {
        RecordingEngine engine = new();
        SpaceTuning tuning = SpaceTuning.Defaults;
        using BridgeSet bridge = new(engine.Graphics.Service, engine.ImplicitSurfaces.Service, tuning.Bridge);
        using BridgeTheater theater = new(engine.Audio.Service, bridge, tuning.Theater);

        for (int turn = 0; turn < 240; turn++)
        {
            theater.Advance(Loaded(20.0), StockShip(), 0UL, Step, published: true);
        }

        Assert.Equal(60.0, theater.NeedleDegrees, 0);
        Assert.Equal(2.0f * (float)tuning.Theater.OverheadDimMin, engine.Graphics.LightLevels[OverheadLight], 3);
    }

    [Fact]
    public void FaultFlickerBreathesInsteadOfHoldingStill()
    {
        RecordingEngine engine = new();
        using BridgeTheater theater = Stage(engine);
        InstalledShip jammed = StockShip();
        jammed.TakeImpact(new PlanarVector(0.0, -1.0), 8.0);

        List<float> levels = [];
        for (int turn = 0; turn < 72; turn++)
        {
            theater.Advance(Loaded(2.0), jammed, 0UL, Step, published: true);
            levels.Add(engine.Graphics.LightLevels[OverheadLight]);
        }

        // Twelve admitted turns span a full 11 Hz shimmer period, so the
        // brownout must vary across the sweep rather than sit at the sag.
        Assert.True(levels.Max() > levels.Min());
    }

    [Fact]
    public void ResetThenImpactStillAnswersExactlyOnce()
    {
        RecordingEngine engine = new();
        using BridgeTheater theater = Stage(engine);

        theater.Advance(Struck(magnitude: 5.0), StockShip(), 1UL, Step, published: true);
        theater.Reset();
        theater.Advance(
            FlightTelemetrySnapshot.Neutral, StockShip(), 0UL, Step, published: true);
        theater.Advance(Struck(magnitude: 5.0), StockShip(), 1UL, Step, published: true);

        Assert.Equal(2, engine.Audio.RetriggeredVoices.Count);
        Assert.All(engine.Audio.RetriggeredVoices, voice => Assert.Equal(ThudVoice, voice));
    }

    [Fact]
    public void AnImpactRetriggersTheThudExactlyOnce()
    {
        RecordingEngine engine = new();
        using BridgeTheater theater = Stage(engine);
        FlightTelemetrySnapshot struck = Struck(magnitude: 5.0);

        theater.Advance(struck, StockShip(), 1UL, Step, published: true);
        theater.Advance(struck, StockShip(), 1UL, Step, published: true);

        Assert.Single(engine.Audio.RetriggeredVoices);
        Assert.Equal(ThudVoice, engine.Audio.RetriggeredVoices[0]);
        Assert.True(engine.Audio.VoiceDescriptors[ThudVoice].Volume > 0.0f);
    }

    [Fact]
    public void AJammedEffectorLightsTheFaultLampAndSagsTheRoom()
    {
        RecordingEngine engine = new();
        using BridgeTheater theater = Stage(engine);
        InstalledShip jammed = StockShip();
        jammed.TakeImpact(new PlanarVector(0.0, -1.0), 8.0);

        for (int turn = 0; turn < 60; turn++)
        {
            theater.Advance(Loaded(2.0), jammed, 0UL, Step, published: true);
        }

        Assert.True(theater.Lamps.FaultLit);
        Assert.Contains("starboard", theater.FaultText);
        float faulted = engine.Graphics.LightLevels[OverheadLight];
        Assert.True(faulted < 2.0f * 0.6f);
    }

    [Fact]
    public void ADisabledTheaterTracksFiltersButStagesNothing()
    {
        RecordingEngine engine = new();
        SpaceTuning tuning = SpaceTuning.Defaults;
        TheaterTuning off = tuning.Theater with
        {
            CameraReactions = false,
            LightingReactions = false,
            PropReactions = false,
            AudioReactions = false,
            RepeaterReactions = false,
        };
        using BridgeSet bridge = new(engine.Graphics.Service, engine.ImplicitSurfaces.Service, tuning.Bridge);
        using BridgeTheater theater = new(engine.Audio.Service, bridge, off);
        InstalledShip jammed = StockShip();
        jammed.TakeImpact(new PlanarVector(0.0, -1.0), 8.0);

        for (int turn = 0; turn < 60; turn++)
        {
            theater.Advance(Struck(magnitude: 5.0, effort: 1.0, load: 6.0), jammed, 1UL, Step, published: true);
        }

        // Filters track so re-enabling is seamless, but no Engine call goes out.
        Assert.True(theater.FilteredSpool > 0.5);
        Assert.True(theater.FilteredLoad > 1.0);
        Assert.Equal(0, engine.Audio.VoiceUpdates);
        Assert.Empty(engine.Audio.RetriggeredVoices);
        Assert.Equal(0, engine.Graphics.LightUpdates);
        Assert.Equal(Quaternion.Identity, bridge.Facts.ToArray()[PropFactIndex].Transform.Rotation);
        Assert.Equal(Quaternion.Identity, bridge.Facts.ToArray()[NeedleFactIndex].Transform.Rotation);
        Assert.Equal(HelmLean.Rest, theater.Lean);
    }

    [Fact]
    public void AnUnpublishedTurnStagesNothing()
    {
        RecordingEngine engine = new();
        using BridgeTheater theater = Stage(engine);

        theater.Advance(Spooling(1.0), StockShip(), 0UL, Step, published: false);

        Assert.Equal(0, engine.Audio.VoiceUpdates);
        Assert.Equal(0, engine.Graphics.LightUpdates);
    }

    [Fact]
    public void TheTheaterOpensItsDocumentedClips()
    {
        RecordingEngine engine = new();
        using BridgeTheater theater = Stage(engine);

        Assert.Equal(2, engine.Audio.OpenedClipPaths.Count);
        Assert.Equal("audio/drive-hum.wav", engine.Audio.OpenedClipPaths[0]);
        Assert.Equal("audio/impact-thud.wav", engine.Audio.OpenedClipPaths[1]);
    }

    [Fact]
    public void ADisabledResetStagesNothing()
    {
        RecordingEngine engine = new();
        SpaceTuning tuning = SpaceTuning.Defaults;
        TheaterTuning off = tuning.Theater with
        {
            CameraReactions = false,
            LightingReactions = false,
            PropReactions = false,
            AudioReactions = false,
            RepeaterReactions = false,
        };
        using BridgeSet bridge = new(engine.Graphics.Service, engine.ImplicitSurfaces.Service, tuning.Bridge);
        using BridgeTheater theater = new(engine.Audio.Service, bridge, off);

        theater.Reset();

        Assert.Equal(0, engine.Audio.VoiceUpdates);
        Assert.Equal(0, engine.Graphics.LightUpdates);
        Assert.Equal(Quaternion.Identity, bridge.Facts.ToArray()[PropFactIndex].Transform.Rotation);
        Assert.Equal(Quaternion.Identity, bridge.Facts.ToArray()[NeedleFactIndex].Transform.Rotation);
    }

    private static BridgeTheater Stage(RecordingEngine engine)
    {
        SpaceTuning tuning = SpaceTuning.Defaults;
        BridgeSet bridge = new(engine.Graphics.Service, engine.ImplicitSurfaces.Service, tuning.Bridge);
        return new BridgeTheater(engine.Audio.Service, bridge, tuning.Theater);
    }

    private static InstalledShip StockShip()
    {
        SpaceTuning tuning = SpaceTuning.Defaults;
        return new InstalledShip(tuning.Ship, tuning.Flight.MaximumThrust, tuning.Damage, tuning.Thermal);
    }

    private static FlightTelemetrySnapshot Spooling(double effort) => new(
        0UL, 1U, 0.0, 0.0, 0.0, effort, 0.0, false, false, 0.6, 0.0, 0.0,
        PlanarVector.Zero, 0.0, null);

    private static FlightTelemetrySnapshot Loaded(double load) => new(
        0UL, 1U, 0.0, 0.0, 0.0, 0.0, 0.0, false, false, 0.6, load, 0.0,
        PlanarVector.Zero, 0.0, null);

    private static FlightTelemetrySnapshot Pushed(double lateral) => new(
        0UL, 1U, 0.0, lateral, 0.0, 0.0, 0.0, false, false, 0.6, 0.0, 0.0,
        PlanarVector.Zero, 0.0, null);

    private static FlightTelemetrySnapshot Struck(double magnitude, double effort = 0.0, double load = 0.0) => new(
        0UL, 1U, 0.0, 0.0, 0.0, effort, 0.0, false, false, 0.6, load, 0.0,
        PlanarVector.Zero, magnitude, null);
}
