using System.Numerics;

namespace Rusty.Space.Product.Navigation;

/// <summary>Engine camera yaw zero faces -Z; positive yaw turns toward +X.</summary>
internal static class CameraOrientation
{
    internal static Vector3 HorizontalForward(double yawDegrees)
    {
        double yaw = yawDegrees * Math.PI / 180.0;
        return new Vector3((float)Math.Sin(yaw), 0.0f, (float)-Math.Cos(yaw));
    }

    internal static (double YawDegrees, double PitchDegrees) LookAt(Vector3 source, Vector3 target)
    {
        Vector3 offset = target - source;
        double length = offset.Length();
        if (length <= 0.0)
        {
            return (0.0, 0.0);
        }
        return (
            Math.Atan2(offset.X, -offset.Z) * 180.0 / Math.PI,
            Math.Asin(Math.Clamp(offset.Y / length, -1.0, 1.0)) * 180.0 / Math.PI);
    }
}
