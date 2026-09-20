using System;
using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Viewing;

/// <summary>
/// Product-owned tracking view around one Engine-owned camera: a world-stable
/// oblique top-down framing whose position loosely follows the ship. Yaw and
/// pitch never rotate with the ship, so the scene keeps a stable orientation
/// while the smoothed chase position supplies the "loose" tracking feel.
/// </summary>
internal sealed class TrackingCamera : IDisposable
{
    private static ReadOnlySpan<byte> ZoomIntent => "space.camera.zoom"u8;

    // Camera smoothing runs on admitted simulated time, never wall-clock time,
    // so a paused product or a catch-up turn cannot invent or skip follow.
    private const double MaximumFollowDeltaSeconds = 0.25;
    private const double NeutralDeltaSeconds = 0.0;
    private const double FullSmoothing = 1.0;

    // Engine world axes are right-handed Y-up. Camera yaw zero faces -Z and
    // positive yaw turns toward +X, so the horizontal look direction for yaw
    // is (sin yaw, 0, -cos yaw); the tuned yaw decides which world side the
    // camera sits on and the pitch decides how far it leans down.
    private const double YAxisForwardZ = -1.0;

    private readonly ICameraViewService cameraView;
    private readonly CameraTuning tuning;
    private readonly Camera camera;
    private Vector3 chasePosition;
    private ulong lastResetCount;
    private double zoomScale = 1.0;
    private bool positioned;
    private bool disposed;

    internal TrackingCamera(
        ICameraViewService cameraView,
        CameraTuning tuning,
        FlightReadout spawn,
        ulong spawnResetCount)
    {
        this.cameraView = cameraView ?? throw new ArgumentNullException(nameof(cameraView));
        this.tuning = tuning;
        chasePosition = AnchorPosition(spawn.Position);
        positioned = true;
        lastResetCount = spawnResetCount;
        camera = this.cameraView.CreateCamera(Descriptor(chasePosition));
        this.cameraView.SetActiveCamera(camera);
    }

    internal void Follow(
        FlightReadout readout,
        TimeSpan turnDuration,
        ulong resetCount,
        ReadOnlySpan<ProductInputEvent> input)
    {
        ThrowIfDisposed();
        double nextZoomScale = ResolveZoomScale(input);
        bool nextPositioned = positioned;
        if (resetCount != lastResetCount)
        {
            nextPositioned = false;
        }

        Vector3 target = AnchorPosition(readout.Position, nextZoomScale);
        Vector3 nextChasePosition;
        if (!nextPositioned)
        {
            nextChasePosition = target;
            nextPositioned = true;
        }
        else
        {
            nextChasePosition = chasePosition
                + ((target - chasePosition) * ToSingle(FollowFraction(
                    turnDuration,
                    tuning.PositionSmoothing)));
        }

        cameraView.UpdateCamera(new CameraUpdateRequest(camera, Descriptor(nextChasePosition)));
        chasePosition = nextChasePosition;
        positioned = nextPositioned;
        zoomScale = nextZoomScale;
        lastResetCount = resetCount;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        camera.Dispose();
    }

    private Vector3 AnchorPosition(PlanarVector shipPosition) => AnchorPosition(shipPosition, zoomScale);

    private Vector3 AnchorPosition(PlanarVector shipPosition, double scale)
    {
        double yawRadians = tuning.YawDegrees * Math.PI / 180.0;
        double forwardX = Math.Sin(yawRadians);
        double forwardZ = YAxisForwardZ * Math.Cos(yawRadians);
        return new Vector3(
            ToSingle(shipPosition.X - (forwardX * tuning.BackDistance * scale)),
            ToSingle(tuning.HeightAboveShip * scale),
            ToSingle(shipPosition.Z - (forwardZ * tuning.BackDistance * scale)));
    }

    private double ResolveZoomScale(ReadOnlySpan<ProductInputEvent> input)
    {
        double mappedDelta = 0.0;
        bool hasMappedZoom = false;
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.Kind == InputEventKind.MappedAxis
                && inputEvent.Intent.Span.SequenceEqual(ZoomIntent))
            {
                hasMappedZoom = true;
                mappedDelta += inputEvent.X;
            }
        }

        double wheelDelta = mappedDelta;
        if (!hasMappedZoom)
        {
            foreach (ProductInputEvent inputEvent in input)
            {
                if (inputEvent.Kind == InputEventKind.Wheel)
                {
                    wheelDelta += inputEvent.Y;
                }
            }
        }

        double requested = zoomScale * Math.Exp(wheelDelta * tuning.WheelZoomSensitivity);
        return Math.Clamp(requested, tuning.MinimumZoomScale, tuning.MaximumZoomScale);
    }

    private CameraDescriptor Descriptor(Vector3 position) => new(
        new CameraPose(position, tuning.PitchDegrees, tuning.YawDegrees),
        CameraBasisMode.Derived,
        default,
        new CameraProjection(
            CameraProjectionKind.Perspective,
            tuning.FovYDegrees,
            VerticalSize: 0.0,
            tuning.NearPlane,
            tuning.FarPlane),
        new CameraViewport(0.0, 0.0, 1.0, 1.0));

    /// <summary>
    /// How much of the remaining gap the follow closes for one turn: a first
    /// order lag over the simulated time the Engine admitted, capped so a long
    /// stall still moves the view rather than teleporting it.
    /// </summary>
    internal static double FollowFraction(TimeSpan turnDuration, TimeSpan positionSmoothing)
    {
        double deltaSeconds = Math.Clamp(
            turnDuration.TotalSeconds,
            NeutralDeltaSeconds,
            MaximumFollowDeltaSeconds);
        return FullSmoothing - Math.Exp(-deltaSeconds / positionSmoothing.TotalSeconds);
    }

    private static float ToSingle(double value) => checked((float)value);

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(TrackingCamera));
        }
    }
}
