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
            Tuning.Field,
            Tuning.Orbital,
            Tuning.GentleCurrent,
            Tuning.SwiftCurrent,
            Tuning.Trajectory,
            Tuning.Approach);
        BridgeSet? bridge = null;
        SpacePresentation? presentation = null;
        TrackingCamera? camera = null;
        try
        {
            // The set stages its meshes once here and retains them for the
            // scene lifetime; the projection below only republishes its
            // precomputed stationary facts each turn.
            bridge = new BridgeSet(
                Engine.Graphics,
                Engine.ImplicitSurfaces,
                Tuning.Bridge);
            presentation = new SpacePresentation(
                Engine.Graphics,
                Engine.Ui,
                flight.Environment,
                flight.Approach,
                bridge,
                Tuning.Presentation,
                Tuning.Overlay);
            camera = new TrackingCamera(
                Engine.CameraView,
                Tuning.Camera,
                flight.Readout,
                flight.ResetCount);
            Flight = flight;
            Bridge = bridge;
            Presentation = presentation;
            Camera = camera;
            Debug = new FlightDebugModule(flight);
            BridgeDebug = new BridgeDebugModule(bridge);
        }
        catch
        {
            // Whatever got as far as opening Engine handles is put back down in
            // the reverse of the order that opened it, so a create that fails
            // partway leaves no owner holding a handle nobody can reach.
            camera?.Dispose();
            presentation?.Dispose();
            bridge?.Dispose();
            flight.Dispose();
            throw;
        }
    }

    internal IEngineContext Engine { get; }

    internal SpaceTuning Tuning { get; }

    internal SpaceFlight Flight { get; }

    internal BridgeSet Bridge { get; }

    internal SpacePresentation Presentation { get; }

    internal TrackingCamera Camera { get; }

    internal FlightDebugModule Debug { get; }

    internal BridgeDebugModule BridgeDebug { get; }

    /// <summary>
    /// Puts the composed owners down in the reverse of the order that built
    /// them: the camera frames the flight it reads and the projection publishes
    /// facts about the flight and the set, so each is released before what it
    /// depends on.
    /// </summary>
    public void Dispose()
    {
        Camera.Dispose();
        Presentation.Dispose();
        Bridge.Dispose();
        Flight.Dispose();
    }
}
