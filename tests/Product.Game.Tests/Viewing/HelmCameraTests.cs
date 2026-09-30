using System.Numerics;
using System.Text;
using Rusty.Engine;
using Rusty.Space.Product.Bridge;
using Rusty.Space.Product.Composition;
using Rusty.Space.Product.Engine.Tests;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Viewing.Tests;

/// <summary>
/// What the helm camera stages around its Engine handle: it opens on the
/// seated eye from the bridge placements, and each follow republishes that
/// pose plus the theater's filtered lean. Switching between this camera and
/// the chart camera is the sit-at-helm toggle; neither camera writes flight
/// state.
/// </summary>
public class HelmCameraTests
{
    [Fact]
    public void TheHelmCameraOpensOnTheSeatedEye()
    {
        RecordingEngine engine = new();
        BridgeLayout layout = BridgeLayout.Defaults.Validate();
        using HelmCamera helm = new(engine.CameraView.Service, layout, new HelmCameraTuning(70, 0.2, 350));

        Assert.Single(engine.CameraView.CreatedCameras);
        helm.Follow(HelmLean.Rest);

        CameraDescriptor pose = engine.CameraView.CameraPoses[engine.CameraView.CreatedCameras[0]];
        Assert.Equal(layout.Placements.SeatedEye.Position, pose.Pose.Position);
        Assert.Equal(70, pose.Projection.FovYDegrees);
        Assert.Equal(0.2, pose.Projection.Near);
        Assert.Equal(350, pose.Projection.Far);
        Assert.Equal(layout.Placements.SeatedEye.YawDegrees, pose.Pose.YawDegrees, 9);
        Assert.Equal(layout.Placements.SeatedEye.PitchDegrees, pose.Pose.PitchDegrees, 9);
    }

    [Fact]
    public void FollowPublishesTheLeanedPose()
    {
        RecordingEngine engine = new();
        BridgeLayout layout = BridgeLayout.Defaults.Validate();
        using HelmCamera helm = new(engine.CameraView.Service, layout, SpaceTuning.Defaults.HelmCamera);
        var lean = new HelmLean(new Vector3(-0.05f, 0.0f, 0.02f), 1.5);

        helm.Follow(lean);

        CameraDescriptor pose = engine.CameraView.CameraPoses[engine.CameraView.CreatedCameras[0]];
        Assert.Equal(layout.Placements.SeatedEye.Position + lean.PositionOffset, pose.Pose.Position);
        Assert.Equal(layout.Placements.SeatedEye.YawDegrees + lean.YawOffsetDeg, pose.Pose.YawDegrees, 9);
    }

    [Fact]
    public void SittingActivatesTheHelmCameraAndStandingReturnsToTheChart()
    {
        RecordingEngine engine = new();
        using SpaceProductComposition composition = new(ProductContexts.For(engine));
        ulong chart = engine.CameraView.CreatedCameras[0];
        ulong helm = engine.CameraView.CreatedCameras[1];

        Assert.Single(engine.CameraView.ActiveCameras);
        Assert.Equal(chart, engine.CameraView.ActiveCameras[0]);

        composition.SetSeated(true);
        composition.SetSeated(false);

        Assert.Equal(3, engine.CameraView.ActiveCameras.Count);
        Assert.Equal(helm, engine.CameraView.ActiveCameras[1]);
        Assert.Equal(chart, engine.CameraView.ActiveCameras[2]);
        Assert.False(composition.Seated);
    }

    [Fact]
    public void ASitPressThroughAnUpdateSitsAndAnotherPressStands()
    {
        RecordingEngine engine = new();
        using SpaceProduct product = new(ProductContexts.For(engine));
        product.Start();
        ulong chart = engine.CameraView.CreatedCameras[0];
        ulong helm = engine.CameraView.CreatedCameras[1];

        product.Update(SitUpdate());
        Assert.Equal(helm, engine.CameraView.ActiveCameras[^1]);

        product.Update(SitUpdate());
        Assert.Equal(chart, engine.CameraView.ActiveCameras[^1]);
    }

    [Fact]
    public void StandingPublishesNoHelmPoseUpdates()
    {
        RecordingEngine engine = new();
        using SpaceProduct product = new(ProductContexts.For(engine));
        product.Start();
        ulong helm = engine.CameraView.CreatedCameras[1];

        product.Update(QuietUpdate());

        Assert.Single(engine.CameraView.ActiveCameras);
        Assert.False(engine.CameraView.CameraPoses.ContainsKey(helm));
    }

    private static ProductUpdate QuietUpdate()
    {
        ProductInputEvent[] input = [];
        return new ProductUpdate(
            new ProductUpdateFacts(
                ProductUpdateMode.Realtime,
                ProductLifecycleState.Running,
                Generation: 1UL,
                ControlRevision: 0UL,
                ObservedHostTimeNanoseconds: 0UL,
                SimulationStep: 0UL,
                FixedStepHz: 60U,
                AdmittedStepCount: 1U,
                DroppedStepCount: 0UL,
                FixedDeltaSeconds: 1.0 / 60.0),
            input);
    }

    private static ProductUpdate SitUpdate()
    {
        ProductInputEvent[] input =
        [
            new ProductInputEvent
            {
                Kind = InputEventKind.MappedDigital,
                Phase = InputPhase.Pressed,
                X = 1.0f,
                Intent = Encoding.UTF8.GetBytes("space.bridge.sit"),
            },
        ];
        return new ProductUpdate(
            new ProductUpdateFacts(
                ProductUpdateMode.Realtime,
                ProductLifecycleState.Running,
                Generation: 1UL,
                ControlRevision: 0UL,
                ObservedHostTimeNanoseconds: 0UL,
                SimulationStep: 0UL,
                FixedStepHz: 60U,
                AdmittedStepCount: 1U,
                DroppedStepCount: 0UL,
                FixedDeltaSeconds: 1.0 / 60.0),
            input);
    }
}
