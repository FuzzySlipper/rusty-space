using Rusty.Engine;
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
        SpacePresentation? presentation = null;
        TrackingCamera? camera = null;
        try
        {
            presentation = new SpacePresentation(
                Engine.Graphics,
                Engine.Ui,
                flight.Environment,
                flight.Approach,
                Tuning.Presentation,
                Tuning.Overlay);
            camera = new TrackingCamera(
                Engine.CameraView,
                Tuning.Camera,
                flight.Readout,
                flight.ResetCount);
            Flight = flight;
            Presentation = presentation;
            Camera = camera;
            Debug = new FlightDebugModule(flight);
        }
        catch
        {
            // Whatever got as far as opening Engine handles is put back down in
            // the reverse of the order that opened it, so a create that fails
            // partway leaves no owner holding a handle nobody can reach.
            camera?.Dispose();
            presentation?.Dispose();
            flight.Dispose();
            throw;
        }
    }

    internal IEngineContext Engine { get; }

    internal SpaceTuning Tuning { get; }

    internal SpaceFlight Flight { get; }

    internal SpacePresentation Presentation { get; }

    internal TrackingCamera Camera { get; }

    internal FlightDebugModule Debug { get; }

    /// <summary>
    /// Puts the composed owners down in the reverse of the order that built
    /// them: the camera frames the flight it reads and the projection publishes
    /// facts about it, so each is released before what it depends on.
    /// </summary>
    public void Dispose()
    {
        Camera.Dispose();
        Presentation.Dispose();
        Flight.Dispose();
    }
}
