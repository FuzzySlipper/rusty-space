using Rusty.Engine;
using Rusty.Space.Product.Engine.Tests;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Tests;

/// <summary>
/// What happens to what Space opened when a create dies partway through. The
/// composition builds flight, then the bridge set, then the projection, then
/// the camera, and the product publishes once the whole thing stands; each of
/// those stages can fail, and the owners a failed construction had already
/// opened cannot be left for a product nobody can reach. Handles whose own
/// constructor never returned are a different matter: they belong to the
/// create call, which the Engine discards whole, and these tests record that
/// Space does not pretend to release them.
/// </summary>
public class SpaceProductCompositionTests
{
    // Every appearance handle the navigation projection opens at construction:
    // the hull, the planet, the wake, both band cores, both band authority
    // regions, the declined band, the velocity reading, the projected path, the
    // flow lattice, the tuning-only push vectors, the centers of force, the
    // star field, the chart's blocks and boulders, and the mark a contact leaves
    // on the hull. A teardown that leaves any of them open is a leak this count
    // catches.
    private const int ProjectionHandleCount = 17;

    // Every appearance handle the bridge set opens at construction: one per
    // extracted implicit surface plus the separately addressable screens,
    // lamps, and prop slate. Its lights, mesh resources, and materials are
    // counted apart.
    private const int BridgeAppearanceHandleCount = 16;
    private const int BridgeLightCount = 2;
    private const int BridgeMeshCount = 7;
    private const int BridgeMaterialCount = 6;

    // Every body the flight opens in the Engine: the hull, and one for each piece
    // of authored approach geometry the chart stands on.
    private static int OpenedBodies => 1 + SpaceTuning.Defaults.Approach.Obstacles.Count;

    [Fact]
    public void AComposedProductPutsDownEveryOwnerItOpened()
    {
        RecordingEngine engine = new();

        SpaceProduct product = new(ProductContexts.For(engine));
        product.Dispose();

        Assert.Equal(1, engine.Graphics.SnapshotPublications);
        Assert.Equal(ProjectionHandleCount + BridgeAppearanceHandleCount, engine.Graphics.AppearanceReleases);
        Assert.Equal(BridgeLightCount, engine.Graphics.LightReleases);
        Assert.Equal(BridgeMeshCount, engine.ImplicitSurfaces.MeshReleases);
        Assert.Equal(BridgeMaterialCount, engine.Graphics.MaterialReleases);
        Assert.Equal(
            ["camera", "ui", "appearance", "light", "appearance", "mesh", "material", "body", "world"],
            engine.Faults.OwnersReleasedInOrder());
    }

    [Fact]
    public void ACameraThatFailsToActivateReleasesTheProjectionAndFlight()
    {
        // Camera creation precedes camera activation, so a failure at the second
        // step means the product was holding a bridge set, a projection, and a
        // flight and had only just failed to acquire a view.
        RecordingEngine engine = new();
        engine.Faults.FailOn = nameof(ICameraViewService.SetActiveCamera);

        Assert.Throws<InjectedFault>(() => new SpaceProduct(ProductContexts.For(engine)));

        Assert.Equal(0, engine.CameraView.CameraReleases);
        Assert.Equal(1, engine.Ui.StreamReleases);
        Assert.Equal(
            ProjectionHandleCount + BridgeAppearanceHandleCount,
            engine.Graphics.AppearanceReleases);
        Assert.Equal(BridgeLightCount, engine.Graphics.LightReleases);
        Assert.Equal(BridgeMeshCount, engine.ImplicitSurfaces.MeshReleases);
        Assert.Equal(BridgeMaterialCount, engine.Graphics.MaterialReleases);
        Assert.Equal(OpenedBodies, engine.Dynamics.BodyReleases);
        Assert.Equal(1, engine.Dynamics.WorldReleases);
        AssertReleasedDeepestFirst(engine);
    }

    [Fact]
    public void AProjectionThatFailsToOpenItsStreamReleasesTheSetAndFlight()
    {
        // The projection's constructor opens its whole set of appearances and
        // then fails on its stream, so the projection was never a held owner:
        // the fully built bridge set and the flight are what the composition
        // has to put down. The projection's own partial appearances belong to
        // the failed create call, which the Engine discards whole.
        RecordingEngine engine = new();
        engine.Faults.FailOn = nameof(IUiService.OpenStream);

        Assert.Throws<InjectedFault>(() => new SpaceProduct(ProductContexts.For(engine)));

        Assert.Equal(0, engine.CameraView.CameraReleases);
        Assert.Equal(0, engine.Ui.StreamReleases);
        Assert.Equal(BridgeAppearanceHandleCount, engine.Graphics.AppearanceReleases);
        Assert.Equal(BridgeLightCount, engine.Graphics.LightReleases);
        Assert.Equal(BridgeMeshCount, engine.ImplicitSurfaces.MeshReleases);
        Assert.Equal(BridgeMaterialCount, engine.Graphics.MaterialReleases);
        Assert.Equal(OpenedBodies, engine.Dynamics.BodyReleases);
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

        Assert.Equal(1, engine.CameraView.CameraReleases);
        Assert.Equal(1, engine.Ui.StreamReleases);
        Assert.Equal(
            ProjectionHandleCount + BridgeAppearanceHandleCount,
            engine.Graphics.AppearanceReleases);
        Assert.Equal(BridgeLightCount, engine.Graphics.LightReleases);
        Assert.Equal(BridgeMeshCount, engine.ImplicitSurfaces.MeshReleases);
        Assert.Equal(BridgeMaterialCount, engine.Graphics.MaterialReleases);
        Assert.Equal(OpenedBodies, engine.Dynamics.BodyReleases);
        Assert.Equal(1, engine.Dynamics.WorldReleases);
        Assert.Equal(
            ["camera", "ui", "appearance", "light", "appearance", "mesh", "material", "body", "world"],
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
        Assert.Equal(BridgeMaterialCount, engine.Graphics.MaterialReleases);
        Assert.Equal(OpenedBodies, engine.Dynamics.BodyReleases);
        Assert.Equal(1, engine.Dynamics.WorldReleases);
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
