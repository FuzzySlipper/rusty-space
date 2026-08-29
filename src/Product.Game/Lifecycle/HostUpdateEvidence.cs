using Rusty.Engine;

namespace Rusty.Space.Product.Lifecycle;

/// <summary>
/// Host-provided admission evidence. It is not a local simulation timestep.
/// </summary>
public readonly record struct HostUpdateEvidence(
    ProductUpdateMode Mode,
    ulong ObservedHostTimeNanoseconds,
    int InputCount)
{
    internal static HostUpdateEvidence From(ProductUpdate update) =>
        new(update.Facts.Mode, update.Facts.ObservedHostTimeNanoseconds, update.Input.Length);
}
