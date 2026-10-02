using System.Text;
using System.Text.Json;
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
        PlaytestDebugModule playtest = Assert.Single(modules.OfType<PlaytestDebugModule>());
        int publications = engine.Graphics.SnapshotPublications;
        Assert.Contains("fixed step", flight.Forces());
        Assert.Contains("heading", flight.Attitude());
        Assert.Contains("fit", flight.Hardware());
        Assert.Contains("since this hull was built", flight.Impacts());
        Assert.Contains("flow", flight.Field());
        Assert.Contains("nothing projected", flight.Path());
        Assert.Contains("at chart", bridge.Bridge());
        Assert.Equal(DebugCommandStatus.Success, playtest.Observe().Status);
        Assert.Equal(DebugCommandStatus.InvalidArguments, playtest.Look(20.0, 0.0).Status);
        Assert.Empty(engine.Dynamics.Steps);
        Assert.Equal(publications, engine.Graphics.SnapshotPublications);
    }

    [Fact]
    public void PlaytestActionsFollowTheAdmittedBindingInsteadOfAPrivateKeyTable()
    {
        RecordingEngine engine = new();
        ProductInputMapping thrust = new()
        {
            Intent = "space.flight.thrust"u8.ToArray(),
            Keyboard = KeyboardControl.KeyB,
            Edge = InputEdge.Held,
        };
        ProductCreateContext context = new(engine.Context, new ProductContent(default),
            new ProductInputConfiguration(default, default, default, new[] { thrust }));
        using SpaceProduct product = new(context);
        List<IDebugCommandModule> modules = [];
        product.RegisterDebugCommands(RecordingServiceProxy.Create<IDebugCommandModuleRegistrar>((_, args) =>
        {
            modules.Add((IDebugCommandModule)args![0]!);
            return new DebugCommandRegistrationResult(DebugCommandRegistrationStatus.Registered, "recorded");
        }));
        PlaytestDebugModule playtest = Assert.Single(modules.OfType<PlaytestDebugModule>());
        using JsonDocument plan = JsonDocument.Parse(playtest.Action("thrust").Message);
        Assert.Equal("KeyB", plan.RootElement.GetProperty("key").GetString());
        Assert.True(plan.RootElement.GetProperty("available").GetBoolean());
        using JsonDocument missing = JsonDocument.Parse(playtest.Action("patch").Message);
        Assert.False(missing.RootElement.GetProperty("available").GetBoolean());
        Assert.Empty(engine.Dynamics.Steps);
    }

    [Fact]
    public void RestartWhilePausedRebuildsAndPublishesWithoutResumingFlight()
    {
        RecordingEngine engine = new();
        using SpaceProduct product = new(ProductContexts.For(engine));
        product.Start();
        product.Pause();
        int bodies = engine.Dynamics.BodyCreates;
        int publications = engine.Graphics.SnapshotPublications;
        product.Restart();
        Assert.Equal(bodies + 1, engine.Dynamics.BodyCreates);
        Assert.Equal(publications + 1, engine.Graphics.SnapshotPublications);
        Assert.Throws<InvalidOperationException>(() => product.Update(Turn()));
        product.Resume();
        product.Update(Turn());
        Assert.Single(engine.Dynamics.Steps);
    }

    [Fact]
    public void DebugSelectionPublishesEachAuthoredFitAndPreservesPause()
    {
        RecordingEngine engine = new();
        using SpaceProduct product = new(ProductContexts.For(engine));
        List<IDebugCommandModule> modules = [];
        product.RegisterDebugCommands(RecordingServiceProxy.Create<IDebugCommandModuleRegistrar>((_, args) =>
        {
            modules.Add((IDebugCommandModule)args![0]!);
            return new DebugCommandRegistrationResult(DebugCommandRegistrationStatus.Registered, "recorded");
        }));
        FlightDebugModule debug = Assert.Single(modules.OfType<FlightDebugModule>());
        Assert.Throws<InvalidOperationException>(() => debug.ScavengedLoadout());
        product.Start();
        product.Pause();
        int publications = engine.Graphics.SnapshotPublications;
        Assert.Contains("oversized-scavenged-emitter", debug.ScavengedLoadout());
        Assert.Contains("damaged-stabilizer", debug.DamagedLoadout());
        Assert.Contains("stock", debug.StockLoadout());
        Assert.Equal(publications + 3, engine.Graphics.SnapshotPublications);
        Assert.Empty(engine.Dynamics.Steps);
        Assert.Throws<InvalidOperationException>(() => product.Update(Turn()));
        product.Shutdown();
        Assert.Throws<InvalidOperationException>(() => debug.StockLoadout());
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
