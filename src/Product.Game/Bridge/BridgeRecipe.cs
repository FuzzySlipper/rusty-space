using System;
using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Implicit;

namespace Rusty.Space.Product.Bridge;

/// <summary>
/// Product-owned parametric authoring for the bridge set. Dimensions come
/// from <see cref="BridgeLayout"/>; solids are composed with Engine implicit
/// primitives, unions, and differences and handed to the owner's emit
/// callback for one-shot extraction.
/// </summary>
/// <remarks>
/// <para>
/// The construction pattern follows the LoadingBay donor: one scoped
/// <see cref="ImplicitRecipe"/> field per surface, bounded extraction
/// regions with per-piece sampling matched to feature size, and synchronous
/// extraction while the field is alive. Room shell, helm recesses, seat, and
/// cabinets are separate surfaces so floor, wall, ceiling, console, fabric,
/// and cabinet materials stay distinct and no piece is sampled at another
/// piece's resolution. The room shell is composed explicitly rather than
/// through <see cref="RoomRecipes.Shell"/> because the doorway gap, the
/// per-surface sampling, and the separate floor/wall/ceiling materials need
/// direct control; the Engine still owns every CSG, meshing, and lifetime
/// operation used here.
/// </para>
/// <para>
/// Screens, lamps, and the prop's presentation module are not implicit
/// surfaces: <see cref="BridgeSet"/> stages those as separately addressable
/// primitive appearances so one combined room mesh never blocks independent
/// screen, lamp, or prop updates.
/// </para>
/// </remarks>
internal static class BridgeRecipe
{
    internal static readonly Transform Identity = new(Vector3.Zero, Quaternion.Identity, Vector3.One);

    internal static void Compose(
        IImplicitSurfacesService service,
        BridgeLayout layout,
        BridgeMaterials materials,
        Action<RecipeSurface> emit)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(emit);

        RecipeWriter writer = new(
            service,
            layout.Recipe.Sampling,
            emit);

        // All recipe math runs in the room-local frame; world placement is the
        // anchor added on the way to the Engine.
        Vector3 W(float x, float y, float z) => layout.ToWorld(new Vector3(x, y, z));

        void Surface(string name, ImplicitRecipe field, ImplicitNode solid, Vector3 localMin, Vector3 localMax, Material material, float cellSize)
        {
            Vector3 min = W(localMin.X, localMin.Y, localMin.Z) - new Vector3(layout.Recipe.ExtractionMargin);
            Vector3 max = W(localMax.X, localMax.Y, localMax.Z) + new Vector3(layout.Recipe.ExtractionMargin);
            writer.Surface(name, field, solid, min, max, material, Identity, [], cellSize, null);
        }

        float halfL = layout.RoomLengthX / 2.0f;
        float halfW = layout.RoomWidthZ / 2.0f;
        float wallT = layout.WallThickness;

        using (ImplicitRecipe f = writer.Begin())
        {
            ImplicitNode floor = f.Box(
                W(-halfL - wallT, -layout.FloorThickness, -halfW - wallT),
                W(halfL + wallT, 0.0f, halfW + wallT));
            Surface(
                "bridge floor",
                f,
                floor,
                new Vector3(-halfL - wallT, -layout.FloorThickness, -halfW - wallT),
                new Vector3(halfL + wallT, 0.0f, halfW + wallT),
                materials.Floor,
                layout.Recipe.FloorCellSize);
        }

        using (ImplicitRecipe f = writer.Begin())
        {
            float height = layout.WallHeight;
            float halfDoor = layout.DoorwayWidth / 2.0f;
            ImplicitNode north = f.Box(
                W(-halfL - wallT, 0.0f, halfW),
                W(halfL + wallT, height, halfW + wallT));
            ImplicitNode southA = f.Box(
                W(-halfL - wallT, 0.0f, -halfW - wallT),
                W(-halfDoor, height, -halfW));
            ImplicitNode southB = f.Box(
                W(halfDoor, 0.0f, -halfW - wallT),
                W(halfL + wallT, height, -halfW));
            ImplicitNode lintel = f.Box(
                W(-halfDoor, layout.DoorwayHeight, -halfW - wallT),
                W(halfDoor, height, -halfW));
            ImplicitNode east = f.Box(
                W(halfL, 0.0f, -halfW - wallT),
                W(halfL + wallT, height, halfW + wallT));
            ImplicitNode west = f.Box(
                W(-halfL - wallT, 0.0f, -halfW - wallT),
                W(-halfL, height, halfW + wallT));
            ImplicitNode walls = f.Union(f.Union(north, f.Union(southA, southB)), f.Union(lintel, f.Union(east, west)));
            Surface(
                "bridge walls",
                f,
                walls,
                new Vector3(-halfL - wallT, 0.0f, -halfW - wallT),
                new Vector3(halfL + wallT, height, halfW + wallT),
                materials.Walls,
                layout.Recipe.WallCellSize);
        }

