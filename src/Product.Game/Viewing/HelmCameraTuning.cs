namespace Rusty.Space.Product.Viewing;

internal sealed record HelmCameraTuning(double FovYDegrees, double NearPlane, double FarPlane)
{
    internal HelmCameraTuning Validate()
    {
        if (!double.IsFinite(FovYDegrees) || FovYDegrees <= 0 || FovYDegrees >= 180)
        {
            throw new ArgumentOutOfRangeException(nameof(FovYDegrees));
        }
        if (!double.IsFinite(NearPlane) || NearPlane <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(NearPlane));
        }
        if (!double.IsFinite(FarPlane) || FarPlane <= NearPlane)
        {
            throw new ArgumentOutOfRangeException(nameof(FarPlane));
        }
        return this;
    }
}
