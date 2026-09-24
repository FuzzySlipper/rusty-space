using System;
using System.Collections.Generic;
using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Implicit;

namespace Rusty.Space.Product.Bridge;

/// <summary>
/// Product owner for the bridge set's staged Engine resources. Generates the
/// recipe's meshes once during construction, retains mesh resources and
/// appearances for the scene lifetime, and publishes one static fact per
/// addressable part. Temporary authoring fields are released by the recipe's
/// scoped surfaces as each extraction returns.
/// </summary>
/// <remarks>
/// <para>
/// This owner stages geometry and neutral presentation facts only. It never
/// reads flight state, never writes it, and never steps anything: filtered
/// camera response, lighting and fault reactions, prop animation, audio, and
/// the flight-fact repeater belong to the parent theater (#8313), which
/// consumes <see cref="BridgeLayout.Placements"/> and the part identities in
/// <see cref="BridgePart"/>.
/// </para>
/// <para>
/// All facts are precomputed and stationary. The set does not travel or
/// rotate with the simulated ship and cannot feed back into flight physics.
/// The loose prop is a primitive module whose fact translation is its pivot:
/// the parent re-poses it by republishing that fact's transform about
/// <see cref="BridgePlacements.PropPivot"/>, with no re-extraction.
/// </para>
/// </remarks>
internal sealed class BridgeSet : IDisposable
{
    /// <summary>First Engine object id owned by the set; clear of the chart ids.</summary>
    internal const ulong FirstBridgeObjectId = 7_000UL;

    /// <summary>First Engine logical light id owned by the set.</summary>
    internal const ulong FirstBridgeLightId = 8_001UL;

    // The strip's fault lamp rests dim red in the neutral presentation; the
    // parent theater owns any fault reaction that lights it.
    private static readonly Color StripFaultColor = new(0.50f, 0.12f, 0.10f, 1.0f);

    // Lit twins for the two driven strip lamps: the presentation publishes
    // one twin visible and the other hidden, so a lamp changes state without
    // replacing an appearance handle mid-life.
    private static readonly Color FaultLampLitColor = new(1.0f, 0.25f, 0.18f, 1.0f);
    private static readonly Color ReadyLampLitColor = new(0.75f, 1.0f, 0.95f, 1.0f);

    // The load needle reads warm paper-white against the console body so the
    // theater's repeater stays legible at a glance from the seat.
    private static readonly Color NeedleColor = new(1.0f, 0.88f, 0.66f, 1.0f);

    private static readonly Vector3 NoEmissionVector = Vector3.Zero;
    private static readonly Color WhiteTint = new(1.0f, 1.0f, 1.0f, 1.0f);
    private const float MatteRoughness = 0.85f;
    private const float SatinRoughness = 0.55f;

    /// <summary>Edge length of the lamp cubes staged on the set.</summary>
    private const float LampSize = 0.07f;

    private const float LampHalfSize = LampSize / 2.0f;

    /// <summary>Air gap between a lamp cube and the face it reads on.</summary>
    private const float LampFaceGap = 0.001f;

    private static readonly BridgePart[] PresentationParts =
    [
        BridgePart.PropSlate,
        BridgePart.MainDisplay,
        BridgePart.SideDisplay,
        BridgePart.EngineeringDisplay,
        BridgePart.StripReadyLamp,
        BridgePart.StripCautionLamp,
        BridgePart.StripFaultLamp,
        BridgePart.EngineeringStatusLamp,
        BridgePart.EngineeringTaskLamp,
        BridgePart.FaultLampLit,
        BridgePart.ReadyLampLit,
        BridgePart.LoadNeedle,
    ];

    private readonly IGraphicsService appearance;
    private readonly List<MeshResource> meshes = [];
    private readonly List<Appearance> meshAppearances = [];
    private readonly List<Appearance> primitiveAppearances = [];
    private readonly List<Material> materials = [];
    private readonly List<Light> lights = [];
    private readonly List<string> partNames = [];
    private readonly AppearanceFact[] facts;
    private LightRequest overheadRequest;
    private LightRequest helmRequest;
    private Transform propBase = BridgeRecipe.Identity;
    private Transform needleBase = BridgeRecipe.Identity;
    private bool released;

