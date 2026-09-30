using System.Text;
using Rusty.Engine;
using Rusty.Engine.Debugging;
using Rusty.Space.Product.Debugging;
using Rusty.Space.Product.Engine.Tests;
using Xunit;

namespace Rusty.Space.Product.Tests;

public class SpaceProductLifecycleTests
{
    [Fact]
    public void LifecycleGuardsRefuseTurnsOutsideRunningAndAllowResume()
    {
        RecordingEngine engine = new();
        using SpaceProduct product = new(ProductContexts.For(engine));
        Assert.Throws<InvalidOperationException>(() => product.Update(Turn()));
        Assert.Throws<InvalidOperationException>(product.Resume);
        product.Start();
        Assert.Throws<InvalidOperationException>(product.Start);
        product.Pause();
        int steps = engine.Dynamics.Steps.Count;
        Assert.Throws<InvalidOperationException>(() => product.Update(Turn()));
        Assert.Equal(steps, engine.Dynamics.Steps.Count);
        Assert.Throws<InvalidOperationException>(product.Pause);
        product.Resume();
        Assert.Equal(ProductUpdateResult.None, product.Update(Turn()));
        Assert.Single(engine.Dynamics.Steps);
    }

    [Fact]
    public void ShutdownRetiresTheSnapshotOnceBeforeDisposeReleasesResources()
    {
        RecordingEngine engine = new();
        SpaceProduct product = new(ProductContexts.For(engine));
        product.Start();
        int publications = engine.Graphics.SnapshotPublications;
        product.Shutdown();
        product.Shutdown();
        Assert.Equal(publications + 1, engine.Graphics.SnapshotPublications);
        Assert.Empty(engine.Graphics.LastSnapshot);
        Assert.Equal(0, engine.Dynamics.BodyReleases);
        Assert.Throws<InvalidOperationException>(() => product.Update(Turn()));
        product.Dispose();
        product.Dispose();
        product.Shutdown();
        Assert.Equal(engine.Dynamics.BodyCreates, engine.Dynamics.BodyReleases);
        Assert.Throws<InvalidOperationException>(product.Start);
    }

    [Fact]
    public void ARefusedSnapshotRetirementCanBeRetriedWithoutSkippingResources()
    {
        RecordingEngine engine = new();
        using SpaceProduct product = new(ProductContexts.For(engine));
        engine.Faults.FailOn = nameof(IGraphicsService.PublishSnapshot);
        Assert.Throws<InjectedFault>(product.Shutdown);
        product.Shutdown();
        Assert.Empty(engine.Graphics.LastSnapshot);
    }

    [Fact]
    public void AbortReportsATerminalFaultAndPublishesNoFurtherPresentation()
    {
        RecordingEngine engine = new();
        using SpaceProduct product = new(ProductContexts.For(engine));
        product.Start();
        int publications = engine.Graphics.SnapshotPublications;
        int voiceUpdates = engine.Audio.VoiceUpdates;
        Assert.Equal(ProductUpdateResult.ReportFault, product.Update(Turn("space.flight.abort")));
        Assert.Equal(publications, engine.Graphics.SnapshotPublications);
        Assert.Equal(voiceUpdates, engine.Audio.VoiceUpdates);
    }

    [Fact]
    public void RegisteredDebugOwnersReportStateWithoutAdvancingOrPublishing()
    {
        RecordingEngine engine = new();
        using SpaceProduct product = new(ProductContexts.For(engine));
        List<IDebugCommandModule> modules = [];
        IDebugCommandModuleRegistrar registrar = RecordingServiceProxy.Create<IDebugCommandModuleRegistrar>((_, args) =>
        {
            modules.Add((IDebugCommandModule)args![0]!);
            return new DebugCommandRegistrationResult(DebugCommandRegistrationStatus.Registered, "recorded");
        });
        product.RegisterDebugCommands(registrar);
        FlightDebugModule flight = Assert.Single(modules.OfType<FlightDebugModule>());
        BridgeDebugModule bridge = Assert.Single(modules.OfType<BridgeDebugModule>());
        int publications = engine.Graphics.SnapshotPublications;
        Assert.Contains("fixed step", flight.Forces());
        Assert.Contains("heading", flight.Attitude());
        Assert.Contains("fit", flight.Hardware());
        Assert.Contains("since this hull was built", flight.Impacts());
        Assert.Contains("flow", flight.Field());
        Assert.Contains("nothing projected", flight.Path());
        Assert.Contains("at chart", bridge.Bridge());
        Assert.Empty(engine.Dynamics.Steps);
        Assert.Equal(publications, engine.Graphics.SnapshotPublications);
    }

    private static ProductUpdate Turn(string? intent = null) => new(
        new ProductUpdateFacts(ProductUpdateMode.Realtime, ProductLifecycleState.Running,
            Generation: 1, ControlRevision: 0, ObservedHostTimeNanoseconds: 0, SimulationStep: 0,
            FixedStepHz: 60, AdmittedStepCount: 1, DroppedStepCount: 0, FixedDeltaSeconds: 1.0 / 60.0),
        intent is null ? Array.Empty<ProductInputEvent>() : new ProductInputEvent[] { new()
        {
            Kind = InputEventKind.MappedDigital,
            Phase = InputPhase.Pressed,
            X = 1,
            Intent = Encoding.UTF8.GetBytes(intent),
        } });
}
