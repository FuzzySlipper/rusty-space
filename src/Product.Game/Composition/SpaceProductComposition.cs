using Rusty.Engine;
using Rusty.Space.Product.Content;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Presentation;
using Rusty.Space.Product.Tuning;

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
            SpacePresentation presentation = new(Engine.Appearance, Tuning.Field, Tuning.Presentation);
            Flight = flight;
            Presentation = presentation;
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
}
