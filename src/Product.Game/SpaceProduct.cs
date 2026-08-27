using Rusty.Engine;
using Rusty.Space.Product.Composition;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Lifecycle;

namespace Rusty.Space.Product;

/// <summary>
/// Product-owned lifecycle and admitted-update state around Engine Dynamics and Appearance facts.
/// The standard Engine host owns transport, control fencing, and output delivery.
/// </summary>
public sealed class SpaceProduct : IEngineProduct
{
    private readonly SpaceProductComposition composition;
    private SpaceLifecycleState lifecycle = SpaceLifecycleState.Created;
    private ulong admittedUpdateCount;
    private HostUpdateEvidence? lastHostUpdate;

    public SpaceProduct(ProductCreateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        composition = new SpaceProductComposition(context);
    }

    public SpaceProductStatus Status => new(
        lifecycle,
        composition.Content.FileCount,
        admittedUpdateCount,
        lastHostUpdate,
        composition.Flight.Readout,
        composition.Flight.FixedStepCount,
        composition.Flight.UpdateSequence);

    public void Start()
    {
        RequireState(SpaceLifecycleState.Created, nameof(Start));
        composition.Presentation.Publish(composition.Flight.Readout);
        lifecycle = SpaceLifecycleState.Running;
    }

    public void Update(ProductUpdate update)
    {
        RequireState(SpaceLifecycleState.Running, nameof(Update));
        FlightAdmission admission = composition.Flight.Admit(update);
        if (!admission.Published)
        {
            return;
        }

        composition.Presentation.Publish(composition.Flight.Readout);
        lastHostUpdate = HostUpdateEvidence.From(update);
        admittedUpdateCount = checked(admittedUpdateCount + 1UL);
    }

    public void Pause()
    {
        RequireState(SpaceLifecycleState.Running, nameof(Pause));
        lifecycle = SpaceLifecycleState.Paused;
    }

    public void Resume()
    {
        RequireState(SpaceLifecycleState.Paused, nameof(Resume));
        lifecycle = SpaceLifecycleState.Running;
    }

    public void Shutdown()
    {
        if (lifecycle == SpaceLifecycleState.Disposed)
        {
            throw new ObjectDisposedException(nameof(SpaceProduct));
        }

        lifecycle = SpaceLifecycleState.Shutdown;
    }

    public void Dispose()
    {
        if (lifecycle == SpaceLifecycleState.Disposed)
        {
            return;
        }

        if (lifecycle != SpaceLifecycleState.Shutdown)
        {
            Shutdown();
        }

        composition.Flight.Dispose();
        lifecycle = SpaceLifecycleState.Disposed;
    }

    private void RequireState(SpaceLifecycleState expected, string operation)
    {
        if (lifecycle != expected)
        {
            throw new InvalidOperationException(
                $"{operation} requires {expected} but Space is {lifecycle}.");
        }
    }
}
