using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SkyBox;
using DCL.SkyBox.Components;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using SceneRunner.Scene;
using UnityEngine;
using Entity = Arch.Core.Entity;
using EnvironmentSources = DCL.SDKComponents.Skybox.SceneSkyboxComponent.EnvironmentSources;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;
using TextureSlot = DCL.SDKComponents.Skybox.SceneSkyboxComponent.TextureSlot;

namespace DCL.SDKComponents.Skybox.Systems
{
    /// <summary>
    ///     Loads the sky texture of the PBSkybox on the scene root entity, builds the look profile of its PBSkybox,
    ///     PBSun and PBEnvironmentFog and the sun override of its PBSun and, while the scene is current, publishes them
    ///     to the global world as the scene environment overrides. Any of the three components makes the scene the owner
    ///     of the overrides.
    /// </summary>
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.SKYBOX)]
    public partial class SceneSkyboxHandlerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener, IFinalizeWorldSystem
    {
        private const int GET_TEXTURE_MAX_ATTEMPT_COUNT = 6;

        // Repeat because the u=0/1 edge of the panorama is a continuous seam: clamping would sample a visible line there.
        private const TextureWrapMode SKY_WRAP_MODE = TextureWrapMode.Repeat;
        private const FilterMode SKY_FILTER_MODE = FilterMode.Bilinear;

        private static readonly QueryDescription GLOBAL_SKYBOX_QUERY = new QueryDescription().WithAll<SceneSkyboxOverrides>();

        private readonly World globalWorld;
        private readonly Entity rootEntity;
        private readonly ISceneData sceneData;
        private readonly IPartitionComponent scenePartition;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly SingleInstanceEntity globalSkyboxEntity;

        internal SceneSkyboxHandlerSystem(World world,
            World globalWorld,
            Entity rootEntity,
            ISceneData sceneData,
            IPartitionComponent scenePartition,
            ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.globalWorld = globalWorld;
            this.rootEntity = rootEntity;
            this.sceneData = sceneData;
            this.scenePartition = scenePartition;
            this.sceneStateProvider = sceneStateProvider;

            globalSkyboxEntity = new SingleInstanceEntity(GLOBAL_SKYBOX_QUERY, globalWorld);
        }

        public override void Initialize()
        {
            World.Add(rootEntity, new SceneSkyboxComponent());
        }

        public void OnSceneIsCurrentChanged(bool value)
        {
            // Nothing to publish or clear before Initialize has added the state component
            if (!World.Has<SceneSkyboxComponent>(rootEntity)) return;

            if (value)
            {
                Sync(skipDirtyCheck: true);
                return;
            }

            // The texture stays loaded for an instant re-apply
            ClearOverridesIfOwner();
        }

        public void FinalizeComponents(in Query query)
        {
            ClearOverridesIfOwner();

            ref SceneSkyboxComponent component = ref World.TryGetRef<SceneSkyboxComponent>(rootEntity, out bool hasComponent);

            if (!hasComponent) return;

            ReleaseAll(ref component);
        }

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            Sync(skipDirtyCheck: false);
        }

        private void Sync(bool skipDirtyCheck)
        {
            PBSkybox? pbSkybox = GetRootComponent<PBSkybox>();
            PBSun? pbSun = GetRootComponent<PBSun>();
            PBEnvironmentFog? pbFog = GetRootComponent<PBEnvironmentFog>();
            ref SceneSkyboxComponent component = ref World.Get<SceneSkyboxComponent>(rootEntity);

            EnvironmentSources sources = ToSources(pbSkybox, pbSun, pbFog);

            if (sources == EnvironmentSources.None)
            {
                ReleaseAll(ref component);
                ClearOverridesIfOwner();
                return;
            }

            bool sourcesChanged = sources != component.Sources;
            component.Sources = sources;

            bool rebuildAll = skipDirtyCheck || sourcesChanged;
            bool skyboxDirty = rebuildAll || pbSkybox is { IsDirty: true };
            bool sunDirty = rebuildAll || pbSun is { IsDirty: true };

            // A PBSun update that only moves the sun keeps the profile and its gradients
            bool lookDirty = skyboxDirty || pbFog is { IsDirty: true } || (sunDirty && SunLookChanged(pbSun, in component));
            var changed = false;

            if (skyboxDirty)
                changed |= UpdateSlot(pbSkybox is { HasSkyboxTexture: true } ? pbSkybox.SkyboxTexture : null, ref component.SkyboxTexture);

            if (lookDirty)
                changed |= UpdateEnvironment(pbSkybox, pbSun, pbFog, ref component);

            if (sunDirty)
                changed |= UpdateSun(pbSun, ref component);

            changed |= ResolveSlot(ref component.SkyboxTexture);

            if (changed || skipDirtyCheck)
                PushOverrides(in component);
        }

        private static EnvironmentSources ToSources(PBSkybox? pbSkybox, PBSun? pbSun, PBEnvironmentFog? pbFog)
        {
            EnvironmentSources sources = EnvironmentSources.None;

            if (pbSkybox != null) sources |= EnvironmentSources.Skybox;
            if (pbSun != null) sources |= EnvironmentSources.Sun;
            if (pbFog != null) sources |= EnvironmentSources.Fog;

            return sources;
        }

        private T? GetRootComponent<T>() where T : class =>
            World.TryGet(rootEntity, out T? component) ? component : null;

        private static bool? SunVisible(PBSun? pbSun) =>
            pbSun is { HasVisible: true } ? pbSun.Visible : null;

        private static bool SunLookChanged(PBSun? pbSun, in SceneSkyboxComponent component) =>
            SunVisible(pbSun) != component.SunVisible || !Equals(pbSun?.Color, component.SunColor);

        /// <summary>
        ///     Starts loading the requested texture source when it differs from the one the slot already tracks.
        /// </summary>
        /// <returns>True when a texture that was published got released.</returns>
        private bool UpdateSlot(string? src, ref TextureSlot slot)
        {
            if (string.IsNullOrEmpty(src))
                return Release(ref slot);

            if (!sceneData.TryGetMediaUrl(src, out URLAddress url))
            {
                ReportHub.LogWarning(GetReportData(), $"{nameof(PBSkybox)} texture {src} could not be resolved, keeping the default environment");
                return Release(ref slot);
            }

            sceneData.TryGetMediaFileHash(src, out string fileHash);

            // Same source as the loading or loaded texture
            if (IsSameSource(in slot.LoadingIntention, url, fileHash))
                return false;

            bool released = Release(ref slot);

            var intention = new GetTextureIntention(url, fileHash, SKY_WRAP_MODE, SKY_FILTER_MODE, TextureType.Albedo,
                nameof(SceneSkyboxHandlerSystem), attemptsCount: GET_TEXTURE_MAX_ATTEMPT_COUNT);

            slot.LoadingIntention = intention;
            slot.LoadingPromise = TexturePromise.Create(World, intention, scenePartition);

            return released;
        }

        private static bool IsSameSource(in GetTextureIntention intention, string url, string fileHash) =>
            intention.Src == url
            && intention.FileHash == fileHash
            && intention.WrapMode == SKY_WRAP_MODE
            && intention.FilterMode == SKY_FILTER_MODE;

        /// <returns>True when a loaded texture was resolved into the slot.</returns>
        private bool ResolveSlot(ref TextureSlot slot)
        {
            if (slot.LoadingPromise == null)
                return false;

            TexturePromise promise = slot.LoadingPromise.Value;

            if (!promise.TryConsume(World, out StreamableLoadingResult<TextureData> result))
                return false;

            // The intention stays so the same source is not requested again
            slot.LoadingPromise = null;

            if (!result.Succeeded)
            {
                result.TryLogException();
                return false;
            }

            slot.TextureData = result.Asset;
            return true;
        }

        /// <returns>True when a loaded texture was released.</returns>
        private bool Release(ref TextureSlot slot)
        {
            bool hadTexture = slot.TextureData != null;
            slot.CleanUp(World);
            return hadTexture;
        }

        private void ReleaseAll(ref SceneSkyboxComponent component)
        {
            component.SkyboxTexture.CleanUp(World);
            component.Environment = null;
            component.Sun = SceneSunOverride.NONE;
            component.SunColor = null;
            component.SunVisible = null;
            component.Sources = EnvironmentSources.None;
        }

        /// <summary>
        ///     Rebuilds the look profile from the components, also on the re-push that skips the dirty check:
        ///     the dirty flags are reset every frame while the scene is not current, so a profile cached before leaving
        ///     could miss changes made in the meantime. Keeps a copy of the PBSun look fields the profile was built from.
        /// </summary>
        /// <returns>True when the profile changed.</returns>
        private static bool UpdateEnvironment(PBSkybox? pbSkybox, PBSun? pbSun, PBEnvironmentFog? pbFog, ref SceneSkyboxComponent component)
        {
            component.SunVisible = SunVisible(pbSun);

            if (!Equals(pbSun?.Color, component.SunColor))
                component.SunColor = pbSun?.Color?.Clone();

            SceneEnvironmentProfile? environment = SceneEnvironmentProfile.FromProto(pbSkybox, pbSun, pbFog);

            if (environment == null && component.Environment == null)
                return false;

            component.Environment = environment;
            return true;
        }

        /// <returns>True when the sun override changed.</returns>
        private static bool UpdateSun(PBSun? pbSun, ref SceneSkyboxComponent component)
        {
            SceneSunOverride sun = SceneSunOverride.FromProto(pbSun);

            if (sun.Equals(component.Sun))
                return false;

            component.Sun = sun;
            return true;
        }

        private void PushOverrides(in SceneSkyboxComponent component)
        {
            ref SceneSkyboxOverrides overrides = ref globalWorld.Get<SceneSkyboxOverrides>(globalSkyboxEntity);
            overrides.SkyboxTexture = component.SkyboxTexture.TextureData?.Asset.Texture;
            overrides.Environment = component.Environment;
            overrides.Sun = component.Sun;
            overrides.Owner = sceneInfo;
        }

        private void ClearOverridesIfOwner()
        {
            ref SceneSkyboxOverrides overrides = ref globalWorld.Get<SceneSkyboxOverrides>(globalSkyboxEntity);

            if (overrides.Owner is not { } owner || !owner.Equals(sceneInfo)) return;

            overrides.SkyboxTexture = null;
            overrides.Environment = null;
            overrides.Sun = SceneSunOverride.NONE;
            overrides.Owner = null;
        }
    }
}
