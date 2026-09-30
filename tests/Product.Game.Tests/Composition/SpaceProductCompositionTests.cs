using Rusty.Engine;
using Rusty.Space.Product.Engine.Tests;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Tests;

/// <summary>
/// What happens to what Space opened when a create dies partway through. The
/// composition builds flight, then the bridge set, then its theater, then the
/// projection, then the cameras, and the product publishes once the whole
/// thing stands; each of those stages can fail, and the owners a failed
/// construction had already opened cannot be left for a product nobody can
/// reach. A failed call keeps what it did (Engine #8736), so an owner whose own
/// constructor fails also releases what it had opened before it failed.
/// </summary>
public class SpaceProductCompositionTests
{
    [Fact]
    public void AComposedProductPutsDownEveryOwnerItOpened()
    {
        RecordingEngine engine = new();

        SpaceProduct product = new(ProductContexts.For(engine));
        product.Dispose();

        Assert.Equal(1, engine.Graphics.SnapshotPublications);
        Assert.Equal(engine.Graphics.AppearanceCreates, engine.Graphics.AppearanceReleases);
        Assert.Equal(engine.Graphics.LightCreates, engine.Graphics.LightReleases);
        Assert.Equal(engine.ImplicitSurfaces.MeshCreates, engine.ImplicitSurfaces.MeshReleases);
        Assert.Equal(engine.Graphics.MaterialCreates, engine.Graphics.MaterialReleases);
        Assert.Equal(engine.Audio.VoiceDescriptors.Count, engine.Audio.VoiceReleases);
        Assert.Equal(engine.Audio.OpenedClipPaths.Count, engine.Audio.ClipReleases);
        Assert.Equal(2, engine.CameraView.CameraReleases);
        Assert.Equal(
            ["camera", "ui", "appearance", "voice", "clip", "light", "appearance", "mesh", "material", "body", "world"],
            engine.Faults.OwnersReleasedInOrder());
    }

    [Fact]
    public void ACameraThatFailsToActivateReleasesTheProjectionAndFlight()
    {
        // Camera creation precedes camera activation, so a failure at the second
        // step means the product was holding a bridge set, its theater, a
        // projection, and a flight and had only just failed to acquire a view.
        RecordingEngine engine = new();
        engine.Faults.FailOn = nameof(ICameraViewService.SetActiveCamera);

        Assert.Throws<InjectedFault>(() => new SpaceProduct(ProductContexts.For(engine)));

        Assert.Equal(1, engine.CameraView.CameraReleases);
        Assert.Equal(1, engine.Ui.StreamReleases);
        Assert.Equal(
            engine.Graphics.AppearanceCreates,
            engine.Graphics.AppearanceReleases);
        Assert.Equal(engine.Graphics.LightCreates, engine.Graphics.LightReleases);
        Assert.Equal(engine.ImplicitSurfaces.MeshCreates, engine.ImplicitSurfaces.MeshReleases);
        Assert.Equal(engine.Graphics.MaterialCreates, engine.Graphics.MaterialReleases);
        Assert.Equal(engine.Audio.VoiceDescriptors.Count, engine.Audio.VoiceReleases);
        Assert.Equal(engine.Audio.OpenedClipPaths.Count, engine.Audio.ClipReleases);
        Assert.Equal(engine.Dynamics.BodyCreates, engine.Dynamics.BodyReleases);
        Assert.Equal(1, engine.Dynamics.WorldReleases);
        AssertReleasedDeepestFirst(engine);
    }

