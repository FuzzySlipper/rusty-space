using System.Numerics;
using Xunit;

namespace Rusty.Space.Product.Bridge.Tests;

/// <summary>
/// What the parametric layout guarantees before any Engine handle exists:
/// defaults that validate, dimensions that refuse nonsense, and attachment
/// facts the parent theater can aim cameras, lights, and audio from.
/// </summary>
public class BridgeLayoutTests
{
    [Fact]
    public void DefaultsValidateAndKeepTheDisplayInsideItsBezel()
    {
        BridgeLayout layout = BridgeLayout.Defaults.Validate();

        Assert.True(layout.DisplayWidth + (2.0f * layout.BezelMargin) <= layout.ConsoleWidthZ);
        Assert.True(layout.RecessDepth < layout.ConsoleDepthX);
    }

    [Fact]
    public void ADisplayWiderThanItsConsoleFaceIsRefused()
    {
        BridgeLayout layout = BridgeLayout.Defaults with { DisplayWidth = 5.0f };

        Assert.Throws<ArgumentOutOfRangeException>(() => layout.Validate());
    }

    [Fact]
    public void ARecessThatPiercesTheConsoleIsRefused()
    {
        BridgeLayout layout = BridgeLayout.Defaults with { RecessDepth = 5.0f };

        Assert.Throws<ArgumentOutOfRangeException>(() => layout.Validate());
    }

    [Fact]
    public void ARecessThatLeavesNoRoomForTheInstrumentFaceIsRefused()
    {
        BridgeLayout layout = BridgeLayout.Defaults with { RecessDepth = 0.01f };

        Assert.Throws<ArgumentOutOfRangeException>(() => layout.Validate());
    }

    [Fact]
    public void AFullHeightDoorwayLeavesNoLintelAndIsRefused()
    {
        BridgeLayout layout = BridgeLayout.Defaults with { DoorwayHeight = BridgeLayout.Defaults.WallHeight };

        Assert.Throws<ArgumentOutOfRangeException>(() => layout.Validate());
    }

    [Fact]
    public void ASideHousingThatPiercesTheNorthWallIsRefused()
    {
        BridgeLayout layout = BridgeLayout.Defaults with { SideHousingWidthZ = 5.0f };

        Assert.Throws<ArgumentOutOfRangeException>(() => layout.Validate());
    }

    [Fact]
    public void ASideHousingThatOverlapsTheConsoleIsRefused()
    {
        BridgeLayout layout = BridgeLayout.Defaults with { SideHousingGap = 0.0f };

        Assert.Throws<ArgumentOutOfRangeException>(() => layout.Validate());
    }

    [Fact]
    public void AnEyeAboveTheWallsIsRefused()
    {
        BridgeLayout layout = BridgeLayout.Defaults with { EyeHeightAboveFloor = 9.0f };

        Assert.Throws<ArgumentOutOfRangeException>(() => layout.Validate());
    }

    [Fact]
    public void TheSeatedEyeLooksAlongPlusXAndDownAtTheInstrument()
    {
        BridgeLayout layout = BridgeLayout.Defaults.Validate();
        BridgePlacements placements = layout.Placements;

        // The seat faces the helm wall: yaw near +X with a downward pitch
        // toward the recessed display.
        Assert.InRange(placements.SeatedEye.YawDegrees, 80.0, 100.0);
        Assert.InRange(placements.SeatedEye.PitchDegrees, -30.0, -5.0);
        Assert.Equal(layout.ToWorld(new Vector3(layout.SeatCenter.X, layout.EyeHeightAboveFloor, layout.SeatCenter.Z)), placements.SeatedEye.Position);
    }

    [Fact]
    public void TheInstrumentSitsInsideItsRecessFacingTheSeat()
    {
        BridgeLayout layout = BridgeLayout.Defaults.Validate();

        // Structural, not formulaic: the instrument lies between the console
        // face and the recess back, and the display module stops short of the
        // cut walls by the named clearance.
        float faceOffset = layout.InstrumentCenter.X - layout.Anchor.X - layout.ConsoleFaceX;
        Assert.True(faceOffset > 0.0f);
        Assert.True(faceOffset < layout.RecessDepth);
        float backGap = (layout.ConsoleFaceX + layout.RecessDepth) - faceOffset - layout.ConsoleFaceX;
        Assert.Equal(BridgeLayout.InstrumentBackClearance, backGap, 4);
        Assert.Equal(-Vector3.UnitX, BridgeLayout.InstrumentNormal);
        Assert.Equal(layout.DisplayCenterHeight, layout.InstrumentCenter.Y - layout.Anchor.Y, 4);
    }

    [Fact]
    public void TheApproachViewReadsTheHelmThroughTheDoorway()
    {
        BridgeLayout layout = BridgeLayout.Defaults.Validate();
        BridgePlacements placements = layout.Placements;

        Vector3 source = placements.ApproachView.Position - layout.Anchor;
        Vector3 focus = placements.ApproachFocus - layout.Anchor;

        // The camera stands outside the room and the focus sits on the helm
        // face inside the display opening.
        double outside = Math.Sqrt(source.X * source.X + source.Z * source.Z);
        Assert.True(outside > layout.RoomLengthX / 2.0f);
        Assert.Equal(layout.ConsoleFaceX, focus.X, 4);
        Assert.Equal(layout.DisplayCenterHeight, focus.Y, 4);
        Assert.Equal(0.0f, focus.Z, 4);

        // The sightline threads the doorway: where it crosses the south-wall
        // plane it is inside the doorway half-width and below the lintel.
        double wallZ = -(layout.RoomWidthZ / 2.0f);
        double t = (source.Z - wallZ) / (source.Z - focus.Z);
        Assert.InRange(t, 0.0, 1.0);
        double crossX = source.X + ((focus.X - source.X) * t);
        double crossY = source.Y + ((focus.Y - source.Y) * t);
        Assert.True(Math.Abs(crossX) <= layout.DoorwayWidth / 2.0f);
        Assert.True(crossY >= 0.0 && crossY <= layout.DoorwayHeight);

        Assert.InRange(placements.ApproachView.YawDegrees, 130.0, 170.0);
        Assert.InRange(placements.ApproachView.PitchDegrees, -15.0, 0.0);
    }

    [Fact]
    public void ThePropPivotIsTheSlateAnchorInWorld()
    {
        BridgeLayout layout = BridgeLayout.Defaults.Validate();

        Assert.Equal(layout.ToWorld(layout.PropAnchor), layout.Placements.PropPivot);
        Assert.Equal(layout.ToWorld(layout.OverheadLightLocal), layout.Placements.OverheadLight);
        Assert.Equal(layout.ToWorld(layout.AudioAnchorLocal), layout.Placements.AudioAnchor);
    }

    [Fact]
    public void YawAndPitchNameTheDirectionFromSourceToTarget()
    {
        (double yaw, double pitch) = BridgeLayout.YawPitchFor(Vector3.Zero, Vector3.UnitX);

        Assert.Equal(90.0, yaw, 9);
        Assert.Equal(0.0, pitch, 9);
    }

    [Fact]
    public void AZeroSightlineIsNeutralRatherThanUndefined()
    {
        (double yaw, double pitch) = BridgeLayout.YawPitchFor(Vector3.One, Vector3.One);

        Assert.Equal(0.0, yaw);
        Assert.Equal(0.0, pitch);
    }
}