    internal BridgeSet(IGraphicsService appearance, IImplicitSurfacesService implicitSurfaces, BridgeLayout layout)
    {
        this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        ArgumentNullException.ThrowIfNull(implicitSurfaces);
        ArgumentNullException.ThrowIfNull(layout);
        Layout = layout.Validate();

        try
        {
            BridgeMaterials recipeMaterials = CreateMaterials(Layout.Palette);
            var placements = new List<Transform>();
            BridgeRecipe.Compose(
                implicitSurfaces,
                Layout,
                recipeMaterials,
                surface =>
                {
                    MeshResource mesh = implicitSurfaces.Generate(new ImplicitGenerateRequest(
                        surface.Field,
                        surface.Root,
                        surface.Min,
                        surface.Max,
                        surface.Sampling.CellSize,
                        surface.Sampling.CreaseDegrees,
                        surface.Sampling.TextureRepeats,
                        surface.Sampling.TextureMapping,
                        surface.Material,
                        surface.Regions,
                        surface.Sampling.MaterialBoundaries,
                        surface.Sampling.MaterialSampleSpacing,
                        surface.Sampling.MaxExtractionVertices,
                        surface.Sampling.MaxExtractionTriangles));
                    meshes.Add(mesh);
                    placements.Add(surface.Placement);
                    meshAppearances.Add(appearance.CreateMeshAppearance(mesh));
                    partNames.Add(surface.Name);
                });

            CreateScreensAndLamps();
            CreatePracticalLights();
            facts = BuildFacts(placements);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal BridgeLayout Layout { get; }

    internal BridgePlacements Placements => Layout.Placements;

    internal int ImplicitSurfaceCount => meshes.Count;

    internal IReadOnlyList<string> PartNames => partNames;

    internal ReadOnlySpan<AppearanceFact> Facts => facts;

    private BridgeMaterials CreateMaterials(BridgePalette palette)
    {
        // Solid product colors with no content texture: the untextured
        // reference leaves albedo at the material color. Roughness stays
        // broad and restrained; wear and labels follow the layout.
        Material Solid(Color color, float roughness) => CreateMaterial(color, roughness);
        BridgeMaterials made = new(
            Floor: Solid(palette.Floor, MatteRoughness),
            Walls: Solid(palette.Walls, MatteRoughness),
            Ceiling: Solid(palette.Ceiling, MatteRoughness),
            Console: Solid(palette.Console, SatinRoughness),
            SeatFabric: Solid(palette.SeatFabric, MatteRoughness),
            Cabinet: Solid(palette.Cabinet, SatinRoughness));
        return made;
    }

    private Material CreateMaterial(Color color, float roughness)
    {
        Material material = appearance.CreateMaterial(new MaterialRequest(
            color,
            new RenderResourceReference(0UL),
            roughness,
            WhiteTint,
            NoEmissionVector,
            0.0f,
            DoubleSided: false,
            MaterialAlphaMode.Opaque,
            0.5f));
        materials.Add(material);
        return material;
    }

    private void CreateScreensAndLamps()
    {
        BridgePalette palette = Layout.Palette;

        // Loose prop slate first: identity order below follows creation
        // order, and the prop owns the first presentation identity. It is a
        // plain module whose fact translation is its pivot, so the parent
        // theater can re-pose it with transform updates alone and never
        // re-extracts geometry to animate it.
        primitiveAppearances.Add(appearance.CreatePrimitive(new PrimitiveAppearanceRequest(
            PrimitiveGeometry.Cube, Wireframe: false, palette.Prop)));
        partNames.Add("loose prop slate");

        // Main display module seated in the helm recess, facing the seat.
        primitiveAppearances.Add(appearance.CreatePrimitive(new PrimitiveAppearanceRequest(
            PrimitiveGeometry.Cube, Wireframe: false, palette.MainDisplay)));
        partNames.Add("main display");

        // Side instrument repeater in its own housing recess.
        primitiveAppearances.Add(appearance.CreatePrimitive(new PrimitiveAppearanceRequest(
            PrimitiveGeometry.Cube, Wireframe: false, palette.SideDisplay)));
        partNames.Add("side display");

        // Engineering status repeater mounted on the tall cabinet face.
        primitiveAppearances.Add(appearance.CreatePrimitive(new PrimitiveAppearanceRequest(
            PrimitiveGeometry.Cube, Wireframe: false, palette.EngineeringDisplay)));
        partNames.Add("engineering display");

        // Control-strip lamp bank: ready, caution, and a resting fault lamp.
        primitiveAppearances.Add(appearance.CreatePrimitive(new PrimitiveAppearanceRequest(
            PrimitiveGeometry.Cube, Wireframe: false, palette.MainDisplay)));
        partNames.Add("strip ready lamp");
        primitiveAppearances.Add(appearance.CreatePrimitive(new PrimitiveAppearanceRequest(
            PrimitiveGeometry.Cube, Wireframe: false, palette.SideDisplay)));
        partNames.Add("strip caution lamp");
        primitiveAppearances.Add(appearance.CreatePrimitive(new PrimitiveAppearanceRequest(
            PrimitiveGeometry.Cube, Wireframe: false, StripFaultColor)));
        partNames.Add("strip fault lamp");

        // Engineering corner lamps: status violet plus a warm task lamp.
        primitiveAppearances.Add(appearance.CreatePrimitive(new PrimitiveAppearanceRequest(
            PrimitiveGeometry.Cube, Wireframe: false, palette.EngineeringDisplay)));
        partNames.Add("engineering status lamp");
        primitiveAppearances.Add(appearance.CreatePrimitive(new PrimitiveAppearanceRequest(
            PrimitiveGeometry.Cube, Wireframe: false, palette.OverheadGlow)));
        partNames.Add("engineering task lamp");

        // Lit twins for the driven strip lamps, then the load needle: the
        // needle is a thin module below the side display whose fact rotation
        // the theater sweeps with field load. Identities follow creation
        // order, and only one lamp twin is ever published visible.
        primitiveAppearances.Add(appearance.CreatePrimitive(new PrimitiveAppearanceRequest(
            PrimitiveGeometry.Cube, Wireframe: false, FaultLampLitColor)));
        partNames.Add("strip fault lamp (lit)");
        primitiveAppearances.Add(appearance.CreatePrimitive(new PrimitiveAppearanceRequest(
            PrimitiveGeometry.Cube, Wireframe: false, ReadyLampLitColor)));
        partNames.Add("strip ready lamp (lit)");
        primitiveAppearances.Add(appearance.CreatePrimitive(new PrimitiveAppearanceRequest(
            PrimitiveGeometry.Cube, Wireframe: false, NeedleColor)));
        partNames.Add("load needle");
    }

    private void CreatePracticalLights()
    {
        BridgePlacements placements = Layout.Placements;
        BridgePalette palette = Layout.Palette;
        overheadRequest = new LightRequest(
            FirstBridgeLightId,
            HasParentObject: false,
            ParentObjectId: 0,
            new LightDescriptor(
                LightKind.Point,
                new Vector3(palette.OverheadGlow.R, palette.OverheadGlow.G, palette.OverheadGlow.B),
                Intensity: 2.0f,
                Enabled: true,
                placements.OverheadLight,
                Direction: -Vector3.UnitY,
                HasRange: true,
                Range: Layout.OverheadLightRange,
                Decay: 2.0f,
                OuterAngleRadians: 0.0f,
                Penumbra: 0.0f,
                LightShadowIntent.Disabled));
        helmRequest = overheadRequest with
        {
            LogicalId = FirstBridgeLightId + 1,
            Descriptor = new LightDescriptor(
                LightKind.Point,
                new Vector3(palette.HelmGlow.R, palette.HelmGlow.G, palette.HelmGlow.B),
                Intensity: 1.0f,
                Enabled: true,
                placements.HelmLight,
                Direction: -Vector3.UnitY,
                HasRange: true,
                Range: Layout.HelmLightRange,
                Decay: 2.0f,
                OuterAngleRadians: 0.0f,
                Penumbra: 0.0f,
                LightShadowIntent.Disabled),
        };
        lights.Add(appearance.CreateLight(overheadRequest));
        lights.Add(appearance.CreateLight(helmRequest));
    }

    /// <summary>
    /// The theater's per-turn light levels as fractions of the staged base
    /// intensities: dimming under load, brownout sag and flicker on faults.
    /// </summary>
    internal void UpdatePracticalLights(float overheadLevel, float helmLevel)
    {
        appearance.UpdateLight(new LightUpdateRequest(
            lights[0],
            overheadRequest with
            {
                Descriptor = overheadRequest.Descriptor with
                {
                    Intensity = overheadRequest.Descriptor.Intensity * overheadLevel,
                },
            }));
        appearance.UpdateLight(new LightUpdateRequest(
            lights[1],
            helmRequest with
            {
                Descriptor = helmRequest.Descriptor with
                {
                    Intensity = helmRequest.Descriptor.Intensity * helmLevel,
                },
            }));
    }

    /// <summary>
    /// Re-poses the loose prop about its pivot by rewriting its fact's
    /// rotation. The mesh is never re-extracted; the next snapshot carries
    /// the new transform.
    /// </summary>
    internal void SetPropSway(Quaternion sway)
    {
        int propIndex = meshes.Count;
        facts[propIndex] = facts[propIndex] with
        {
            Transform = propBase with { Rotation = sway * propBase.Rotation },
        };
    }

    /// <summary>
    /// Sweeps the load needle about the housing face normal by rewriting its
    /// fact's rotation, the same staged-fact lane as the prop.
    /// </summary>
    internal void SetNeedleRotation(Quaternion rotation)
    {
        int needleIndex = meshes.Count + (int)BridgePart.LoadNeedle - (int)BridgePart.PropSlate;
        facts[needleIndex] = facts[needleIndex] with
        {
            Transform = needleBase with { Rotation = rotation * needleBase.Rotation },
        };
    }

    private AppearanceFact[] BuildFacts(List<Transform> placements)
    {
        var built = new AppearanceFact[checked(meshes.Count + PresentationParts.Length)];
        int index = 0;
        for (int piece = 0; piece < meshes.Count; piece++)
        {
            built[index++] = new AppearanceFact(
                FirstBridgeObjectId + (ulong)piece,
                false,
                0,
                placements[piece],
                meshAppearances[piece],
                Visible: true,
                RenderLayer.Scene);
        }

        foreach (BridgePart part in PresentationParts)
        {
            built[index++] = new AppearanceFact(
                FirstBridgeObjectId + (ulong)part,
                false,
                0,
                PartTransform(part),
                primitiveAppearances[(int)part - (int)BridgePart.PropSlate],
                Visible: true,
                RenderLayer.Scene);
        }

        propBase = PartTransform(BridgePart.PropSlate);
        needleBase = PartTransform(BridgePart.LoadNeedle);
        return built;
    }

    /// <summary>Center of the load needle's sweep on the side housing face.</summary>
    internal Vector3 NeedlePivot => Layout.ToWorld(new Vector3(
        Layout.SideHousingFaceX - 0.04f,
        0.45f,
        Layout.SideHousingCenterZ));

    private Transform PartTransform(BridgePart part)
    {
        return part switch
        {
            BridgePart.MainDisplay => new Transform(
                Layout.InstrumentCenter,
                BridgeRecipe.Identity.Rotation,
                new Vector3(0.03f, Layout.DisplayHeight, Layout.DisplayWidth)),
            BridgePart.SideDisplay => new Transform(
                Layout.SideInstrumentCenter,
                BridgeRecipe.Identity.Rotation,
                new Vector3(0.03f, Layout.SideDisplayHeight, Layout.SideDisplayWidth)),
            BridgePart.EngineeringDisplay => new Transform(
                EngineeringDisplayCenter,
                BridgeRecipe.Identity.Rotation,
                new Vector3(0.03f, 0.30f, 0.50f)),
            BridgePart.StripReadyLamp => LampOnStrip(-0.5f),
            BridgePart.StripCautionLamp => LampOnStrip(0.0f),
            BridgePart.StripFaultLamp => LampOnStrip(0.5f),
            BridgePart.EngineeringStatusLamp => new Transform(
                Layout.ToWorld(new Vector3(
                    -Layout.RoomLengthX / 2.0f + Layout.CabinetDepth + LampHalfSize + LampFaceGap,
                    1.45f,
                    -Layout.RoomWidthZ / 2.0f + 0.25f)),
                BridgeRecipe.Identity.Rotation,
                new Vector3(LampSize, LampSize, LampSize)),
            BridgePart.EngineeringTaskLamp => new Transform(
                Layout.ToWorld(new Vector3(
                    -Layout.RoomLengthX / 2.0f + Layout.CabinetDepth + 0.6f,
                    Layout.LowCabinetHeight + LampHalfSize + LampFaceGap,
                    -Layout.RoomWidthZ / 2.0f + Layout.CabinetDepth / 2.0f)),
                BridgeRecipe.Identity.Rotation,
                new Vector3(LampSize, LampSize, LampSize)),
            BridgePart.PropSlate => new Transform(
                Placements.PropPivot,
                BridgeRecipe.Identity.Rotation,
                Layout.PropSize),
            BridgePart.FaultLampLit => LampOnStrip(0.5f),
            BridgePart.ReadyLampLit => LampOnStrip(-0.5f),
            BridgePart.LoadNeedle => new Transform(
                NeedlePivot,
                BridgeRecipe.Identity.Rotation,
                new Vector3(0.025f, 0.34f, 0.025f)),
            // The switch names every member; anything else is a programming
            // error the construction-time BuildFacts surfaces at once.
            _ => throw new ArgumentOutOfRangeException(nameof(part)),
        };
    }

    private Transform LampOnStrip(float zOffset) => new(
        Layout.ToWorld(new Vector3(
            Layout.ConsoleFaceX - Layout.ControlStripProtrusion - LampHalfSize - LampFaceGap,
            Layout.ControlStripCenterHeight,
            zOffset)),
        BridgeRecipe.Identity.Rotation,
        new Vector3(LampSize, LampSize, LampSize));

    private Vector3 EngineeringDisplayCenter => Layout.ToWorld(new Vector3(
        // A hair proud of the cabinet face so the module back never z-fights it.
        -Layout.RoomLengthX / 2.0f + Layout.CabinetDepth + 0.016f,
        1.20f,
        -Layout.RoomWidthZ / 2.0f + Layout.CabinetLengthZ / 2.0f));

    /// <summary>
    /// Releases staged handles in the reverse of the order that opened them:
    /// lights first, then primitive and mesh appearances, then the mesh
    /// resources, then the materials they were extracted with.
    /// </summary>
    public void Dispose()
    {
        if (released)
        {
            return;
        }

        released = true;
        List<Exception> errors = [];
        void Release(IDisposable? resource)
        {
            try
            {
                resource?.Dispose();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        foreach (Light light in lights)
        {
            Release(light);
        }

        foreach (Appearance screen in primitiveAppearances)
        {
            Release(screen);
        }

        foreach (Appearance staged in meshAppearances)
        {
            Release(staged);
        }

        foreach (MeshResource mesh in meshes)
        {
            Release(mesh);
        }

        foreach (Material material in materials)
        {
            Release(material);
        }

        if (errors.Count > 0)
        {
            throw new AggregateException(errors);
        }
    }
}

/// <summary>
/// The separately addressable presentation objects of the set, in fact order
/// after the implicit surfaces. The parent theater addresses screens, lamps,
/// and the prop by these identities; implicit surface parts are addressed by
/// their recipe names in <see cref="BridgeSet.PartNames"/> order.
/// </summary>
internal enum BridgePart
{
    PropSlate = 7,
    MainDisplay = 8,
    SideDisplay = 9,
    EngineeringDisplay = 10,
    StripReadyLamp = 11,
    StripCautionLamp = 12,
    StripFaultLamp = 13,
    EngineeringStatusLamp = 14,
    EngineeringTaskLamp = 15,
    FaultLampLit = 16,
    ReadyLampLit = 17,
    LoadNeedle = 18,
}
