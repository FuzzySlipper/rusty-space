using Rusty.Engine;
using Rusty.Space.Product.Bridge;
using Rusty.Space.Product.Debugging;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Presentation;
using Rusty.Space.Product.Tuning;
using Rusty.Space.Product.Viewing;

namespace Rusty.Space.Product.Composition;

internal sealed class SpaceProductComposition : IDisposable
{
    internal SpaceProductComposition(ProductCreateContext context)
    {
        Engine = context.Engine;
        Tuning = SpaceTuning.Defaults.Validate();
        SpaceFlight flight = new(
            Engine.Dynamics,
            Engine.Kinematic,
            Tuning.Flight,
            Tuning.Coupling,
            Tuning.FlightBody,
            Tuning.Ship,
            Tuning.Damage,
            Tuning.Thermal,
            Tuning.Reserve,
            Tuning.Field,
            Tuning.Orbital,
            Tuning.GentleCurrent,
            Tuning.SwiftCurrent,
            Tuning.Trajectory,
            Tuning.Approach);
        BridgeSet? bridge = null;
        BridgeTheater? theater = null;
        SpacePresentation? presentation = null;
        TrackingCamera? camera = null;
        HelmCamera? helm = null;
        try
        {
            // The set stages its meshes once here and retains them for the
            // scene lifetime; the theater below only moves its lights, prop,
            // voices, and repeater, and the projection republishes the
            // resulting facts each turn.
            bridge = new BridgeSet(
                Engine.Graphics,
                Engine.ImplicitSurfaces,
                Tuning.Bridge);
            theater = new BridgeTheater(
                Engine.Audio,
                bridge,
                Tuning.Theater);
            presentation = new SpacePresentation(
                Engine.Graphics,
                Engine.Ui,
                flight.Environment,
                flight.Approach,
                bridge,
                theater,
                Tuning.Presentation,
                Tuning.Overlay);
            camera = new TrackingCamera(
                Engine.CameraView,
                Tuning.Camera,
                flight.Readout,
                flight.ResetCount);
            helm = new HelmCamera(Engine.CameraView, Tuning.Bridge, Tuning.HelmCamera);
            Flight = flight;
            Bridge = bridge;
            Theater = theater;
            Presentation = presentation;
            Camera = camera;
            Helm = helm;
            Seated = false;
            BridgeDebug = new BridgeDebugModule(bridge, theater, () => Seated);
        }
        catch
        {
            // Whatever got as far as opening Engine handles is put back down in
            // the reverse of the order that opened it, so a create that fails
            // partway leaves no owner holding a handle nobody can reach.
            ReleaseOwners(helm, camera, presentation, theater, bridge, flight);
            throw;
        }
    }

    internal IEngineContext Engine { get; }

    internal SpaceTuning Tuning { get; }

    internal SpaceFlight Flight { get; }

    internal BridgeSet Bridge { get; }

    internal BridgeTheater Theater { get; }

    internal SpacePresentation Presentation { get; }

    internal TrackingCamera Camera { get; }

    internal HelmCamera Helm { get; }

    internal bool Seated { get; private set; }

    internal BridgeDebugModule BridgeDebug { get; }

    /// <summary>
    /// The sit-at-helm toggle: sitting activates the helm camera, standing
    /// returns to the chart. Cameras frame; they never write flight state.
    /// </summary>
    internal void SetSeated(bool seated)
    {
        Seated = seated;
        if (seated)
        {
            Helm.Activate();
        }
        else
        {
            Camera.Activate();
        }
    }

    /// <summary>
    /// Puts the composed owners down in the reverse of the order that built
    /// them: the cameras frame the flight and the set they read, the
    /// projection publishes about them, and the theater moves only what the
    /// projection republishes, so each is released before what it depends on.
    /// </summary>
    public void Dispose()
    {
        ReleaseOwners(Helm, Camera, Presentation, Theater, Bridge, Flight);
    }

    private static void ReleaseOwners(params IDisposable?[] owners)
    {
        List<Exception> errors = [];
        foreach (IDisposable? owner in owners)
        {
            try
            {
                owner?.Dispose();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException(errors).Flatten();
        }
    }
}
