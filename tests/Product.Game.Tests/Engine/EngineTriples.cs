using System.Numerics;
using Rusty.Engine;

namespace Rusty.Space.Product.Engine.Tests;

/// <summary>
/// The fault a test plants into one named Engine call, so a create that dies
/// partway has somewhere to die and the product's unwind has something to
/// unwind through.
/// </summary>
internal sealed class InjectedFault(string operation) : Exception($"injected Engine fault at {operation}")
{
    internal string Operation { get; } = operation;
}

/// <summary>
/// Shared bookkeeping for the Engine doubles below: which named call fails next,
/// and the order in which handles were handed back.
/// </summary>
internal sealed class ServiceFaults
{
    private readonly List<string> releases = [];

    internal string? FailOn { get; set; }

    internal IReadOnlyList<string> Releases => releases;

    /// <summary>
    /// The released owners in order, with repeats from one owner folded into
    /// one entry, so a test names the sequence it expects rather than counting
    /// silhouettes.
    /// </summary>
    internal string[] OwnersReleasedInOrder()
    {
        List<string> owners = [];
        foreach (string owner in releases)
        {
            if (owners.Count == 0 || owners[^1] != owner)
            {
                owners.Add(owner);
            }
        }

        return [.. owners];
    }

    internal void FailIf(string operation)
    {
        if (FailOn == operation)
        {
            throw new InjectedFault(operation);
        }
    }

    internal void RecordRelease(string owner) => releases.Add(owner);
}

/// <summary>
/// An <see cref="IEngineContext"/> whose four services Space actually composes
/// against are recording doubles; every other service is absent, so a product
/// change that reaches for one fails at once instead of quietly working.
/// </summary>
internal sealed class RecordingEngine : IEngineContext
{
    public ServiceFaults Faults { get; } = new();

    public RecordingDynamics Dynamics { get; }
    public RecordingGraphics Graphics { get; }
    public RecordingUi Ui { get; }
    public RecordingCameraView CameraView { get; }

    public RecordingEngine()
    {
        Dynamics = new RecordingDynamics(Faults);
        Graphics = new RecordingGraphics(Faults);
        Ui = new RecordingUi(Faults);
        CameraView = new RecordingCameraView(Faults);
    }

    public IImplicitSurfacesService ImplicitSurfaces => Absent<IImplicitSurfacesService>();
    public IDiagnosticsService Diagnostics => Absent<IDiagnosticsService>();
    IDynamicsService IEngineContext.Dynamics => Dynamics;
    public IMotionService Motion => Absent<IMotionService>();
    public IKinematicService Kinematic => Absent<IKinematicService>();
    public ISpatialService Spatial => Absent<ISpatialService>();
    public IPerceptionService Perception => Absent<IPerceptionService>();
    public IWorldOriginService WorldOrigin => Absent<IWorldOriginService>();
    public IVoxelService Voxel => Absent<IVoxelService>();
    public IVoxelContentService VoxelContent => Absent<IVoxelContentService>();
    public IVoxelScenePresentationService VoxelScenePresentation => Absent<IVoxelScenePresentationService>();
    public IContentService Content => Absent<IContentService>();
    public IAuthoredContentService AuthoredContent => Absent<IAuthoredContentService>();
    IGraphicsService IEngineContext.Graphics => Graphics;
    public IPresentationService Presentation => Absent<IPresentationService>();
    public IAnimationService Animation => Absent<IAnimationService>();
    public IAudioService Audio => Absent<IAudioService>();
    ICameraViewService IEngineContext.CameraView => CameraView;
    public IRandomService Random => Absent<IRandomService>();
    public IPersistenceService Persistence => Absent<IPersistenceService>();
    public IContentStoreService ContentStore => Absent<IContentStoreService>();
    IUiService IEngineContext.Ui => Ui;

    private static T Absent<T>() => throw new NotSupportedException(
        $"Space does not compose {typeof(T).Name}; a product that reaches for it is a design change, not a test.");
}

internal static class ProductContexts
{
    internal static ProductCreateContext For(RecordingEngine engine) => new(
        engine,
        new ProductContent(default),
        new ProductInputConfiguration(default, default, default, default));
}

