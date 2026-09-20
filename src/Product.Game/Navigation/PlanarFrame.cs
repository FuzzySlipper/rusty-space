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
/// controller aims along, and what every consumer means by forward. Because the
/// plane turns the other way about <c>+Y</c>, the Engine's angular channel —
/// attitude, angular velocity, and torque alike — is the negation of the
/// heading quantity it stands for, in both directions. <see cref="HeadingOf"/>
/// and <see cref="HeadingRateOf"/> read an Engine attitude or angular velocity
/// back as heading and heading rate; <see cref="EngineYaw"/> turns a
/// heading-positive angular quantity into the Engine value to command.
/// </para>
/// <para>
/// These are one rule, not three. Read one and write the other in the same
/// direction and the ship spins one way while its nose, its thrust, and its
/// readouts report the other: a heading <c>h</c> authored and then read back
/// comes home to <c>h</c> only when both crossings flip. Anything that touches
/// an Engine attitude or the <c>+Y</c> angular channel goes through this type,
/// which is the only place the flip is written down.
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
    /// A local offset in the ship's own frame — <c>+X</c> toward the bow, <c>+Z</c>
    /// toward starboard — expressed in the plane's world axes. The same turn that
    /// makes a heading face where it faces carries a mounted part's lever arm to
    /// wherever the ship is pointing, so a part's offset is authored once and
    /// never has to be re-derived from the heading.
    /// </summary>
    internal static PlanarVector Rotate(PlanarVector local, double headingRadians) =>
        Forward(headingRadians).Scale(local.X) + Right(headingRadians).Scale(local.Z);

    /// <summary>
    /// The heading an Engine attitude faces: the exact inverse of
    /// <see cref="ToEngineAttitude"/>, so an authored heading survives the round
    /// trip through a body and back.
    /// </summary>
    internal static double HeadingOf(Quaternion attitude)
    {
        double yawNumerator = QuaternionDoubleFactor
            * ((attitude.W * attitude.Y) + (attitude.X * attitude.Z));
        double yawDenominator = QuaternionUnitMagnitude - (QuaternionDoubleFactor
            * ((attitude.Y * attitude.Y) + (attitude.Z * attitude.Z)));
        return -Math.Atan2(yawNumerator, yawDenominator);
    }

    /// <summary>
    /// How fast the authoritative heading turns under an Engine angular velocity
    /// about <c>+Y</c>. Inverse of <see cref="EngineYaw"/>.
    /// </summary>
    internal static double HeadingRateOf(double engineYawRate) => -engineYawRate;

    /// <summary>
    /// The Engine value for the <c>+Y</c> angular channel — an angular velocity
    /// to command or a torque to apply — that turns the authoritative heading by
    /// the given heading-positive amount.
    /// </summary>
    internal static double EngineYaw(double headingChannel) => -headingChannel;

    /// <summary>
    /// The Engine attitude that turns a shape's local <c>+X</c> to face a planar
    /// heading. The only place a heading becomes an Engine rotation.
    /// </summary>
    internal static Quaternion ToEngineAttitude(double headingRadians) =>
        Quaternion.CreateFromAxisAngle(Vector3.UnitY, checked((float)-headingRadians));

    /// <summary>
    /// Signed torque for an in-plane offset from the center of mass and an
    /// in-plane force, in the authoritative heading sense: positive increases
    /// the heading, turning the nose toward the ship's right, so an offset
    /// toward starboard driving forward yaws the ship to port.
    /// </summary>
    /// <remarks>
    /// This is a heading quantity, so it reaches a solver only through
    /// <see cref="EngineYaw"/>. A raw world-space cross product of the same two
    /// vectors has the opposite sign, and the heading sense is the negation of
    /// it.
    /// </remarks>
    internal static double YawTorque(PlanarVector offset, PlanarVector force) =>
        (offset.X * force.Z) - (offset.Z * force.X);
}
