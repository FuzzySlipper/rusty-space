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

    /// <summary>Authored clearance around each extraction region.</summary>
    private const float ExtractionMargin = 0.3f;

    /// <summary>How far a recess cut starts outside its face so the opening rim survives.</summary>
    private const float CutOvershoot = 0.1f;

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
            new(0.12f, 0.0f, 0.25f, ImplicitMaterialBoundaryMode.Interpolated),
            emit);

        // All recipe math runs in the room-local frame; world placement is the
        // anchor added on the way to the Engine.
        Vector3 W(float x, float y, float z) => layout.ToWorld(new Vector3(x, y, z));

        void Surface(string name, ImplicitRecipe field, ImplicitNode solid, Vector3 localMin, Vector3 localMax, Material material, float cellSize)
        {
            Vector3 min = W(localMin.X, localMin.Y, localMin.Z) - new Vector3(ExtractionMargin);
            Vector3 max = W(localMax.X, localMax.Y, localMax.Z) + new Vector3(ExtractionMargin);
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
                0.15f);
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
                0.12f);
        }

        using (ImplicitRecipe f = writer.Begin())
        {
            float slabBase = layout.WallHeight;
            float slabTop = slabBase + layout.CeilingThickness;
            ImplicitNode slab = f.Box(
                W(-halfL - wallT, slabBase, -halfW - wallT),
                W(halfL + wallT, slabTop, halfW + wallT));
            foreach (float beamX in new[] { -1.0f, 1.0f })
            {
                ImplicitNode beam = f.Box(
                    W(beamX - 0.09f, slabBase - 0.25f, -halfW),
                    W(beamX + 0.09f, slabBase, halfW));
                slab = f.Union(slab, beam);
            }

            Surface(
                "bridge overhead",
                f,
                slab,
                new Vector3(-halfL - wallT, slabBase - 0.25f, -halfW - wallT),
                new Vector3(halfL + wallT, slabTop, halfW + wallT),
                materials.Ceiling,
                0.12f);
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
            world(faceX - layout.ControlStripProtrusion, layout.ControlStripCenterHeight - stripHalf, -halfConsole + BridgeLayout.ControlStripZInset),
            world(faceX + 0.02f, layout.ControlStripCenterHeight + stripHalf, halfConsole - BridgeLayout.ControlStripZInset));
        body = f.Union(body, strip);

        // Main display recess: cut from outside the face so a thick bezel rim
        // of console body survives around the opening.
        float displayHalfW = layout.DisplayWidth / 2.0f;
        float displayHalfH = layout.DisplayHeight / 2.0f;
        ImplicitNode recess = f.Box(
            world(faceX - CutOvershoot, layout.DisplayCenterHeight - displayHalfH, -displayHalfW),
            world(faceX + layout.RecessDepth, layout.DisplayCenterHeight + displayHalfH, displayHalfW));
        body = f.Subtract(body, recess);

        surface(
            "helm console",
            f,
            body,
            new Vector3(faceX - layout.ControlStripProtrusion, 0.0f, -halfConsole),
            new Vector3(eastX, layout.ConsoleHeight, halfConsole),
            materials.Console,
            0.05f);
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
            world(faceX - CutOvershoot, layout.SideDisplayCenterHeight - displayHalfH, centerZ - displayHalfW),
            world(faceX + layout.RecessDepth, layout.SideDisplayCenterHeight + displayHalfH, centerZ + displayHalfW));
        body = f.Subtract(body, recess);

        surface(
            "side instrument housing",
            f,
            body,
            new Vector3(faceX, 0.0f, centerZ - halfHousing),
            new Vector3(eastX, layout.SideHousingHeight, centerZ + halfHousing),
            materials.Console,
            0.05f);
    }

    private static void ComposeSeat(
        RecipeWriter writer,
        BridgeLayout layout,
        BridgeMaterials materials,
        Func<float, float, float, Vector3> world,
        Action<string, ImplicitRecipe, ImplicitNode, Vector3, Vector3, Material, float> surface)
    {
        float sx = layout.SeatCenter.X;
        float sz = layout.SeatCenter.Z;

        using ImplicitRecipe f = writer.Begin();
        ImplicitNode pedestal = f.Box(
            world(sx - 0.15f, 0.0f, sz - 0.15f),
            world(sx + 0.15f, 0.42f, sz + 0.15f));
        ImplicitNode cushion = f.Box(
            world(sx - 0.35f, 0.42f, sz - 0.35f),
            world(sx + 0.35f, 0.55f, sz + 0.35f));
        ImplicitNode backrest = f.Box(
            world(sx - 0.40f, 0.55f, sz - 0.35f),
            world(sx - 0.25f, 1.25f, sz + 0.35f));
        ImplicitNode seat = f.Union(pedestal, f.Union(cushion, backrest));

        surface(
            "bridge seat",
            f,
            seat,
            new Vector3(sx - 0.40f, 0.0f, sz - 0.35f),
            new Vector3(sx + 0.35f, 1.25f, sz + 0.35f),
            materials.SeatFabric,
            0.06f);
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
            0.08f);
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