/// <summary>
/// The smallest Dynamics service that records what the product asked the
/// integrator for and hands out lease handles a test can watch being released.
/// It echoes the attitude each body was created with, so a heading authored by
/// the product travels the same path it travels in a live host. Anything Space
/// never calls is refused loudly, so a change that starts reaching for another
/// path is a test failure rather than a silent widening of the seam.
/// </summary>
internal sealed class RecordingDynamics(ServiceFaults faults) : IDynamicsService
{
    private const float SlipPerRead = 0.5f;

    internal RecordingDynamics()
        : this(new ServiceFaults())
    {
    }

    private readonly ServiceFaults faults = faults;
    private Quaternion createdAttitude = Quaternion.Identity;

    internal List<DynamicsStepRequest> Steps { get; } = [];
    internal int Reads { get; private set; }
    internal int BodyCreates { get; private set; }
    internal int BodyReleases { get; private set; }
    internal int WorldReleases { get; private set; }

    public DynamicsWorld CreateWorld(DynamicsWorldConfig arg0)
    {
        faults.FailIf(nameof(CreateWorld));
        return new DynamicsWorld(default, RecordWorldRelease);
    }

    public DynamicsBody CreateBody(DynamicsCreateBodyRequest arg0)
    {
        BodyCreates++;
        createdAttitude = arg0.Body.Transform.Rotation;
        return new DynamicsBody(default, RecordBodyRelease);
    }

    public DynamicsStepReceipt Step(DynamicsStepRequest arg0)
    {
        Steps.Add(arg0);
        return new DynamicsStepReceipt((ulong)Steps.Count, 1U, 0U);
    }

    public DynamicsReadout Read(DynamicsReadRequest arg0) => new(
        new Transform(Vector3.Zero, createdAttitude, Vector3.One),
        new Vector3(SlipPerRead * ++Reads, 0.0f, 0.0f),
        Vector3.Zero,
        Sleeping: false,
        new MassProperties(
            Available: true,
            Mass: BodyMass,
            PrincipalInertia: new Vector3(BodyInertia, BodyInertia, BodyInertia),
            Policy: DynamicsMassPolicyKind.DeriveFromShapeAndMass,
            CenterOfMass: Vector3.Zero,
            PrincipalInertiaLocalFrame: Quaternion.Identity),
        ContactCount: 0U,
        FirstContact: default);

    public DynamicsBody CreateSphereBody(DynamicsCreateSphereBodyRequest arg0)
        => throw new NotSupportedException();

    public DynamicsBody CreateCuboidBody(DynamicsCreateCuboidBodyRequest arg0)
        => throw new NotSupportedException();

    public DynamicsBody CreateSphereBodyWithProperties(DynamicsCreateSphereBodyPropertiesRequest arg0)
        => throw new NotSupportedException();

    public DynamicsBody CreateCapsuleBody(DynamicsCreateCapsuleBodyRequest arg0)
        => throw new NotSupportedException();

    public void BindWorldCollision(DynamicsWorldCollisionBindingRequest arg0)
        => throw new NotSupportedException();

    public DynamicsRebaseWorldOriginReceipt RebaseWorldOrigin(DynamicsRebaseWorldOriginRequest arg0)
        => throw new NotSupportedException();

    public DynamicsStepAndReadLeaseReceipt StepAndRead(DynamicsStepAndReadRequest arg0)
        => throw new NotSupportedException();

    public void Reset(DynamicsResetRequest arg0) => throw new NotSupportedException();

    public void UpdateBody(DynamicsUpdateBodyRequest arg0) => throw new NotSupportedException();

    public DynamicsWorldReadout ReadWorld(DynamicsWorldReadRequest arg0)
        => throw new NotSupportedException();

    public DynamicsBodyAtReceipt ReadBodyAt(DynamicsBodyAtRequest arg0)
        => throw new NotSupportedException();

    public DynamicsContactAtReceipt ReadContactAt(DynamicsContactAtRequest arg0)
        => throw new NotSupportedException();

    public DynamicsBody ReplaceBody(DynamicsReplaceBodyRequest arg0)
        => throw new NotSupportedException();

