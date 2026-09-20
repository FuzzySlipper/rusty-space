using System.Text;
using Rusty.Engine;
using Rusty.Space.Product.Viewing;
using Xunit;

namespace Rusty.Space.Product.Viewing.Tests;

/// <summary>
/// The chase view closes its gap over admitted simulated time, never over a
/// count multiplied by a product rate, and it takes zoom from the declared
/// intent like every other control. These pin the follow law SpaceFlight feeds
/// it — no time, no move; the same time closes the same gap however the Engine
/// admitted it; a long stall stops at the cap instead of teleporting the view —
/// and the bounded multiplicative zoom law behind the wheel.
/// </summary>
public class TrackingCameraTests
{
    private static readonly TimeSpan FixedStep = TimeSpan.FromSeconds(1.0 / 60.0);
    private static readonly TimeSpan Smoothing = TimeSpan.FromSeconds(0.35);
    private static readonly TimeSpan FollowCap = TimeSpan.FromSeconds(0.25);
    private const double Tolerance = 1e-12;

    private static readonly CameraTuning Camera = new(
        PitchDegrees: 55.0,
        YawDegrees: 0.0,
        HeightAboveShip: 9.0,
        BackDistance: 11.0,
        PositionSmoothing: Smoothing,
        FovYDegrees: 55.0,
        NearPlane: 0.1,
        FarPlane: 900.0,
        MinimumZoomScale: 0.45,
        MaximumZoomScale: 3.0,
        WheelZoomSensitivity: 0.0015);

    [Fact]
    public void ATurnThatSimulatedNothingLeavesTheViewWhereItWas()
    {
        Assert.Equal(0.0, TrackingCamera.FollowFraction(TimeSpan.Zero, Smoothing), Tolerance);
    }

    [Fact]
    public void TheSameSimulatedTimeClosesTheSameGapHoweverItWasAdmitted()
    {
        // Four one-step turns and one turn admitted four steps move the chase
        // position identically: the admitted count is not a second clock.
        double oneStep = TrackingCamera.FollowFraction(FixedStep, Smoothing);
        double acrossFourTurns = 1.0 - Math.Pow(1.0 - oneStep, 4.0);
        double oneTurnOfFourSteps = TrackingCamera.FollowFraction(
            TimeSpan.FromTicks(4 * FixedStep.Ticks),
            Smoothing);

        Assert.Equal(acrossFourTurns, oneTurnOfFourSteps, 9);
    }

    [Fact]
    public void ALongerWindowClosesMoreOfTheGapButNeverAllOfIt()
    {
        double shortWindow = TrackingCamera.FollowFraction(FixedStep, Smoothing);
        double longWindow = TrackingCamera.FollowFraction(TimeSpan.FromSeconds(0.1), Smoothing);

        Assert.True(shortWindow < longWindow);
        Assert.True(longWindow < 1.0);
    }

    [Fact]
    public void AStallPastTheCapStopsAtTheCapRatherThanTeleportingTheView()
    {
        double atCap = TrackingCamera.FollowFraction(FollowCap, Smoothing);
        double pastCap = TrackingCamera.FollowFraction(TimeSpan.FromSeconds(10.0), Smoothing);

        Assert.Equal(atCap, pastCap, Tolerance);
    }

    [Fact]
    public void OneWheelNotchMovesTheViewByTheSameProportionWhereverItSits()
    {
        // Zoom is multiplicative in the wheel, so a notch out from close range
        // and the same notch from far range cover the same fraction of the
        // remaining distance rather than a fixed number of metres.
        double notch = 100.0;
        double fromClose = TrackingCamera.ApplyZoom(1.0, notch, Camera);
        double fromFar = TrackingCamera.ApplyZoom(2.0, notch, Camera);

        Assert.Equal(2.0 * fromClose, fromFar, 9);
    }

    [Fact]
    public void ZoomStopsAtBothEndsOfItsAuthoredRange()
    {
        Assert.Equal(Camera.MaximumZoomScale, TrackingCamera.ApplyZoom(3.0, 10000.0, Camera), 9);
        Assert.Equal(Camera.MinimumZoomScale, TrackingCamera.ApplyZoom(0.45, -10000.0, Camera), 9);
    }

    [Fact]
    public void ZoomArrivesOnlyThroughTheDeclaredIntent()
    {
        // The wheel is mapped to space.camera.zoom in the product manifest. A
        // raw wheel fact is not a second voice the camera listens to, so a host
        // that sends one leaves the view exactly where it was.
        Assert.Equal(120.0, TrackingCamera.MappedZoomDelta(new[] { MappedZoom(120.0f) }), 9);
        Assert.Equal(0.0, TrackingCamera.MappedZoomDelta(new[] { RawWheel(120.0) }), 9);
    }

    private static ProductInputEvent MappedZoom(float value) => new()
    {
        Kind = InputEventKind.MappedAxis,
        Phase = InputPhase.Axis,
        X = value,
        Intent = Encoding.UTF8.GetBytes("space.camera.zoom"),
    };

    private static ProductInputEvent RawWheel(double y) => new()
    {
        Kind = InputEventKind.Wheel,
        Phase = InputPhase.None,
        Y = (float)y,
    };
}
