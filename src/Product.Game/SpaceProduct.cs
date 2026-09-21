using Rusty.Engine;
using Rusty.Engine.Debugging;
using Rusty.Space.Product.Composition;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Lifecycle;

namespace Rusty.Space.Product;

/// <summary>
/// Product-owned lifecycle around the composed Space owners. The Engine host
/// owns transport, control fencing, and output delivery; this type decides what
/// the product does when the host admits a turn, a control, or a teardown.
/// </summary>
public sealed class SpaceProduct : IEngineProduct, IDebugCommandModuleSource
{
    private readonly SpaceProductComposition composition;
    private SpaceLifecycleState lifecycle = SpaceLifecycleState.Created;

    public SpaceProduct(ProductCreateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        SpaceProductComposition composed = new(context);
        try
        {
            composition = composed;
            // Create-time projection: the Engine retains this initial snapshot
            // alongside create outputs, before any update is admitted.
            PublishFlight();
        }
        catch
        {
            // A create that fails here leaves no product for anyone to dispose,
            // so every owner this constructor opened goes back down now. That is
            // what the lease contract covers: a release issued inside the create
            // call is committed or rolled back with it, so putting owners down
            // on the way out cannot desynchronize the failed create.
            composed.Dispose();
            throw;
        }
    }

    // The Engine generates the catalog and its dispatch; Space only names the
    // live owners worth reading. Commands report state and never write it.
    public void RegisterDebugCommands(IDebugCommandModuleRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        registrar.Register(composition.Debug);
    }

    public void Start()
    {
        RequireState(SpaceLifecycleState.Created, nameof(Start));
        PublishFlight();
        FollowCamera(ReadOnlySpan<ProductInputEvent>.Empty, TimeSpan.Zero);
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
            PublishFlight();
        }

        FollowCamera(update.Input, admission.TurnDuration);
        return ProductUpdateResult.None;
    }

    public void Restart()
    {
        RequireState(SpaceLifecycleState.Running, nameof(Restart));
        composition.Flight.ResetFlight();
        PublishFlight();
        FollowCamera(ReadOnlySpan<ProductInputEvent>.Empty, TimeSpan.Zero);
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

        // Shutdown arrives inside a staged Engine call, so the retained
        // projection is retired here while the services it references are still
        // reachable. The handles that snapshot pointed at are put down in
        // Dispose, after it no longer reads them.
        composition.Presentation.RetireRetainedSnapshot();
        lifecycle = SpaceLifecycleState.Shutdown;
    }

    public void Dispose()
    {
        if (lifecycle == SpaceLifecycleState.Disposed)
        {
            return;
        }

        // The generated host completes its lease coordinator terminally before
        // it calls this, and a lease handle released after that point drops its
        // release rather than issuing one. So putting the composed owners down
        // here cannot reach into a runtime that has already gone, while on any
        // earlier path the same calls release through the staged call they are
        // issued in.
        lifecycle = SpaceLifecycleState.Disposed;
        composition.Dispose();
    }

    private void PublishFlight() => composition.Presentation.Publish(
        composition.Flight.Readout,
        composition.Flight.Telemetry,
        composition.Flight.Contributions,
        composition.Flight.ProjectedPath,
        composition.Flight.Ship);

    private void FollowCamera(ReadOnlySpan<ProductInputEvent> input, TimeSpan turnDuration)
        => composition.Camera.Follow(
            composition.Flight.Readout,
            turnDuration,
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