    public DynamicsBody ReplaceCuboidBody(DynamicsReplaceCuboidBodyRequest arg0)
        => throw new NotSupportedException();

    public DynamicsBody ReplaceSphereBody(DynamicsReplaceSphereBodyRequest arg0)
        => throw new NotSupportedException();

    public DynamicsBody ReplaceCapsuleBody(DynamicsReplaceCapsuleBodyRequest arg0)
        => throw new NotSupportedException();

    private const float BodyMass = 2.0f;
    private const float BodyInertia = 2.0f;

    private void RecordWorldRelease()
    {
        WorldReleases++;
        faults.RecordRelease("world");
    }

    private void RecordBodyRelease()
    {
        BodyReleases++;
        faults.RecordRelease("body");
    }
}

/// <summary>
/// The render side of the same seam: it hands out appearance leases a test can
/// watch being released and refuses the surface Space never asks for.
/// </summary>
internal sealed class RecordingGraphics(ServiceFaults faults) : IGraphicsService
{
    private readonly ServiceFaults faults = faults;

    internal int AppearanceReleases { get; private set; }
    internal int SnapshotPublications { get; private set; }

    public Appearance CreatePrimitive(PrimitiveAppearanceRequest arg0)
    {
        faults.FailIf(nameof(CreatePrimitive));
        return new Appearance(default, RecordAppearanceRelease);
    }

    public Appearance CreateStaticMeshFromContent(StaticMeshContentAppearanceRequest arg0)
    {
        faults.FailIf(nameof(CreateStaticMeshFromContent));
        return new Appearance(default, RecordAppearanceRelease);
    }

    public void PublishSnapshot(ReadOnlySpan<AppearanceFact> values)
    {
        faults.FailIf(nameof(PublishSnapshot));
        SnapshotPublications++;
    }

    public RenderResourceInfo OpenResource(RenderResourceRequest arg0) => throw new NotSupportedException();

    public RenderResourceInfo OpenResourceFromContent(RenderResourceContentRequest arg0)
        => throw new NotSupportedException();

    public Appearance CreateStaticMeshFromContentReference(StaticMeshContentReferenceRequest arg0)
        => throw new NotSupportedException();

    public Material CreateMaterial(MaterialRequest arg0) => throw new NotSupportedException();

    public void UpdateMaterial(MaterialUpdateRequest arg0) => throw new NotSupportedException();

    public Material ReplaceMaterial(MaterialUpdateRequest arg0) => throw new NotSupportedException();

    public Appearance ReplacePrimitive(PrimitiveAppearanceReplaceRequest arg0)
        => throw new NotSupportedException();

    public MeshResource CreateMeshResource(MeshResourceCreateRequest arg0) => throw new NotSupportedException();

    public Appearance CreateMeshAppearance(MeshResource arg0) => throw new NotSupportedException();

    public MeshPartition PartitionMesh(MeshPartitionRequest arg0) => throw new NotSupportedException();

    public MeshPartitionReadout ReadMeshPartition(MeshPartition arg0) => throw new NotSupportedException();

    public MeshResource TakeMeshPartitionPart(MeshPartitionPartRequest arg0) => throw new NotSupportedException();

    public Appearance CreateStaticMesh(StaticMeshAppearanceRequest arg0) => throw new NotSupportedException();

    public Appearance ReplaceStaticMesh(Appearance arg0, StaticMeshAppearanceRequest arg1)
        => throw new NotSupportedException();

    public Appearance ReplaceStaticMeshFromContent(Appearance arg0, StaticMeshContentAppearanceRequest arg1)
        => throw new NotSupportedException();

    public void UpdateStaticMeshMaterials(StaticMeshMaterialUpdateRequest arg0)
        => throw new NotSupportedException();

    public Appearance CreateSprite(SpriteAppearanceRequest arg0) => throw new NotSupportedException();

    public Appearance ReplaceSprite(SpriteAppearanceReplaceRequest arg0) => throw new NotSupportedException();

    public SpriteAtlas CreateSpriteAtlas(SpriteAtlasCreateRequest arg0) => throw new NotSupportedException();

    public Appearance CreateSpriteFromAtlas(SpriteFromAtlasRequest arg0) => throw new NotSupportedException();

