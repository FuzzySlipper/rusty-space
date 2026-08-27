namespace Rusty.Space.Product.Navigation;

public readonly record struct PlanarVector(double X, double Z)
{
    internal static PlanarVector Zero { get; } = new(0.0, 0.0);

    internal static PlanarVector UnitX { get; } = new(1.0, 0.0);

    internal double Magnitude => Math.Sqrt(MagnitudeSquared);

    internal double MagnitudeSquared => Dot(this);

    internal double Dot(PlanarVector other) => X * other.X + Z * other.Z;

    internal PlanarVector Scale(double factor) => new(X * factor, Z * factor);

    public static PlanarVector operator +(PlanarVector left, PlanarVector right) =>
        new(left.X + right.X, left.Z + right.Z);

    public static PlanarVector operator -(PlanarVector left, PlanarVector right) =>
        new(left.X - right.X, left.Z - right.Z);
}
