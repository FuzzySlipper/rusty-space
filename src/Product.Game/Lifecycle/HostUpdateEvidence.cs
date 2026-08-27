using Rusty.Engine;

namespace Rusty.Space.Product.Lifecycle;

/// <summary>
/// Host-provided admission evidence. It is not a local simulation timestep.
/// </summary>
public readonly record struct HostUpdateEvidence(
    uint Kind,
    ulong Observation,
    int InputCount)
{
    internal static HostUpdateEvidence From(ProductUpdate update) =>
        new(update.Kind, update.Observation, update.Input.Length);
}