        using (ImplicitRecipe f = writer.Begin())
        {
            float slabBase = layout.WallHeight;
            float slabTop = slabBase + layout.CeilingThickness;
            ImplicitNode slab = f.Box(
                W(-halfL - wallT, slabBase, -halfW - wallT),
                W(halfL + wallT, slabTop, halfW + wallT));
            foreach (float beamX in new[] { -layout.Recipe.BeamOffsetX, layout.Recipe.BeamOffsetX })
            {
                ImplicitNode beam = f.Box(
                    W(beamX - layout.Recipe.BeamHalfWidth, slabBase - layout.Recipe.BeamDrop, -halfW),
                    W(beamX + layout.Recipe.BeamHalfWidth, slabBase, halfW));
                slab = f.Union(slab, beam);
            }

            Surface(
                "bridge overhead",
                f,
                slab,
                new Vector3(-halfL - wallT, slabBase - layout.Recipe.BeamDrop, -halfW - wallT),
                new Vector3(halfL + wallT, slabTop, halfW + wallT),
                materials.Ceiling,
                layout.Recipe.OverheadCellSize);
        }

        ComposeHelm(writer, layout, materials, W, Surface);
        ComposeSideHousing(writer, layout, materials, W, Surface);
        ComposeSeat(writer, layout, materials, W, Surface);
        ComposeEngineering(writer, layout, materials, W, Surface);
    }

    private static void ComposeHelm(
        RecipeWriter writer,
        BridgeLayout layout,
        BridgeMaterials materials,
        Func<float, float, float, Vector3> world,
        Action<string, ImplicitRecipe, ImplicitNode, Vector3, Vector3, Material, float> surface)
    {
        float faceX = layout.ConsoleFaceX;
        float eastX = faceX + layout.ConsoleDepthX;
        float halfConsole = layout.ConsoleWidthZ / 2.0f;

        using ImplicitRecipe f = writer.Begin();
        ImplicitNode body = f.Box(
            world(faceX, 0.0f, -halfConsole),
            world(eastX, layout.ConsoleHeight, halfConsole));

        // Control strip: a protruding island on the face below the display.
        float stripHalf = layout.ControlStripHeight / 2.0f;
        ImplicitNode strip = f.Box(
            world(faceX - layout.ControlStripProtrusion, layout.ControlStripCenterHeight - stripHalf, -halfConsole + layout.Instruments.ControlStripInset),
            world(faceX + layout.Recipe.StripFaceOverlap, layout.ControlStripCenterHeight + stripHalf, halfConsole - layout.Instruments.ControlStripInset));
        body = f.Union(body, strip);

        // Main display recess: cut from outside the face so a thick bezel rim
        // of console body survives around the opening.
        float displayHalfW = layout.DisplayWidth / 2.0f;
        float displayHalfH = layout.DisplayHeight / 2.0f;
        ImplicitNode recess = f.Box(
            world(faceX - layout.Recipe.CutOvershoot, layout.DisplayCenterHeight - displayHalfH, -displayHalfW),
            world(faceX + layout.RecessDepth, layout.DisplayCenterHeight + displayHalfH, displayHalfW));
        body = f.Subtract(body, recess);

        surface(
            "helm console",
            f,
            body,
            new Vector3(faceX - layout.ControlStripProtrusion, 0.0f, -halfConsole),
            new Vector3(eastX, layout.ConsoleHeight, halfConsole),
            materials.Console,
            layout.Recipe.ConsoleCellSize);
    }

    private static void ComposeSideHousing(
        RecipeWriter writer,
        BridgeLayout layout,
        BridgeMaterials materials,
        Func<float, float, float, Vector3> world,
        Action<string, ImplicitRecipe, ImplicitNode, Vector3, Vector3, Material, float> surface)
    {
        float faceX = layout.SideHousingFaceX;
        float eastX = faceX + layout.SideHousingDepthX;
        float centerZ = layout.SideHousingCenterZ;
        float halfHousing = layout.SideHousingWidthZ / 2.0f;

        using ImplicitRecipe f = writer.Begin();
        ImplicitNode body = f.Box(
            world(faceX, 0.0f, centerZ - halfHousing),
            world(eastX, layout.SideHousingHeight, centerZ + halfHousing));

        float displayHalfW = layout.SideDisplayWidth / 2.0f;
        float displayHalfH = layout.SideDisplayHeight / 2.0f;
        ImplicitNode recess = f.Box(
            world(faceX - layout.Recipe.CutOvershoot, layout.SideDisplayCenterHeight - displayHalfH, centerZ - displayHalfW),
            world(faceX + layout.RecessDepth, layout.SideDisplayCenterHeight + displayHalfH, centerZ + displayHalfW));
        body = f.Subtract(body, recess);

        surface(
            "side instrument housing",
            f,
            body,
            new Vector3(faceX, 0.0f, centerZ - halfHousing),
            new Vector3(eastX, layout.SideHousingHeight, centerZ + halfHousing),
            materials.Console,
            layout.Recipe.ConsoleCellSize);
    }

    private static void ComposeSeat(
        RecipeWriter writer,
        BridgeLayout layout,
        BridgeMaterials materials,
        Func<float, float, float, Vector3> world,
        Action<string, ImplicitRecipe, ImplicitNode, Vector3, Vector3, Material, float> surface)
    {
        BridgeSeatDefinition seatShape = layout.Recipe.Seat;
        float sx = layout.SeatCenter.X;
        float sz = layout.SeatCenter.Z;

        using ImplicitRecipe f = writer.Begin();
        ImplicitNode pedestal = f.Box(
            world(sx - seatShape.PedestalHalfWidth, 0.0f, sz - seatShape.PedestalHalfWidth),
            world(sx + seatShape.PedestalHalfWidth, seatShape.PedestalTop, sz + seatShape.PedestalHalfWidth));
        ImplicitNode cushion = f.Box(
            world(sx - seatShape.CushionHalfWidth, seatShape.PedestalTop, sz - seatShape.CushionHalfWidth),
            world(sx + seatShape.CushionHalfWidth, seatShape.CushionTop, sz + seatShape.CushionHalfWidth));
        ImplicitNode backrest = f.Box(
            world(sx + seatShape.BackRearOffsetX, seatShape.CushionTop, sz - seatShape.CushionHalfWidth),
            world(sx + seatShape.BackFrontOffsetX, seatShape.BackTop, sz + seatShape.CushionHalfWidth));
        ImplicitNode seat = f.Union(pedestal, f.Union(cushion, backrest));

        surface(
            "bridge seat",
            f,
            seat,
            new Vector3(sx + Math.Min(seatShape.BackRearOffsetX, -seatShape.CushionHalfWidth), 0.0f, sz - seatShape.CushionHalfWidth),
            new Vector3(sx + Math.Max(seatShape.BackFrontOffsetX, seatShape.CushionHalfWidth), seatShape.BackTop, sz + seatShape.CushionHalfWidth),
            materials.SeatFabric,
            layout.Recipe.SeatCellSize);
    }

    private static void ComposeEngineering(
        RecipeWriter writer,
        BridgeLayout layout,
        BridgeMaterials materials,
        Func<float, float, float, Vector3> world,
        Action<string, ImplicitRecipe, ImplicitNode, Vector3, Vector3, Material, float> surface)
    {
        float halfL = layout.RoomLengthX / 2.0f;
        float halfW = layout.RoomWidthZ / 2.0f;

        using ImplicitRecipe f = writer.Begin();
        ImplicitNode tall = f.Box(
            world(-halfL, 0.0f, -halfW),
            world(-halfL + layout.CabinetDepth, layout.CabinetHeight, -halfW + layout.CabinetLengthZ));
        ImplicitNode low = f.Box(
            world(-halfL + layout.CabinetDepth, 0.0f, -halfW),
            world(-halfL + layout.CabinetDepth + layout.LowCabinetLengthX, layout.LowCabinetHeight, -halfW + layout.CabinetDepth));
        ImplicitNode corner = f.Union(tall, low);

        surface(
            "engineering corner",
            f,
            corner,
            new Vector3(-halfL, 0.0f, -halfW),
            new Vector3(-halfL + layout.CabinetDepth + layout.LowCabinetLengthX, layout.CabinetHeight, -halfW + layout.CabinetLengthZ),
            materials.Cabinet,
            layout.Recipe.CabinetCellSize);
    }

}

/// <summary>
/// The Engine material handles one bridge set extracts its surfaces with.
/// Created once by <see cref="BridgeSet"/> and released with it.
/// </summary>
internal sealed record BridgeMaterials(
    Material Floor,
    Material Walls,
    Material Ceiling,
    Material Console,
    Material SeatFabric,
    Material Cabinet);
