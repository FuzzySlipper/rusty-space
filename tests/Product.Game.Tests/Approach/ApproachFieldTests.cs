using Rusty.Engine;
using Rusty.Space.Product.Engine.Tests;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Approach.Tests;

/// <summary>
/// An authored approach space has to arrive in the Engine's world as the shapes
/// it was authored as, and stay where the chart puts it. These pin the handover:
/// which lane each obstacle goes down, what holds it in place, and that the
/// product can still say which rock a contact was against afterwards.
/// </summary>
public class ApproachFieldTests
{
    [Fact]
    public void AnAuthoredChartArrivesInTheWorldOnTheLaneForItsShapes()
    {
        // Angular blocks and round masses are handed to the Engine's shape-typed
        // create lanes, in the order they were authored, so the Engine is the one
        // that decides what happens where two of them meet and the product never
        // keeps a grid or an overlap test of its own.
        RecordingDynamics dynamics = new();
        DynamicsWorld world = dynamics.CreateWorld(default);
        ApproachField chart = new(dynamics, world, SpaceTuning.Defaults.Approach);

        IReadOnlyList<ObstacleDefinition> authored = SpaceTuning.Defaults.Approach.Obstacles;
        Assert.Equal(authored.Count, dynamics.CreatedBlocks.Count + dynamics.CreatedBoulders.Count);
        Assert.Equal(authored.OfType<WreckBlock>().Count(), dynamics.CreatedBlocks.Count);
        Assert.Equal(authored.OfType<Boulder>().Count(), dynamics.CreatedBoulders.Count);

        WreckBlock first = authored.OfType<WreckBlock>().First();
        DynamicsCuboidBodyConfig placed = dynamics.CreatedBlocks[0];
        Assert.Equal(first.Position.X, placed.Transform.Translation.X, 4);
        Assert.Equal(first.Position.Z, placed.Transform.Translation.Z, 4);
        Assert.Equal(first.HalfExtents.X, placed.HalfExtents.X, 4);
        Assert.Equal(first.HalfExtents.Z, placed.HalfExtents.Z, 4);

        Boulder firstBoulder = authored.OfType<Boulder>().First();
        DynamicsSphereBodyPropertiesConfig placedBoulder = dynamics.CreatedBoulders[0];
        Assert.Equal(firstBoulder.Radius, placedBoulder.Radius, 4);
    }

    [Fact]
    public void AnObstacleIsHeldToTheChartRatherThanLeftToDriftAcrossIt()
    {
        // A rock has to be on the chart in the next approach as well as this one.
        // It is held there by its locked translation axes — not by being
        // weightless, which the Engine does not admit — and its faces are left
        // unpowered by a solver that would have to be paid for at speeds this
        // chart is never flown at.
        RecordingDynamics dynamics = new();
        DynamicsWorld world = dynamics.CreateWorld(default);
        ApproachField chart = new(dynamics, world, SpaceTuning.Defaults.Approach);

        Assert.NotEmpty(dynamics.CreatedBlocks);
        foreach (DynamicsBodyProperties held in HeldProperties(dynamics))
        {
            Assert.True(held.AxisLocks.TranslationX);
            Assert.True(held.AxisLocks.TranslationY);
            Assert.True(held.AxisLocks.TranslationZ);
            Assert.True(held.Mass > 0.0f);
            Assert.False(held.ContinuousCollision);
        }
    }

    [Fact]
    public void AnAuthoredBodyIsNamedBackWhenTheEngineSaysWhatItMet()
    {
        // The Engine reports a contact as a pair of bodies. Everything that names
        // the thing a hull struck starts from the identity this owner kept for the
        // body it opened, and a body it did not open is not one of the chart's.
        RecordingDynamics dynamics = new();
        DynamicsWorld world = dynamics.CreateWorld(default);
        ApproachField chart = new(dynamics, world, SpaceTuning.Defaults.Approach);

        ObstacleDefinition second = SpaceTuning.Defaults.Approach.Obstacles[1];
        ObstacleDefinition last = SpaceTuning.Defaults.Approach.Obstacles[^1];

        Assert.Equal(second.Id, chart.ObstacleAt(new DynamicsBodyReference(2UL)));
        Assert.Equal(
            last.Id,
            chart.ObstacleAt(new DynamicsBodyReference((ulong)chart.Obstacles.Count)));
        Assert.Null(chart.ObstacleAt(new DynamicsBodyReference(0UL)));
        Assert.Null(chart.ObstacleAt(new DynamicsBodyReference(900UL)));
    }

    private static IEnumerable<DynamicsBodyProperties> HeldProperties(RecordingDynamics dynamics)
    {
        foreach (DynamicsCuboidBodyConfig block in dynamics.CreatedBlocks)
        {
            yield return block.Properties;
        }

        foreach (DynamicsSphereBodyPropertiesConfig boulder in dynamics.CreatedBoulders)
        {
            yield return boulder.Properties;
        }
    }
}
