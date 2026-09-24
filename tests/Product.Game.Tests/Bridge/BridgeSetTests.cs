using Rusty.Engine;
using Rusty.Space.Product.Engine.Tests;
using Xunit;

namespace Rusty.Space.Product.Bridge.Tests;

/// <summary>
/// What one bridge set stages on recording doubles: seven one-shot implicit
/// extractions with recipe names, nine separately addressable presentation
/// objects with stable identities, a teardown that puts every staged handle
/// back down, and a create-failure unwind that releases what the failed build
/// had opened. Geometry correctness itself is proven against the live host,
/// not against a double's arithmetic.
/// </summary>
public class BridgeSetTests
{
    private static readonly string[] ExpectedSurfaces =
    [
        "bridge floor",
        "bridge walls",
        "bridge overhead",
        "helm console",
        "side instrument housing",
        "bridge seat",
        "engineering corner",
    ];

    private const int PresentationPartCount = 9;

    [Fact]
    public void ConstructionExtractsEachSurfaceExactlyOnce()
    {
        RecordingEngine engine = new();
        using BridgeSet bridge = new(engine.Graphics, engine.ImplicitSurfaces, BridgeLayout.Defaults);

        Assert.Equal(ExpectedSurfaces.Length, engine.ImplicitSurfaces.GenerationCount);
        Assert.Equal(ExpectedSurfaces.Length, bridge.ImplicitSurfaceCount);
        Assert.Equal(ExpectedSurfaces, bridge.PartNames.Take(ExpectedSurfaces.Length));
    }

    [Fact]
    public void FactsCarryStableIdentitiesOnTheSceneLayer()
    {
        RecordingEngine engine = new();
        using BridgeSet bridge = new(engine.Graphics, engine.ImplicitSurfaces, BridgeLayout.Defaults);

        AppearanceFact[] facts = bridge.Facts.ToArray();
        Assert.Equal(ExpectedSurfaces.Length + PresentationPartCount, facts.Length);
        for (int index = 0; index < facts.Length; index++)
        {
            Assert.Equal(BridgeSet.FirstBridgeObjectId + (ulong)index, facts[index].ObjectId);
            Assert.True(facts[index].Visible);
            Assert.Equal(RenderLayer.Scene, facts[index].Layer);
        }
    }

    [Fact]
    public void TheMainDisplaySitsInsideTheHelmRecess()
    {
        RecordingEngine engine = new();
        using BridgeSet bridge = new(engine.Graphics, engine.ImplicitSurfaces, BridgeLayout.Defaults);

        AppearanceFact display = bridge.Facts.ToArray()
            .Single(fact => fact.ObjectId == BridgeSet.FirstBridgeObjectId + (ulong)BridgePart.MainDisplay);

        Assert.Equal(bridge.Layout.InstrumentCenter, display.Transform.Translation);
        Assert.Equal(
            new System.Numerics.Vector3(0.03f, bridge.Layout.DisplayHeight, bridge.Layout.DisplayWidth),
            display.Transform.Scale);
    }

    [Fact]
    public void ThePropSlateSitsOnItsPivotWithItsOwnIdentity()
    {
        RecordingEngine engine = new();
        using BridgeSet bridge = new(engine.Graphics, engine.ImplicitSurfaces, BridgeLayout.Defaults);

        // The slate is a plain module, not an extraction: the parent theater
        // re-poses it by republishing this fact's transform about the pivot.
        AppearanceFact prop = bridge.Facts.ToArray()
            .Single(fact => fact.ObjectId == BridgeSet.FirstBridgeObjectId + (ulong)BridgePart.PropSlate);

        Assert.Equal(bridge.Placements.PropPivot, prop.Transform.Translation);
        Assert.Equal(bridge.Layout.PropSize, prop.Transform.Scale);
    }

    [Fact]
    public void DisposalPutsDownEveryStagedHandle()
    {
        RecordingEngine engine = new();
        BridgeSet bridge = new(engine.Graphics, engine.ImplicitSurfaces, BridgeLayout.Defaults);
        bridge.Dispose();

        Assert.Equal(
            ExpectedSurfaces.Length + PresentationPartCount,
            engine.Graphics.AppearanceReleases);
        Assert.Equal(2, engine.Graphics.LightReleases);
        Assert.Equal(ExpectedSurfaces.Length, engine.ImplicitSurfaces.MeshReleases);
        Assert.Equal(6, engine.Graphics.MaterialReleases);
    }

    [Fact]
    public void AFailingExtractionReleasesTheMaterialsAndNothingElse()
    {
        RecordingEngine engine = new();
        engine.Faults.FailOn = nameof(IImplicitSurfacesService.Generate);

        Assert.Throws<InjectedFault>(
            () => new BridgeSet(engine.Graphics, engine.ImplicitSurfaces, BridgeLayout.Defaults));

        Assert.Equal(6, engine.Graphics.MaterialReleases);
        Assert.Equal(0, engine.ImplicitSurfaces.MeshReleases);
        Assert.Equal(0, engine.Graphics.AppearanceReleases);
        Assert.Equal(0, engine.Graphics.LightReleases);
    }

    [Fact]
    public void AFailingPresentationPartReleasesTheMeshesAndMaterials()
    {
        RecordingEngine engine = new();
        engine.Faults.FailOn = nameof(IGraphicsService.CreatePrimitive);

        Assert.Throws<InjectedFault>(
            () => new BridgeSet(engine.Graphics, engine.ImplicitSurfaces, BridgeLayout.Defaults));

        Assert.Equal(ExpectedSurfaces.Length, engine.Graphics.AppearanceReleases);
        Assert.Equal(ExpectedSurfaces.Length, engine.ImplicitSurfaces.MeshReleases);
        Assert.Equal(6, engine.Graphics.MaterialReleases);
        Assert.Equal(0, engine.Graphics.LightReleases);
    }
}
