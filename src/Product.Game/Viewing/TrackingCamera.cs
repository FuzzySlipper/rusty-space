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
    // The simulated flight clock advances in fixed 1/60 s steps; camera
    // smoothing runs on that same clock so pauses and resets never invent
    // wall-clock time.
    private const double FixedStepSeconds = 1.0 / 60.0;
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
    private ulong lastFixedStepCount;
    private ulong lastResetCount;
    private bool positioned;
    private bool disposed;

    internal TrackingCamera(
        ICameraViewService cameraView,
        CameraTuning tuning,
        FlightReadout spawn,
        ulong spawnFixedStepCount,
        ulong spawnResetCount)
    {
        this.cameraView = cameraView ?? throw new ArgumentNullException(nameof(cameraView));
        this.tuning = tuning.Validate();
        chasePosition = AnchorPosition(spawn.Position);
        positioned = true;
        lastFixedStepCount = spawnFixedStepCount;
        lastResetCount = spawnResetCount;
        camera = this.cameraView.CreateCamera(Descriptor(chasePosition));
        this.cameraView.SetActiveCamera(camera);
    }

    internal void Follow(FlightReadout readout, ulong fixedStepCount, ulong resetCount)
    {
        ThrowIfDisposed();
        if (resetCount != lastResetCount)
        {
            lastResetCount = resetCount;
            positioned = false;
        }

        Vector3 target = AnchorPosition(readout.Position);
        if (!positioned)
        {
            chasePosition = target;
            positioned = true;
        }
        else
        {
            chasePosition += (target - chasePosition) * ToSingle(FollowFraction(fixedStepCount));
        }

        cameraView.UpdateCamera(new CameraUpdateRequest(camera, Descriptor(chasePosition)));
        lastFixedStepCount = fixedStepCount;
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

    private Vector3 AnchorPosition(PlanarVector shipPosition)
    {
        double yawRadians = tuning.YawDegrees * Math.PI / 180.0;
        double forwardX = Math.Sin(yawRadians);
        double forwardZ = YAxisForwardZ * Math.Cos(yawRadians);
        return new Vector3(
            ToSingle(shipPosition.X - (forwardX * tuning.BackDistance)),
            ToSingle(tuning.HeightAboveShip),
            ToSingle(shipPosition.Z - (forwardZ * tuning.BackDistance)));
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

    private double FollowFraction(ulong fixedStepCount)
    {
        double deltaSeconds = (double)(fixedStepCount - lastFixedStepCount) * FixedStepSeconds;
        deltaSeconds = Math.Clamp(deltaSeconds, NeutralDeltaSeconds, MaximumFollowDeltaSeconds);
        return FullSmoothing - Math.Exp(-deltaSeconds / tuning.PositionSmoothing.TotalSeconds);
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
