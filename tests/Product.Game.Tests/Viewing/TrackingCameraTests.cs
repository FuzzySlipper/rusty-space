using Rusty.Space.Product.Viewing;
using Xunit;

namespace Rusty.Space.Product.Viewing.Tests;

/// <summary>
/// The chase view closes its gap over admitted simulated time, never over a
/// count multiplied by a product rate. These pin the follow law SpaceFlight
/// feeds it: no time, no move; the same time closes the same gap however the
/// Engine admitted it; and a long stall stops at the cap instead of
/// teleporting the view.
/// </summary>
public class TrackingCameraTests
{
    private static readonly TimeSpan FixedStep = TimeSpan.FromSeconds(1.0 / 60.0);
    private static readonly TimeSpan Smoothing = TimeSpan.FromSeconds(0.35);
    private static readonly TimeSpan FollowCap = TimeSpan.FromSeconds(0.25);
    private const double Tolerance = 1e-12;

    [Fact]
    public void ATurnThatSimulatedNothingLeavesTheViewWhereItWas()
    {
        Assert.Equal(0.0, TrackingCamera.FollowFraction(TimeSpan.Zero, Smoothing), Tolerance);
    }

    [Fact]
    public void TheSameSimulatedTimeClosesTheSameGapHoweverItWasAdmitted()
    {
        // Four one-step turns and one turn admitted four steps move the chase
        // position identically: the admitted count is not a second clock.
        double oneStep = TrackingCamera.FollowFraction(FixedStep, Smoothing);
        double acrossFourTurns = 1.0 - Math.Pow(1.0 - oneStep, 4.0);
        double oneTurnOfFourSteps = TrackingCamera.FollowFraction(
            TimeSpan.FromTicks(4 * FixedStep.Ticks),
            Smoothing);

        Assert.Equal(acrossFourTurns, oneTurnOfFourSteps, 9);
    }

    [Fact]
    public void ALongerWindowClosesMoreOfTheGapButNeverAllOfIt()
    {
        double shortWindow = TrackingCamera.FollowFraction(FixedStep, Smoothing);
        double longWindow = TrackingCamera.FollowFraction(TimeSpan.FromSeconds(0.1), Smoothing);

        Assert.True(shortWindow < longWindow);
        Assert.True(longWindow < 1.0);
    }

    [Fact]
    public void AStallPastTheCapStopsAtTheCapRatherThanTeleportingTheView()
    {
        double atCap = TrackingCamera.FollowFraction(FollowCap, Smoothing);
        double pastCap = TrackingCamera.FollowFraction(TimeSpan.FromSeconds(10.0), Smoothing);

        Assert.Equal(atCap, pastCap, Tolerance);
    }
}
