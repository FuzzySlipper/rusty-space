using Rusty.Engine;
using Rusty.Engine.Debugging;
using Rusty.Space.Product.Composition;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Debugging;
using Rusty.Space.Product.ShipSystems;
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
    private readonly FlightDebugModule debugCommands;
    private SpaceLifecycleState lifecycle = SpaceLifecycleState.Created;

    public SpaceProduct(ProductCreateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        SpaceProductComposition composed = new(context);
        try
        {
            composition = composed;
            debugCommands = new FlightDebugModule(composed.Flight, SelectLoadout);
            // Create-time projection: the Engine retains this initial snapshot
            // alongside create outputs, before any update is admitted.
            PublishFlight();
        }
        catch
        {
            // A failed create keeps its completed Engine calls. No product
            // reaches the caller, so release every owner opened here.
            composed.Dispose();
            throw;
        }
    }

    // The Engine generates the catalog and its dispatch; Space only names the
    // live owners worth reading and the explicit loadout selection controls.
    public void RegisterDebugCommands(IDebugCommandModuleRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        registrar.Register(debugCommands);
        registrar.Register(composition.BridgeDebug);
    }

    public void Start()
    {
        RequireState(SpaceLifecycleState.Created, nameof(Start));
        PublishFlight();
        FollowCamera(ReadOnlySpan<ProductInputEvent>.Empty, TimeSpan.Zero);
        lifecycle = SpaceLifecycleState.Running;
    }

    public ProductUpdateResult Update(ProductUpdate update)
    {
        RequireState(SpaceLifecycleState.Running, nameof(Update));
        if (SitTogglePressed(update.Input))
        {
            composition.SetSeated(!composition.Seated);
        }

        FlightAdmission admission = composition.Flight.Admit(update);
        if (admission.ResetOccurred)
        {
            composition.Theater.Reset();
        }

        if (admission.FaultRequested)
        {
            // Operator abort (F): a product-owned terminal report; the turn
            // publishes nothing further.
            return ProductUpdateResult.ReportFault;
        }

        // The theater reads the telemetry the turn admitted and stages its
        // reactions before the projection republishes: filters always track,
        // Engine calls happen only for enabled reactions on a published turn.
        if (!admission.ResetOccurred)
        {
            composition.Theater.Advance(
                composition.Flight.Telemetry,
                composition.Flight.Ship,
                composition.Flight.ImpactCount,
                admission.TurnDuration,
                admission.Published);
        }

        if (admission.Published)
        {
            PublishFlight();
        }

        FollowCamera(update.Input, admission.TurnDuration);
        // The helm pose is published only while sat in it: at the chart the
        // camera needs no upkeep, and with the reactions off nothing here may
        // stage. Sitting recomputes the leaned pose on its first turn.
        if (composition.Seated)
        {
            composition.Helm.Follow(composition.Theater.Lean);
        }

        return ProductUpdateResult.None;
    }

    public void Restart()
    {
        if (lifecycle is not (SpaceLifecycleState.Running or SpaceLifecycleState.Paused))
        {
            throw new InvalidOperationException($"Restart requires a running or paused product; current state is {lifecycle}.");
        }
        composition.Flight.ResetFlight();
        PublishReset();
    }

    private void SelectLoadout(ShipLoadout loadout)
    {
        if (lifecycle is not (SpaceLifecycleState.Running or SpaceLifecycleState.Paused))
        {
            throw new InvalidOperationException($"Loadout selection requires a running or paused product; current state is {lifecycle}.");
        }
        composition.Flight.Refit(loadout);
        PublishReset();
    }

    private void PublishReset()
    {
        composition.Theater.Reset();
        PublishFlight();
        FollowCamera(ReadOnlySpan<ProductInputEvent>.Empty, TimeSpan.Zero);
        if (composition.Seated)
        {
            composition.Helm.Follow(composition.Theater.Lean);
        }
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

        // Shutdown is a product call made while the Engine services are still
        // reachable, so the retained projection is retired here. The handles that snapshot pointed at are put down in
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

        // Each owner's Dispose releases its Engine resources immediately
        // (Engine #8736); there is no call-scoped release to wait for.
        lifecycle = SpaceLifecycleState.Disposed;
        composition.Dispose();
    }

    private void PublishFlight() => composition.Presentation.Publish(
        composition.Flight.Readout,
        composition.Flight.Telemetry,
        composition.Flight.Coupling,
        composition.Flight.Contributions,
        composition.Flight.ProjectedPath,
        composition.Flight.LastStrike,
        composition.Flight.Ship);

    private void FollowCamera(ReadOnlySpan<ProductInputEvent> input, TimeSpan turnDuration)
        => composition.Camera.Follow(
            composition.Flight.Readout,
            turnDuration,
            composition.Flight.ResetCount,
            input);

    /// <summary>
    /// The sit-at-helm toggle (C): a pressed edge on the declared digital
    /// intent, the same one-shot shape as the stabilizer switch. The flight
    /// mapper ignores the intent; only the seat answers it.
    /// </summary>
    private static bool SitTogglePressed(ReadOnlySpan<ProductInputEvent> input)
    {
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.Kind == InputEventKind.MappedDigital
                && inputEvent.Phase == InputPhase.Pressed
                && inputEvent.Intent.Span.SequenceEqual(SitToggleIntent))
            {
                return true;
            }
        }

        return false;
    }

    private static ReadOnlySpan<byte> SitToggleIntent => "space.bridge.sit"u8;

    private void RequireState(SpaceLifecycleState expected, string operation)
    {
        if (lifecycle != expected)
        {
            throw new InvalidOperationException(
                $"{operation} requires {expected} but Space is {lifecycle}.");
        }
    }
}
