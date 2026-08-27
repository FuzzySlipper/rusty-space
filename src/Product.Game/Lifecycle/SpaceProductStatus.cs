using Rusty.Space.Product.Flight;

namespace Rusty.Space.Product.Lifecycle;

public readonly record struct SpaceProductStatus(
    SpaceLifecycleState Lifecycle,
    int ContentFileCount,
    ulong AdmittedUpdateCount,
    HostUpdateEvidence? LastHostUpdate,
    FlightReadout FlightReadout,
    ulong FixedStepCount,
    ulong UpdateSequence);
