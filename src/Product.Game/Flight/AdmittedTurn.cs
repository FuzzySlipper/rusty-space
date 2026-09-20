using Rusty.Engine;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// The simulated time one admitted update turn carries, taken from the
/// Engine's own facts. The Engine derives the fixed delta from the lifecycle
/// rate configured for the product, so no product owner restates that rate: a
/// turn is the fixed step it was admitted to simulate and how many of them.
/// </summary>
internal readonly record struct AdmittedTurn(double FixedDeltaSeconds, uint StepCount)
{
    /// <summary>
    /// One fixed step of admitted simulated time.
    /// </summary>
    internal TimeSpan FixedStep => TimeSpan.FromSeconds(FixedDeltaSeconds);

    /// <summary>
    /// How much simulated time the whole turn covers: the admitted count at the
    /// admitted rate, from the same facts rather than a second clock.
    /// </summary>
    internal TimeSpan Duration => TimeSpan.FromSeconds(FixedDeltaSeconds * StepCount);

    /// <summary>
    /// The admitted fixed step in the seconds form a Dynamics step request
    /// takes, carried from the fact itself rather than rebuilt from a constant.
    /// </summary>
    internal float DynamicsStepSeconds => checked((float)FixedDeltaSeconds);

    /// <summary>
    /// The one place Space checks admitted time. A realtime lifecycle carries a
    /// positive finite fixed delta; a turn that was admitted steps without one
    /// is not something the product can integrate step by step. Checking it
    /// here, once, is what lets every owner downstream take the step it is
    /// handed as given.
    /// </summary>
    internal static AdmittedTurn FromFacts(ProductUpdateFacts facts)
    {
        if (!double.IsFinite(facts.FixedDeltaSeconds)
            || facts.FixedDeltaSeconds <= 0.0)
        {
            throw new InvalidOperationException(
                $"Space cannot simulate {facts.AdmittedStepCount} admitted step(s) at a fixed delta of {facts.FixedDeltaSeconds}; only a realtime lifecycle carries a usable one.");
        }

        return new AdmittedTurn(facts.FixedDeltaSeconds, facts.AdmittedStepCount);
    }
}
