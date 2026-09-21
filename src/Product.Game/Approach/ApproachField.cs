using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Approach;

/// <summary>
/// One approach space standing in the Engine's Dynamics world: the authored
/// geometry, handed over once, and the answer to which of it a hull struck.
/// </summary>
/// <remarks>
/// <para>
/// The product chooses the geometry and the Engine keeps it. Each obstacle
/// becomes a body in the same world the hull flies in, through the Engine's
/// shape-typed create lanes, and from then on the Engine alone decides what
/// happens where two of them meet. This owner never tests for overlap, never
/// sweeps, and never keeps a grid of its own: it holds the bodies open and the
/// identity each one stands for, so a contact the Engine reports can be named.
/// </para>
/// <para>
/// A rock is held in place by locking its translation axes rather than by being
/// weightless. The Engine admits no body with zero mass, and a body whose axes
/// are locked cannot be shoved out of the chart by the hull that hits it, which
/// is the property an authored obstacle needs: it must be exactly where the
/// chart says it is on the next approach as well as this one. Its mass is still
/// real, so the contact the Engine resolves is a real one.
/// </para>
/// <para>
/// Continuous collision is left off. This space is flown at the speeds the
/// flight tuning allows, which is a fraction of a metre per fixed step against
/// obstacles measured in metres: a hull cannot be on both sides of a rock in the
/// time between steps, and asking the Engine for swept contacts would be paying
/// for insurance against a scale this chart does not use.
/// </para>
/// </remarks>
internal sealed class ApproachField : IDisposable
{
    private const float NoLinearDamping = 0.0f;
    private const float NoAngularDamping = 0.0f;
    private const float NoGravityScale = 0.0f;
    private const float NoRestitution = 0.0f;
    private const uint AllCollisionGroups = uint.MaxValue;
    private const bool ObstacleEnabled = true;
    private const bool ObstacleAwake = false;
    private const bool ObstaclesUseContinuousCollision = false;
    private const float ChartPlaneHeight = 0.0f;
    private const bool AxisLocked = true;

    private readonly List<DynamicsBody> bodies = [];
    private readonly Dictionary<ulong, ObstacleId> obstacleByBody = [];
    private bool disposed;

    internal ApproachField(
        IDynamicsService dynamics,
        DynamicsWorld world,
        ApproachFieldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(dynamics);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();

        Name = definition.Name;
        Obstacles = [.. definition.Obstacles];
        foreach (ObstacleDefinition obstacle in definition.Obstacles)
        {
            DynamicsBody body = PutInPlace(dynamics, world, obstacle);
            try
            {
                bodies.Add(body);
                obstacleByBody.Add(body.Handle.Value, obstacle.Id);
            }
            catch
            {
                body.Dispose();
                throw;
            }
        }
    }

    /// <summary>The name this approach space is charted under.</summary>
    internal string Name { get; }

    /// <summary>What stands in the space, in the order it was authored.</summary>
    internal IReadOnlyList<ObstacleDefinition> Obstacles { get; }

    /// <summary>
    /// Which authored obstacle an Engine body stands for, or nothing for a body
    /// this space did not put there. The hull is the obvious one: a contact
    /// between two authored obstacles and the hull names the obstacle on the
    /// other end of it and leaves the hull to the owner that asked.
    /// </summary>
    internal ObstacleId? ObstacleAt(DynamicsBodyReference body) =>
        obstacleByBody.TryGetValue(body.Value, out ObstacleId obstacle) ? obstacle : null;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (DynamicsBody body in bodies)
        {
            body.Dispose();
        }

        bodies.Clear();
        obstacleByBody.Clear();
    }

    private static DynamicsBody PutInPlace(
        IDynamicsService dynamics,
        DynamicsWorld world,
        ObstacleDefinition obstacle) => obstacle switch
    {
        Boulder boulder => dynamics.CreateSphereBodyWithProperties(
            new DynamicsCreateSphereBodyPropertiesRequest(
                world,
                new DynamicsSphereBodyPropertiesConfig(
                    Siting(obstacle),
                    ToSingle(boulder.Radius),
                    HeldStill(obstacle)))),
        WreckBlock block => dynamics.CreateCuboidBody(new DynamicsCreateCuboidBodyRequest(
            world,
            new DynamicsCuboidBodyConfig(
                Siting(obstacle),
                new Vector3(
                    ToSingle(block.HalfExtents.X),
                    ToSingle(block.HalfHeight),
                    ToSingle(block.HalfExtents.Z)),
                HeldStill(obstacle)))),
        _ => throw new ArgumentException(
            $"An approach obstacle is of a shape nothing knows how to place: {obstacle.Id}.",
            nameof(obstacle)),
    };

    private static Transform Siting(ObstacleDefinition obstacle) => new(
        new Vector3(ToSingle(obstacle.Position.X), ChartPlaneHeight, ToSingle(obstacle.Position.Z)),
        PlanarFrame.ToEngineAttitude(obstacle.HeadingRadians),
        Vector3.One);

    private static DynamicsBodyProperties HeldStill(ObstacleDefinition obstacle) => new(
        ToSingle(obstacle.Mass),
        new DynamicsMassPolicy(DynamicsMassPolicyKind.DeriveFromShapeAndMass, default),
        LinearVelocity: Vector3.Zero,
        AngularVelocity: Vector3.Zero,
        new AxisLocks(
            TranslationX: AxisLocked,
            TranslationY: AxisLocked,
            TranslationZ: AxisLocked,
            RotationX: AxisLocked,
            RotationY: AxisLocked,
            RotationZ: AxisLocked),
        LinearDamping: NoLinearDamping,
        AngularDamping: NoAngularDamping,
        GravityScale: NoGravityScale,
        Friction: ToSingle(obstacle.Friction),
        Restitution: NoRestitution,
        CollisionGroups: AllCollisionGroups,
        CollisionMask: AllCollisionGroups,
        Enabled: ObstacleEnabled,
        Sleeping: ObstacleAwake,
        ContinuousCollision: ObstaclesUseContinuousCollision);

    private static float ToSingle(double value) => checked((float)value);
}
