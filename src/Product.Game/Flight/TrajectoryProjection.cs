using System;
using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.ShipSystems;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// Walks the hull's line forward on the Engine's kinematic lane, so the
/// navigation view can show where the ship is on its way before it gets there.
/// </summary>
/// <remarks>
/// <para>
/// The Engine owns every integration in this product, and a projected line is an
/// integration like any other. Each sample asks the Engine's call-local kinematic
/// integrator to advance the hull's own state over whole fixed steps, handed the
/// acceleration the same force rule that moves the hull says applies at the point
/// the line has reached. Nothing here adds a private integrator, an accumulator,
/// or a second clock: the projection is a caller of a lane the Engine already
/// publishes, at the same fixed interval the admitted turn runs on.
/// </para>
/// <para>
/// The controls are held as they are — the bow keeps its heading, the drive keeps
/// what it is delivering, the coupling keeps its trim — while the environment is
/// re-read at each point along the way. That combination is the point: the line
/// curves toward a band the ship has not entered yet, and stops promising
/// anything the moment the ship's actual state moves off it, because the next
/// turn rebuilds the whole line from where the hull really is.
/// </para>
/// </remarks>
internal sealed class TrajectoryProjection
{
    // The product's orbital well is a product force handed to the integrator as
    // acceleration, so the Engine's own gravity term stays out of it.
    private static readonly Vector3 NoEngineGravity = Vector3.Zero;
    private const float NoGravityScale = 0.0f;

    private readonly IKinematicService kinematic;
    private readonly HullForceModel forceModel;
    private readonly StellarField field;
    private readonly TrajectoryTuning tuning;

    internal TrajectoryProjection(
        IKinematicService kinematic,
        HullForceModel forceModel,
        StellarField field,
        TrajectoryTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(kinematic);
        ArgumentNullException.ThrowIfNull(forceModel);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(tuning);

        this.kinematic = kinematic;
        this.forceModel = forceModel;
        this.field = field;
        this.tuning = tuning.Validate();
    }

    /// <summary>
    /// The line the hull is on from this state, sampled forward. The effort is
    /// what the fitted hardware reached on the last admitted substep and is held
    /// there, which is what makes the line answer the throttle: a drive still
    /// spooling up carries the line farther than one just released.
    /// </summary>
    internal FlightPath Project(
        FlightBodyState from,
        InstalledShip ship,
        ShipEffort heldEffort,
        double mass,
        TimeSpan fixedStep)
    {
        ArgumentNullException.ThrowIfNull(ship);
        if (!double.IsFinite(mass) || mass <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(mass));
        }

        PlanarVector[] points = new PlanarVector[tuning.SampleCount];
        PlanarVector position = from.Position;
        PlanarVector velocity = from.LinearVelocity;
        double secondsPerTick = fixedStep.TotalSeconds;
        for (int sample = 0; sample < points.Length; sample++)
        {
            // The environment is read where the line has got to, not where the
            // ship is: a current ahead is part of the answer the view is for.
            FlightBodyState stateAtPoint = new(
                position,
                from.HeadingRadians,
                velocity,
                from.AngularVelocity);
            FlightForces push = forceModel.Resolve(
                stateAtPoint,
                ship,
                field.Sample(position),
                heldEffort,
                mass);
            PlanarVector acceleration = push.Total.Force.Scale(1.0 / mass);
            IntegrationResult stepped = kinematic.Integrate(new KinematicIntegrationRequest(
                new KinematicBody(
                    ToEngine(position),
                    ToEngine(velocity),
                    ToEngine(acceleration),
                    NoGravityScale,
                    KinematicCollisionMode.None),
                new PhysicsSettings(NoEngineGravity),
                new PhysicsStep((ulong)tuning.TicksPerSample, ToSingle(secondsPerTick))));
            position = FromEngine(stepped.NextPosition);
            velocity = FromEngine(stepped.NextVelocity);
            points[sample] = position;
        }

        return new FlightPath(
            points,
            TimeSpan.FromSeconds(tuning.TicksPerSample * secondsPerTick));
    }

    private static Vector3 ToEngine(PlanarVector planar) =>
        new(ToSingle(planar.X), 0.0f, ToSingle(planar.Z));

    private static PlanarVector FromEngine(Vector3 engine) => new(engine.X, engine.Z);

    private static float ToSingle(double value) => checked((float)value);
}
