using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Utils;
using DCL.SkyBox;
using DCL.SkyBox.Components;
using DCL.WebRequests;
using Decentraland.Common;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Textures.Components;
using ECS.Unity.Textures.Components.Extensions;
using SceneRunner.Scene;
using Entity = Arch.Core.Entity;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;
using TextureSlot = DCL.SDKComponents.Skybox.SceneSkyboxComponent.TextureSlot;

namespace DCL.SDKComponents.Skybox.Systems
{
    /// <summary>
    ///     Loads the textures of the PBSkybox on the scene root entity, builds its environment profile and, while the scene
    ///     is current, publishes them to the global world as the scene environment overrides.
    /// </summary>
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.SKYBOX)]
    public partial class SceneSkyboxHandlerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener, IFinalizeWorldSystem
    {
        private const int GET_TEXTURE_MAX_ATTEMPT_COUNT = 6;

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
            if (value)
                Sync(skipDirtyCheck: true);
            else
                ClearOverridesIfOwner();
        }

        public void FinalizeComponents(in Query query)
        {
            ClearOverridesIfOwner();

            ref SceneSkyboxComponent component = ref World.TryGetRef<SceneSkyboxComponent>(rootEntity, out bool hasComponent);

            if (!hasComponent) return;

            component.ReflectionMap.CleanUp(World);
            component.SkyboxTexture.CleanUp(World);
            component.Environment = null;
        }

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            Sync(skipDirtyCheck: false);
        }

        private void Sync(bool skipDirtyCheck)
        {
            ref PBSkybox pbSkybox = ref World.TryGetRef<PBSkybox>(rootEntity, out bool hasPbSkybox);
            ref SceneSkyboxComponent component = ref World.Get<SceneSkyboxComponent>(rootEntity);

            if (!hasPbSkybox)
            {
                Release(ref component.ReflectionMap);
                Release(ref component.SkyboxTexture);
                component.Environment = null;
                ClearOverridesIfOwner();
                return;
            }

            var changed = false;

            if (skipDirtyCheck || pbSkybox.IsDirty)
            {
                changed |= UpdateSlot(pbSkybox.ReflectionMap, ref component.ReflectionMap);
                changed |= UpdateSlot(pbSkybox.SkyboxTexture, ref component.SkyboxTexture);
                changed |= UpdateEnvironment(pbSkybox, ref component);
            }

            changed |= ResolveSlot(ref component.ReflectionMap);
            changed |= ResolveSlot(ref component.SkyboxTexture);

            if (changed || skipDirtyCheck)
                PushOverrides(in component);
        }

        /// <summary>
        ///     Starts loading the requested texture when it differs from the one the slot already tracks.
        /// </summary>
        /// <returns>True when a texture that was published got released.</returns>
        private bool UpdateSlot(TextureUnion? texture, ref TextureSlot slot)
        {
            if (texture == null)
                return Release(ref slot);

            if (texture.TexCase != TextureUnion.TexOneofCase.Texture)
            {
                ReportHub.LogWarning(GetReportData(), $"{nameof(PBSkybox)} supports file textures only, ignoring {texture.TexCase}");
                return Release(ref slot);
            }

            TextureComponent? textureComponent = texture.CreateTextureComponent(sceneData);

            if (textureComponent == null)
                return Release(ref slot);

            TextureComponent requested = textureComponent.Value;

            // Same source as the loading or loaded texture
            if (TextureComponentUtils.Equals(in requested, in slot.LoadingIntention))
                return false;

            bool released = Release(ref slot);

            var intention = new GetTextureIntention(requested.Src, requested.FileHash, requested.WrapMode, requested.FilterMode, TextureType.Albedo,
                nameof(SceneSkyboxHandlerSystem), attemptsCount: GET_TEXTURE_MAX_ATTEMPT_COUNT);

            slot.LoadingIntention = intention;
            slot.LoadingPromise = TexturePromise.Create(World, intention, scenePartition);

            return released;
        }

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
                ReportHub.LogWarning(GetReportData(), $"{nameof(PBSkybox)} texture {promise.LoadingIntention.Src} failed to load, keeping the default environment");
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

        /// <summary>
        ///     Rebuilds the environment profile from the component data, also on the re-push that skips the dirty check:
        ///     the dirty flag is reset every frame while the scene is not current, so a profile cached before leaving
        ///     could miss changes made in the meantime.
        /// </summary>
        /// <returns>True when the profile changed.</returns>
        private static bool UpdateEnvironment(PBSkybox pbSkybox, ref SceneSkyboxComponent component)
        {
            SceneEnvironmentProfile? environment = SceneEnvironmentProfile.FromProto(pbSkybox);

            if (environment == null && component.Environment == null)
                return false;

            component.Environment = environment;
            return true;
        }

        private void PushOverrides(in SceneSkyboxComponent component)
        {
            ref SceneSkyboxOverrides overrides = ref globalWorld.Get<SceneSkyboxOverrides>(globalSkyboxEntity);
            overrides.ReflectionMap = component.ReflectionMap.TextureData?.EnsureTexture2D();
            overrides.SkyboxTexture = component.SkyboxTexture.TextureData?.EnsureTexture2D();
            overrides.Environment = component.Environment;
            overrides.Owner = sceneInfo;
        }

        private void ClearOverridesIfOwner()
        {
            ref SceneSkyboxOverrides overrides = ref globalWorld.Get<SceneSkyboxOverrides>(globalSkyboxEntity);

            if (overrides.Owner is not { } owner || !owner.Equals(sceneInfo)) return;

            overrides.ReflectionMap = null;
            overrides.SkyboxTexture = null;
            overrides.Environment = null;
            overrides.Owner = null;
        }
    }
}