    [Fact]
    public void AProjectionThatFailsToOpenItsStreamReleasesTheSetAndFlight()
    {
        // The projection's constructor opens its whole set of appearances and
        // then fails on its stream. It releases the appearances it opened, and
        // the composition puts down the fully built bridge set, its theater,
        // and the flight.
        RecordingEngine engine = new();
        engine.Faults.FailOn = nameof(IUiService.OpenStream);

        Assert.Throws<InjectedFault>(() => new SpaceProduct(ProductContexts.For(engine)));

        Assert.Equal(0, engine.CameraView.CameraReleases);
        Assert.Equal(0, engine.Ui.StreamReleases);
        Assert.Equal(engine.Graphics.AppearanceCreates, engine.Graphics.AppearanceReleases);
        Assert.Equal(engine.Graphics.LightCreates, engine.Graphics.LightReleases);
        Assert.Equal(engine.ImplicitSurfaces.MeshCreates, engine.ImplicitSurfaces.MeshReleases);
        Assert.Equal(engine.Graphics.MaterialCreates, engine.Graphics.MaterialReleases);
        Assert.Equal(engine.Audio.VoiceDescriptors.Count, engine.Audio.VoiceReleases);
        Assert.Equal(engine.Audio.OpenedClipPaths.Count, engine.Audio.ClipReleases);
        Assert.Equal(engine.Dynamics.BodyCreates, engine.Dynamics.BodyReleases);
        Assert.Equal(1, engine.Dynamics.WorldReleases);
    }

    [Fact]
    public void AFailedInitialPublicationReleasesTheWholeComposition()
    {
        // The composition is complete and standing when the create-time
        // publication runs, so a failure there unwinds all three owners — not
        // only the ones whose constructors threw.
        RecordingEngine engine = new();
        engine.Faults.FailOn = nameof(IGraphicsService.PublishSnapshot);

        Assert.Throws<InjectedFault>(() => new SpaceProduct(ProductContexts.For(engine)));

        Assert.Equal(2, engine.CameraView.CameraReleases);
        Assert.Equal(1, engine.Ui.StreamReleases);
        Assert.Equal(
            engine.Graphics.AppearanceCreates,
            engine.Graphics.AppearanceReleases);
        Assert.Equal(engine.Graphics.LightCreates, engine.Graphics.LightReleases);
        Assert.Equal(engine.ImplicitSurfaces.MeshCreates, engine.ImplicitSurfaces.MeshReleases);
        Assert.Equal(engine.Graphics.MaterialCreates, engine.Graphics.MaterialReleases);
        Assert.Equal(engine.Audio.VoiceDescriptors.Count, engine.Audio.VoiceReleases);
        Assert.Equal(engine.Audio.OpenedClipPaths.Count, engine.Audio.ClipReleases);
        Assert.Equal(engine.Dynamics.BodyCreates, engine.Dynamics.BodyReleases);
        Assert.Equal(1, engine.Dynamics.WorldReleases);
        Assert.Equal(
            ["camera", "ui", "appearance", "voice", "clip", "light", "appearance", "mesh", "material", "body", "world"],
            engine.Faults.OwnersReleasedInOrder());
    }

    [Fact]
    public void ASetThatFailsItsFirstMeshAppearanceReleasesItsMeshAndMaterials()
    {
        // The set fails while staging its first surface: one mesh resource and
        // the material set are already open, so the set's own unwind puts them
        // down and the composition then releases the flight it was holding.
        RecordingEngine engine = new();
        engine.Faults.FailOn = nameof(IGraphicsService.CreateMeshAppearance);

        Assert.Throws<InjectedFault>(() => new SpaceProduct(ProductContexts.For(engine)));

        Assert.Equal(0, engine.CameraView.CameraReleases);
        Assert.Equal(0, engine.Ui.StreamReleases);
        Assert.Equal(0, engine.Graphics.AppearanceReleases);
        Assert.Equal(0, engine.Graphics.LightReleases);
        Assert.Equal(1, engine.ImplicitSurfaces.MeshReleases);
        Assert.Equal(engine.Graphics.MaterialCreates, engine.Graphics.MaterialReleases);
        Assert.Equal(0, engine.Audio.VoiceReleases);
        Assert.Equal(0, engine.Audio.ClipReleases);
        Assert.Equal(engine.Dynamics.BodyCreates, engine.Dynamics.BodyReleases);
        Assert.Equal(1, engine.Dynamics.WorldReleases);
    }

