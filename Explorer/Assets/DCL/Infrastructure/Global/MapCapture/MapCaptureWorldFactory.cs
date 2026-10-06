using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using DCL.GlobalPartitioning;
using DCL.Ipfs;
using DCL.LOD.Systems;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem.Global;
using DCL.Rendering.GPUInstancing;
using DCL.Roads.Systems;
using DCL.Time.Systems;
using DCL.WebRequests;
using ECS;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Systems;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace Global.MapCapture
{
    /// <summary>
    ///     The capture world: scene definition and ISS descriptor loading, deferred asset loading, the plugins'
    ///     own systems (asset bundles, LOD, roads, landscape, skybox) and the unload path. No partitioning,
    ///     pointer streaming, scene runtime or comms.
    /// </summary>
    public static class MapCaptureWorldFactory
    {
        private const string GLOBAL_SCENE_NAME = "global";
        private const string ISS_DISK_CACHE_EXTENSION = "iss.json";

        public static SystemGroupWorld Create(World world, StaticContainer staticContainer, IDecentralandUrlsSource urls, IRealmData realmData,
            EntitiesAnalytics entitiesAnalytics, LODContainer lodContainer, IReadOnlyList<IDCLGlobalPlugin> plugins, Entity playerEntity,
            MapCaptureSceneFeeder feeder, Camera camera)
        {
            ISceneStateProvider stateProvider = new SceneStateProvider();
            stateProvider.State.Set(SceneState.Running);

            world.Create(new SceneShortInfo(Vector2Int.zero, GLOBAL_SCENE_NAME));

            var builder = new ArchSystemsWorldBuilder<World>(world);

            builder.InjectCustomGroup(new SyncedPresentationSystemGroup(stateProvider))
                   .InjectCustomGroup(new SyncedPreRenderingSystemGroup(stateProvider));

            IWebRequestController webRequests = staticContainer.WebRequestsContainer.WebRequestController;

            LoadSceneDefinitionListSystem.InjectToWorld(ref builder, webRequests, false, false, NoCache<SceneDefinitions, GetSceneDefinitionList>.INSTANCE, entitiesAnalytics);

            // A world the registry has no manifest for resolves its scenes one URN at a time, as the client's fixed pointer loader does.
            LoadSceneDefinitionSystem.InjectToWorld(ref builder, webRequests, false, false, NoCache<SceneEntityDefinition, GetSceneDefinition>.INSTANCE);

            LoadISSDescriptorSystem.InjectToWorld(ref builder, webRequests, URLDomain.FromString(urls.Url(DecentralandUrl.LodGeneratorCDN)),
                new NoCache<ISSDescriptorMetadata, GetISSDescriptorIntention>(false, false),
                new DiskCacheOptions<ISSDescriptorMetadata, GetISSDescriptorIntention>(staticContainer.ISSDescriptorDiskCache, new GetISSDescriptorIntention.DiskHashCompute(urls), ISS_DISK_CACHE_EXTENSION));

            ResolveISSDescriptorSystem.InjectToWorld(ref builder);

            var sceneBudget = new ConcurrentLoadingPerformanceBudget(staticContainer.StaticSettings.ScenesLoadingBudget);
            GlobalDeferredLoadingSystem.InjectToWorld(ref builder, sceneBudget, staticContainer.SingletonSharedDependencies.MemoryBudget, staticContainer.ScenesCache, playerEntity);

            DestroyEntitiesSystem.InjectToWorld(ref builder);
            UpdateTimeSystem.InjectToWorld(ref builder);
            MapCaptureFeedSystem.InjectToWorld(ref builder, feeder);
            MapCaptureBrainUpdateSystem.InjectToWorld(ref builder);

            if (staticContainer.GPUInstancingService != null)
                MapCaptureGpuInstancingSystem.InjectToWorld(ref builder, staticContainer.GPUInstancingService, realmData, camera);

            var pluginArguments = new GlobalPluginArguments(playerEntity, world.Create());

            foreach (IDCLGlobalPlugin plugin in plugins)
                plugin.InjectToWorld(ref builder, pluginArguments);

            UnloadSceneLODSystem.InjectToWorld(ref builder, staticContainer.ScenesCache, lodContainer.LodCache, staticContainer.RealmPartitionSettings);
            UnloadRoadSystem.InjectToWorld(ref builder, lodContainer.RoadAssetsPool, staticContainer.ScenesCache);

            SystemGroupWorld systems = builder.Finish();
            systems.Initialize();
            return systems;
        }
    }

    /// <summary>Drives the feeder from the realm group, where the pointer loaders it replaces run.</summary>
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class MapCaptureFeedSystem : BaseUnityLoopSystem
    {
        private readonly MapCaptureSceneFeeder feeder;

        internal MapCaptureFeedSystem(World world, MapCaptureSceneFeeder feeder) : base(world)
        {
            this.feeder = feeder;
        }

        protected override void Update(float t) =>
            feeder.Update(World);
    }

    /// <summary>
    ///     The rig's Cinemachine brain runs in manual update mode and is normally ticked by the character camera
    ///     plugin; without this tick the output camera never leaves its prefab pose.
    /// </summary>
    [UpdateInGroup(typeof(CameraGroup))]
    public partial class MapCaptureBrainUpdateSystem : BaseUnityLoopSystem
    {
        internal MapCaptureBrainUpdateSystem(World world) : base(world) { }

        protected override void Update(float t) =>
            ManualBrainUpdateQuery(World);

        [Query]
        private void ManualBrainUpdate(ref ICinemachinePreset cinemachinePreset) =>
            cinemachinePreset.Brain.ManualUpdate();
    }

    /// <summary>
    ///     GPU-instanced roads and foliage draw only to the camera registered with the service; the client
    ///     registers the Cinemachine output camera once loading completes, this registers it up front.
    /// </summary>
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    public partial class MapCaptureGpuInstancingSystem : BaseUnityLoopSystem
    {
        private readonly GPUInstancingService gpuInstancingService;
        private readonly IRealmData realmData;

        internal MapCaptureGpuInstancingSystem(World world, GPUInstancingService gpuInstancingService, IRealmData realmData, Camera camera) : base(world)
        {
            this.gpuInstancingService = gpuInstancingService;
            this.realmData = realmData;
            gpuInstancingService.SetCamera(camera);
        }

        protected override void Update(float t)
        {
            if (gpuInstancingService.IsEnabled && realmData.Configured)
                gpuInstancingService.RenderIndirect();
        }
    }
}
