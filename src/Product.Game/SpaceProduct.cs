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
        // Create-time projection: the Engine retains this initial snapshot
        // alongside create outputs, before any update is admitted.
        composition.Presentation.Publish(composition.Flight.Readout);
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
        FollowCamera(ReadOnlySpan<ProductInputEvent>.Empty);
        lifecycle = SpaceLifecycleState.Running;
    }

    public void Attach()
    {
        // The Engine owns host attachment. Space retains its product state and
        // presentation across a browser reconnect, so attachment does not
        // start a second simulation or reset the flight model.
    }

    public ProductUpdateResult Update(ProductUpdate update)
    {
        RequireState(SpaceLifecycleState.Running, nameof(Update));
        FlightAdmission admission = composition.Flight.Admit(update);
        if (admission.FaultRequested)
        {
            // Operator abort (F): a product-owned terminal report; the turn
            // publishes nothing further.
            return ProductUpdateResult.ReportFault;
        }

        if (admission.Published)
        {
            composition.Presentation.Publish(composition.Flight.Readout);
        }

        FollowCamera(update.Input);
        if (!admission.Published)
        {
            return ProductUpdateResult.None;
        }

        lastHostUpdate = HostUpdateEvidence.From(update);
        admittedUpdateCount = checked(admittedUpdateCount + 1UL);
        return ProductUpdateResult.None;
    }

    // Space initiates no external timelines yet, so it accepts completions
    // addressed to it; a zero ticket is not a valid Engine ticket id.
    public bool CompleteTimeline(ProductTimelineCompletion completion)
    {
        RequireState(SpaceLifecycleState.Running, nameof(CompleteTimeline));
        return completion.Ticket != 0UL;
    }

    public void Restart()
    {
        RequireState(SpaceLifecycleState.Running, nameof(Restart));
        composition.Flight.ResetFlight();
        composition.Presentation.Publish(composition.Flight.Readout);
        FollowCamera(ReadOnlySpan<ProductInputEvent>.Empty);
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
        if (lifecycle is SpaceLifecycleState.Shutdown or SpaceLifecycleState.Disposed)
        {
            return;
        }

        // The Engine opens a staged service call around Shutdown. Retire the
        // retained projection here, but leave the terminal runtime/context to
        // reclaim its own lease-backed resources. The current safe C# surface
        // has no post-commit acknowledgement for transactional lease release.
        composition.Presentation.RetireRetainedSnapshot();
        lifecycle = SpaceLifecycleState.Shutdown;
    }

    public void Dispose()
    {
        if (lifecycle == SpaceLifecycleState.Disposed)
        {
            return;
        }

        // NativeAOT Destroy is deliberately not a staged Engine call. The
        // terminal Engine runtime/context reclaims its lease-backed resources,
        // and a create-time destroy must likewise avoid calling its services.
        lifecycle = SpaceLifecycleState.Disposed;
    }

    private void FollowCamera(ReadOnlySpan<ProductInputEvent> input) => composition.Camera.Follow(
        composition.Flight.Readout,
        composition.Flight.FixedStepCount,
        composition.Flight.ResetCount,
        input);

    private void RequireState(SpaceLifecycleState expected, string operation)
    {
        if (lifecycle != expected)
        {
            throw new InvalidOperationException(
                $"{operation} requires {expected} but Space is {lifecycle}.");
        }
    }
}
