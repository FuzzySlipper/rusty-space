using System;
using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Bridge;

namespace Rusty.Space.Product.Viewing;

/// <summary>
/// Product-owned helm view around a second Engine-owned camera: the seated
/// eye from the bridge placements, plus the theater's filtered lean. The
/// chart camera stays the initial active view; sitting at the helm activates
/// this one through the Engine's active-camera lane, and standing returns to
/// the chart. Neither camera writes flight state.
/// </summary>
internal sealed class HelmCamera : IDisposable
{
    private readonly ICameraViewService cameraView;
    private readonly CameraPose seated;
    private readonly HelmCameraTuning tuning;
    private readonly Camera camera;
    private bool disposed;

    internal HelmCamera(ICameraViewService cameraView, BridgeLayout layout, HelmCameraTuning tuning)
    {
        this.cameraView = cameraView ?? throw new ArgumentNullException(nameof(cameraView));
        ArgumentNullException.ThrowIfNull(layout);
        this.tuning = tuning.Validate();
        seated = layout.Validate().Placements.SeatedEye;
        camera = this.cameraView.CreateCamera(Descriptor(seated.Position, seated.PitchDegrees, seated.YawDegrees));
    }

    internal void Activate()
    {
        ThrowIfDisposed();
        cameraView.SetActiveCamera(camera);
    }

    internal void Follow(HelmLean lean)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(lean);
        Vector3 position = seated.Position + lean.PositionOffset;
        double yaw = seated.YawDegrees + lean.YawOffsetDeg;
        cameraView.UpdateCamera(new CameraUpdateRequest(camera, Descriptor(position, seated.PitchDegrees, yaw)));
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

    private CameraDescriptor Descriptor(Vector3 position, double pitchDegrees, double yawDegrees) => new(
        new CameraPose(position, pitchDegrees, yawDegrees),
        CameraBasisMode.Derived,
        default,
        new CameraProjection(
            CameraProjectionKind.Perspective,
            tuning.FovYDegrees,
            VerticalSize: 0.0,
            Near: tuning.NearPlane,
            Far: tuning.FarPlane),
        new CameraViewport(0.0, 0.0, 1.0, 1.0));

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(HelmCamera));
        }
    }
}
