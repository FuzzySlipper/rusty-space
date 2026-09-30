namespace Rusty.Space.Product.Flight;

/// <summary>The last Dynamics receipt, for observation of the simulated world.</summary>
internal readonly record struct DynamicsStepObservation(ulong Generation, uint BodyCount, uint ContactCount);
