using System.Numerics;

namespace Rusty.Space.Product.Navigation;

/// <summary>
/// The one owner of the planar frame convention: heading vectors, the sign of a
/// turn, and the single conversion from a heading to an Engine attitude.
/// </summary>
/// <remarks>
/// <para>
/// Positions, velocities, and forces cross into the Engine with the plane's
/// coordinates taken identically as <c>(X, Z)</c>: a planar <c>(x, z)</c> is the
/// world vector <c>(x, 0, z)</c>. No planar vector is mirrored on the way
/// across.
/// </para>
/// <para>
/// Rotation needs one more decision, because the Engine is right-handed Y-up and
/// a planar <c>(X, Z)</c> pair is left-handed about <c>+Y</c>. A heading is the
/// angle a nose makes with <c>+X</c>, opening toward <c>+Z</c>, so heading
/// <c>h</c> faces <c>(cos h, sin h)</c> and an Engine attitude facing the same
/// way rotates <c>-h</c> about <c>+Y</c>. <see cref="ToEngineAttitude"/> is that
/// conversion, and <see cref="YawTorque"/> carries the matching sense.
/// </para>
/// <para>
/// The planar heading is authoritative: it is what the player sees, what the
/// controller aims along, and what every consumer means by forward. A body's own
/// attitude comes back through <see cref="EngineYawOf"/>, which carries
/// <c>+h</c> where anything drawn for it stands at <c>-h</c>. A collider
/// silhouette or mount offset authored against the drawn ship therefore reads
/// mirrored in <c>Z</c>: an asymmetric half-extent pair presents its long side
/// and its short side on opposite halves of a turn from the long and short sides
/// of the ship it belongs to.
/// </para>
/// </remarks>
internal static class PlanarFrame
{
    private const double QuaternionDoubleFactor = 2.0;
    private const double QuaternionUnitMagnitude = 1.0;

    /// <summary>Unit vector along a heading: <c>+X</c> at zero, opening toward <c>+Z</c>.</summary>
    internal static PlanarVector Forward(double headingRadians) =>
        new(Math.Cos(headingRadians), Math.Sin(headingRadians));

    /// <summary>
    /// Unit vector toward the ship's right: a quarter turn from
    /// <see cref="Forward"/> in the direction a positive heading turns, which is
    /// the planar image of a body's local <c>+Z</c> under
    /// <see cref="ToEngineAttitude"/>.
    /// </summary>
    internal static PlanarVector Right(double headingRadians)
    {
        PlanarVector forward = Forward(headingRadians);
        return new PlanarVector(-forward.Z, forward.X);
    }

    /// <summary>The heading a planar direction points along.</summary>
    internal static double HeadingOf(PlanarVector direction) =>
        Math.Atan2(direction.Z, direction.X);

    /// <summary>
    /// Yaw an Engine attitude carries, read the way the product reads heading.
    /// Not the inverse of <see cref="ToEngineAttitude"/>: see the mirror in the
    /// type remarks.
    /// </summary>
    internal static double EngineYawOf(Quaternion attitude)
    {
        double yawNumerator = QuaternionDoubleFactor
            * ((attitude.W * attitude.Y) + (attitude.X * attitude.Z));
        double yawDenominator = QuaternionUnitMagnitude - (QuaternionDoubleFactor
            * ((attitude.Y * attitude.Y) + (attitude.Z * attitude.Z)));
        return Math.Atan2(yawNumerator, yawDenominator);
    }

    /// <summary>
    /// The Engine attitude that turns a shape's local <c>+X</c> to face a planar
    /// heading. The only place a heading becomes an Engine rotation.
    /// </summary>
    internal static Quaternion ToEngineAttitude(double headingRadians) =>
        Quaternion.CreateFromAxisAngle(Vector3.UnitY, checked((float)-headingRadians));

    /// <summary>
    /// Signed Engine-Y torque for an in-plane offset from the center of mass and
    /// an in-plane force. Positive increases the heading, turning the nose
    /// toward the ship's right, so an offset toward starboard driving forward
    /// yaws the ship to port.
    /// </summary>
    /// <remarks>
    /// A world-space cross product of the same two vectors has the opposite
    /// sign, and that is the one a solver applies to a body's own frame. The
    /// mirror in the type remarks separates the two.
    /// </remarks>
    internal static double YawTorque(PlanarVector offset, PlanarVector force) =>
        (offset.X * force.Z) - (offset.Z * force.X);
}
