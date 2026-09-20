using Rusty.Engine;
using Rusty.Space.Product.Engine.Tests;
using Xunit;

namespace Rusty.Space.Product.Tests;

/// <summary>
/// What happens to what Space opened when a create dies partway through. The
/// composition builds flight, then the projection, then the camera, and the
/// product publishes once the whole thing stands; each of those stages can fail,
/// and the owners a failed construction had already opened cannot be left for a
/// product nobody can reach. Handles whose own constructor never returned are a
/// different matter: they belong to the create call, which the Engine discards
/// whole, and these tests record that Space does not pretend to release them.
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
        Assert.Equal(["camera", "ui", "appearance", "body", "world"], engine.Faults.OwnersReleasedInOrder());
    }

    [Fact]
    public void ACameraThatFailsToActivateReleasesTheProjectionAndFlight()
    {
        // Camera creation precedes camera activation, so a failure at the second
        // step means the product was holding a projection and a flight and had
        // only just failed to acquire a view.
        RecordingEngine engine = new();
        engine.Faults.FailOn = nameof(ICameraViewService.SetActiveCamera);

        Assert.Throws<InjectedFault>(() => new SpaceProduct(ProductContexts.For(engine)));

        Assert.Equal(0, engine.CameraView.CameraReleases);
        Assert.Equal(1, engine.Ui.StreamReleases);
        Assert.Equal(6, engine.Graphics.AppearanceReleases);
        Assert.Equal(1, engine.Dynamics.BodyReleases);
        Assert.Equal(1, engine.Dynamics.WorldReleases);
        AssertReleasedDeepestFirst(engine);
    }

    [Fact]
    public void AProjectionThatFailsToOpenItsStreamReleasesTheFlight()
    {
        // The projection's constructor opens six appearances and then fails on
        // its stream, so the projection was never a held owner: the flight is
        // what the composition has to put down.
        RecordingEngine engine = new();
        engine.Faults.FailOn = nameof(IUiService.OpenStream);

        Assert.Throws<InjectedFault>(() => new SpaceProduct(ProductContexts.For(engine)));

        Assert.Equal(0, engine.CameraView.CameraReleases);
        Assert.Equal(0, engine.Ui.StreamReleases);
        Assert.Equal(0, engine.Graphics.AppearanceReleases);
        Assert.Equal(1, engine.Dynamics.BodyReleases);
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
        Assert.Equal(6, engine.Graphics.AppearanceReleases);
        Assert.Equal(1, engine.Dynamics.BodyReleases);
        Assert.Equal(1, engine.Dynamics.WorldReleases);
        Assert.Equal(["camera", "ui", "appearance", "body", "world"], engine.Faults.OwnersReleasedInOrder());
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
