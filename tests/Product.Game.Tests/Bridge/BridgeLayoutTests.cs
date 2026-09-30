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
        Assert.Equal(layout.Instruments.BackClearance, backGap, 4);
        Assert.Equal(-Vector3.UnitX, BridgeLayout.InstrumentNormal);
        Assert.Equal(layout.DisplayCenterHeight, layout.InstrumentCenter.Y - layout.Anchor.Y, 4);
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
        (double yaw, double pitch) = Rusty.Space.Product.Navigation.CameraOrientation.LookAt(Vector3.Zero, Vector3.UnitX);

        Assert.Equal(90.0, yaw, 9);
        Assert.Equal(0.0, pitch, 9);
    }

    [Fact]
    public void AZeroSightlineIsNeutralRatherThanUndefined()
    {
        (double yaw, double pitch) = Rusty.Space.Product.Navigation.CameraOrientation.LookAt(Vector3.One, Vector3.One);

        Assert.Equal(0.0, yaw);
        Assert.Equal(0.0, pitch);
    }
}
