using System.Text;
using Rusty.Engine;
using Rusty.Space.Product.Engine.Tests;
using Rusty.Space.Product.Presentation;
using Xunit;

namespace Rusty.Space.Product.Tests;

public class SpaceProductResetTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MappedResetAndRestartPublishTheSameFlightAndBridgeState(bool seated)
    {
        RecordingEngine mapped = new();
        RecordingEngine restarted = new();
        using SpaceProduct mappedProduct = new(ProductContexts.For(mapped));
        using SpaceProduct restartedProduct = new(ProductContexts.For(restarted));
        mappedProduct.Start();
        restartedProduct.Start();
        for (int turn = 0; turn < 60; turn++)
        {
            ProductUpdate input = Turn(turn == 0 && seated
                ? [Digital("space.flight.thrust"), Digital("space.bridge.sit")]
                : [Digital("space.flight.thrust")]);
            mappedProduct.Update(input);
            restartedProduct.Update(input);
        }

        mappedProduct.Update(Turn([Digital("space.flight.reset")]));
        restartedProduct.Restart();

        AssertSamePresentation(mapped, restarted);
        AssertSameAudio(mapped, restarted);
        Assert.Equal(mapped.Graphics.LightLevels, restarted.Graphics.LightLevels);
        Assert.Equal(mapped.CameraView.CameraPoses, restarted.CameraView.CameraPoses);
        mappedProduct.Update(Turn([]));
        restartedProduct.Update(Turn([]));
        AssertSamePresentation(mapped, restarted);
        AssertSameAudio(mapped, restarted);
    }

    [Fact]
    public void CoupledBandsUseTheSameAppearanceAtCreateStartAndRestartAsTheFirstTurn()
    {
        RecordingEngine engine = new();
        using SpaceProduct product = new(ProductContexts.For(engine));
        ulong atCreate = GentleBandAppearance(engine);
        product.Start();
        Assert.Equal(atCreate, GentleBandAppearance(engine));
        product.Update(Turn([]));
        Assert.Equal(atCreate, GentleBandAppearance(engine));
        product.Restart();
        Assert.Equal(atCreate, GentleBandAppearance(engine));
    }

    private static void AssertSamePresentation(RecordingEngine expected, RecordingEngine actual)
    {
        Assert.Equal(expected.Graphics.LastSnapshot.Length, actual.Graphics.LastSnapshot.Length);
        for (int index = 0; index < expected.Graphics.LastSnapshot.Length; index++)
        {
            AppearanceFact left = expected.Graphics.LastSnapshot[index];
            AppearanceFact right = actual.Graphics.LastSnapshot[index];
            Assert.Equal(left.Appearance.Handle, right.Appearance.Handle);
            Assert.Equal(left with { Appearance = right.Appearance }, right);
        }
    }

    private static void AssertSameAudio(RecordingEngine expected, RecordingEngine actual)
    {
        foreach ((ulong voice, AudioSourceDescriptor left) in expected.Audio.VoiceDescriptors)
        {
            AudioSourceDescriptor right = actual.Audio.VoiceDescriptors[voice];
            Assert.Equal(left.Clip.Handle, right.Clip.Handle);
            Assert.Equal(left with { Clip = right.Clip }, right);
        }
    }

    private static ulong GentleBandAppearance(RecordingEngine engine) => engine.Graphics.LastSnapshot
        .Single(fact => fact.ObjectId == (ulong)SpaceAppearanceObject.GentleCurrent).Appearance.Handle.Value;

    private static ProductInputEvent Digital(string intent) => new()
    {
        Kind = InputEventKind.MappedDigital,
        Phase = InputPhase.Pressed,
        X = 1,
        Intent = Encoding.UTF8.GetBytes(intent),
    };

    private static ProductUpdate Turn(ProductInputEvent[] input) => new(
        new ProductUpdateFacts(ProductUpdateMode.Realtime, ProductLifecycleState.Running,
            Generation: 1, ControlRevision: 0, ObservedHostTimeNanoseconds: 0, SimulationStep: 0,
            FixedStepHz: 60, AdmittedStepCount: 1, DroppedStepCount: 0, FixedDeltaSeconds: 1.0 / 60.0),
        input);
}