    [Fact]
    public void ATheaterThatFailsItsFirstVoiceReleasesTheClipsItOpened()
    {
        // The theater fails while staging its voices. It releases the clips it
        // had already opened, and the composition puts down the fully built set
        // and the flight it was holding.
        RecordingEngine engine = new();
        engine.Faults.FailOn = nameof(IAudioService.CreateVoice);

        Assert.Throws<InjectedFault>(() => new SpaceProduct(ProductContexts.For(engine)));

        Assert.Equal(0, engine.CameraView.CameraReleases);
        Assert.Equal(0, engine.Ui.StreamReleases);
        Assert.Equal(engine.Graphics.AppearanceCreates, engine.Graphics.AppearanceReleases);
        Assert.Equal(engine.Graphics.LightCreates, engine.Graphics.LightReleases);
        Assert.Equal(engine.ImplicitSurfaces.MeshCreates, engine.ImplicitSurfaces.MeshReleases);
        Assert.Equal(engine.Graphics.MaterialCreates, engine.Graphics.MaterialReleases);
        Assert.Equal(0, engine.Audio.VoiceReleases);
        Assert.Equal(engine.Audio.OpenedClipPaths.Count, engine.Audio.ClipReleases);
        Assert.Equal(engine.Dynamics.BodyCreates, engine.Dynamics.BodyReleases);
        Assert.Equal(1, engine.Dynamics.WorldReleases);
    }

    [Theory]
    [InlineData(nameof(IDynamicsService.CreateBody), 1)]
    [InlineData(nameof(IDynamicsService.CreateCuboidBody), 2)]
    [InlineData(nameof(IDynamicsService.CreateSphereBodyWithProperties), 2)]
    public void ABodyCreationFailureReleasesEveryEarlierBody(string operation, int occurrence)
    {
        RecordingEngine engine = new();
        engine.Faults.FailOn = operation;
        engine.Faults.FailOnOccurrence = occurrence;
        Assert.Throws<InjectedFault>(() => new SpaceProduct(ProductContexts.For(engine)));
        Assert.Equal(engine.Dynamics.BodyCreates, engine.Dynamics.BodyReleases);
        Assert.Equal(1, engine.Dynamics.WorldReleases);
    }

    [Theory]
    [InlineData("camera")]
    [InlineData("ui")]
    [InlineData("appearance")]
    [InlineData("voice")]
    [InlineData("clip")]
    [InlineData("light")]
    [InlineData("mesh")]
    [InlineData("material")]
    [InlineData("body")]
    [InlineData("world")]
    public void AThrowingReleaseStillReleasesEveryOtherResource(string owner)
    {
        RecordingEngine engine = new();
        SpaceProduct product = new(ProductContexts.For(engine));
        engine.Faults.FailOn = "release-" + owner;
        Assert.Throws<AggregateException>(product.Dispose);
        Assert.Equal(engine.CameraView.CreatedCameras.Count, engine.CameraView.CameraReleases);
        Assert.Equal(1, engine.Ui.StreamReleases);
        Assert.Equal(engine.Graphics.AppearanceCreates, engine.Graphics.AppearanceReleases);
        Assert.Equal(engine.Graphics.LightCreates, engine.Graphics.LightReleases);
        Assert.Equal(engine.Graphics.MaterialCreates, engine.Graphics.MaterialReleases);
        Assert.Equal(engine.ImplicitSurfaces.MeshCreates, engine.ImplicitSurfaces.MeshReleases);
        Assert.Equal(engine.Audio.VoiceDescriptors.Count, engine.Audio.VoiceReleases);
        Assert.Equal(engine.Audio.OpenedClipPaths.Count, engine.Audio.ClipReleases);
        Assert.Equal(engine.Dynamics.BodyCreates, engine.Dynamics.BodyReleases);
        Assert.Equal(1, engine.Dynamics.WorldReleases);
        product.Dispose();
    }

    private static void AssertReleasedDeepestFirst(RecordingEngine engine)
    {
        string[] released = engine.Faults.OwnersReleasedInOrder();

        // The projection publishes about the flight, so it goes down first and
        // the world the flight stands in is the last thing left.
        Assert.True(
            Array.IndexOf(released, "appearance") < Array.IndexOf(released, "body"),
            "the projection that publishes about the flight is put down before it");
        Assert.Equal("world", released[^1]);
    }
}
