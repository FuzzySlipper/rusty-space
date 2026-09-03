using Rusty.Engine;
using Rusty.Space.Product.Content;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Presentation;
using Rusty.Space.Product.Tuning;
using Rusty.Space.Product.Viewing;

namespace Rusty.Space.Product.Composition;

internal sealed class SpaceProductComposition
{
    internal SpaceProductComposition(ProductCreateContext context)
    {
        Engine = context.Engine;
        Content = SpaceContent.From(context.Content);
        Tuning = SpaceTuning.Defaults.Validate();
        SpaceFlight flight = new(
            Engine.Dynamics,
            Tuning.Flight,
            Tuning.FlightBody,
            Tuning.Field);
        try
        {
            SpacePresentation presentation = new(Engine.Appearance, Engine.Ui, Tuning.Field, Tuning.Presentation);
            TrackingCamera camera = new(
                Engine.CameraView,
                Tuning.Camera,
                flight.Readout,
                flight.FixedStepCount,
                flight.ResetCount);
            Flight = flight;
            Presentation = presentation;
            Camera = camera;
        }
        catch
        {
            flight.Dispose();
            throw;
        }
    }

    internal IEngineContext Engine { get; }

    internal SpaceContent Content { get; }

    internal SpaceTuning Tuning { get; }

    internal SpaceFlight Flight { get; }

    internal SpacePresentation Presentation { get; }

    internal TrackingCamera Camera { get; }

}
