namespace Rusty.Space.Product.Viewing;

internal sealed record CameraTuning(
    double PitchDegrees,
    double YawDegrees,
    double HeightAboveShip,
    double BackDistance,
    TimeSpan PositionSmoothing,
    double FovYDegrees,
    double NearPlane,
    double FarPlane)
{
    internal CameraTuning Validate()
    {
        if (!double.IsFinite(PitchDegrees) || PitchDegrees <= -90.0 || PitchDegrees >= 90.0)
        {
            throw new ArgumentOutOfRangeException(nameof(PitchDegrees));
        }

        if (!double.IsFinite(YawDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(YawDegrees));
        }

        ValidatePositive(HeightAboveShip, nameof(HeightAboveShip));
        ValidatePositive(BackDistance, nameof(BackDistance));
        ValidatePositive(PositionSmoothing.TotalSeconds, nameof(PositionSmoothing));
        if (!double.IsFinite(FovYDegrees) || FovYDegrees <= 0.0 || FovYDegrees >= 180.0)
        {
            throw new ArgumentOutOfRangeException(nameof(FovYDegrees));
        }

        ValidatePositive(NearPlane, nameof(NearPlane));
        if (!double.IsFinite(FarPlane) || FarPlane <= NearPlane)
        {
            throw new ArgumentOutOfRangeException(nameof(FarPlane));
        }

        return this;
    }

    private static void ValidatePositive(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