    public Appearance ReplaceSpriteFromAtlas(SpriteFromAtlasReplaceRequest arg0) => throw new NotSupportedException();

    public void SetSpriteFrame(SpriteFrameUpdateRequest arg0) => throw new NotSupportedException();

    public void SetSpriteViewport(SpriteViewportUpdateRequest arg0) => throw new NotSupportedException();

    public SpriteReadout ReadSprite(Appearance arg0) => throw new NotSupportedException();

    public SpritePlayback CreateSpritePlayback(SpritePlaybackCreateRequest arg0) => throw new NotSupportedException();

    public SpritePlaybackReadout ControlSpritePlayback(SpritePlaybackControlRequest arg0)
        => throw new NotSupportedException();

    public SpritePlaybackReadout SelectSpritePlaybackFrame(SpritePlaybackFrameSelectionRequest arg0)
        => throw new NotSupportedException();

    public SpritePlaybackAdvanceLeaseReceipt AdvanceSpritePlayback(SpritePlaybackAdvanceRequest arg0)
        => throw new NotSupportedException();

    public SpritePlaybackSample SampleSpritePlayback(SpritePlaybackSampleRequest arg0)
        => throw new NotSupportedException();

    public SpritePlaybackReadout ReadSpritePlayback(SpritePlayback arg0) => throw new NotSupportedException();

    public Light CreateLight(LightRequest arg0) => throw new NotSupportedException();

    public void UpdateLight(LightUpdateRequest arg0) => throw new NotSupportedException();

    public Light ReplaceLight(LightUpdateRequest arg0) => throw new NotSupportedException();

    public LightReadout ReadLight(Light arg0) => throw new NotSupportedException();

    public PresentationReadout ReadPresentation() => throw new NotSupportedException();

    public Material CreateAuthoredMaterial(AuthoredMaterialAppearanceRequest arg0)
        => throw new NotSupportedException();

    private void RecordAppearanceRelease()
    {
        AppearanceReleases++;
        faults.RecordRelease("appearance");
    }
}

internal sealed class RecordingUi(ServiceFaults faults) : IUiService
{
    private readonly ServiceFaults faults = faults;

    internal int StreamReleases { get; private set; }

    public UiStream OpenStream(UiStreamRequest arg0)
    {
        faults.FailIf(nameof(OpenStream));
        return new UiStream(default, RecordStreamRelease);
    }

    public void PublishProjection(UiProjection arg0)
    {
    }

    private void RecordStreamRelease()
    {
        StreamReleases++;
        faults.RecordRelease("ui");
    }
}

internal sealed class RecordingCameraView(ServiceFaults faults) : ICameraViewService
{
    private readonly ServiceFaults faults = faults;

    internal int CameraReleases { get; private set; }

    public Camera CreateCamera(CameraDescriptor arg0)
    {
        faults.FailIf(nameof(CreateCamera));
        return new Camera(default, RecordCameraRelease);
    }

    public void SetActiveCamera(Camera arg0) => faults.FailIf(nameof(SetActiveCamera));

    private void RecordCameraRelease()
    {
        CameraReleases++;
        faults.RecordRelease("camera");
    }

    public void UpdateCamera(CameraUpdateRequest arg0) => throw new NotSupportedException();

    public void UpdateCameraSample(CameraSampleRequest arg0) => throw new NotSupportedException();

    public Camera ReplaceCamera(CameraReplaceRequest arg0) => throw new NotSupportedException();

    public CameraTarget CreateCameraTarget(CameraTargetDescriptor arg0) => throw new NotSupportedException();

    public void UpdateCameraTarget(CameraTargetUpdateRequest arg0) => throw new NotSupportedException();

    public CameraTarget ReplaceCameraTarget(CameraTargetReplaceRequest arg0) => throw new NotSupportedException();

    public void SetCameraComposition(CameraCompositionRequest arg0) => throw new NotSupportedException();

    public void ClearActiveCamera(ClearActiveCameraRequest arg0) => throw new NotSupportedException();

    public void SetSkyBackground(RenderResource arg0) => throw new NotSupportedException();

    public void ClearSkyBackground(ClearSkyBackgroundRequest arg0) => throw new NotSupportedException();
}
