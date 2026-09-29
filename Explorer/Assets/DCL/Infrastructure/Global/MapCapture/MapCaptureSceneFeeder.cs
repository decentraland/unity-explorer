using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Roads.Components;
using DCL.SceneRunner.Scene;
using ECS;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace Global.MapCapture
{
    /// <summary>Marks the entities the feeder created, so a chunk unload touches nothing else.</summary>
    public struct MapCaptureSceneTag { }

    /// <summary>
    ///     Feeds scene definition entities for explicit parcel lists instead of the player-centred radius streaming.
    ///     Roads and LOD scenes get the same components the pointer loader would give them, so the existing road
    ///     and ISS LOD_0 systems load them unchanged; readiness is answered per parcel.
    /// </summary>
    public class MapCaptureSceneFeeder
    {
        private readonly IDecentralandUrlsSource urls;
        private readonly IScenesCache scenesCache;
        private readonly IRealmData realmData;
        private readonly HashSet<Vector2Int> roadCoordinates;
        private readonly int batchSize;

        private readonly Queue<List<int2>> pendingRequests = new ();
        private readonly List<SceneEntityDefinition> definitionsBuffer = new ();
        private readonly Dictionary<Vector2Int, Entity> entitiesByParcel = new ();
        private readonly HashSet<string> sceneIds = new ();
        private readonly HashSet<Vector2Int> emptyParcels = new ();
        private readonly List<Entity> awaitingDescriptor = new ();
        private readonly List<Entity> awaitingLod = new ();
        private readonly HashSet<Entity> withoutDescriptor = new ();
        private readonly HashSet<Entity> partiallyAssembled = new ();
        private int requestCounter;

        private AssetPromise<SceneDefinitions, GetSceneDefinitionList>? activePromise;

        public bool HasRequestsInFlight => activePromise.HasValue || pendingRequests.Count > 0;

        public MapCaptureSceneFeeder(IDecentralandUrlsSource urls, IScenesCache scenesCache, IRealmData realmData, HashSet<Vector2Int> roadCoordinates, int batchSize)
        {
            this.urls = urls;
            this.scenesCache = scenesCache;
            this.realmData = realmData;
            this.roadCoordinates = roadCoordinates;
            this.batchSize = Mathf.Max(1, batchSize);
        }

        public void Request(IReadOnlyList<Vector2Int> parcels)
        {
            var batch = new List<int2>(batchSize);
            var skipped = 0;
            int batchesBefore = pendingRequests.Count;

            foreach (Vector2Int parcel in parcels)
            {
                if (IsCovered(parcel))
                {
                    skipped++;
                    continue;
                }

                batch.Add(parcel.ToInt2());

                if (batch.Count < batchSize) continue;

                pendingRequests.Enqueue(batch);
                batch = new List<int2>(batchSize);
            }

            if (batch.Count > 0)
                pendingRequests.Enqueue(batch);

            Debug.Log($"[JUANI] Request for {parcels.Count} parcels from ({parcels[0].x},{parcels[0].y}): {pendingRequests.Count - batchesBefore} registry batches queued, {skipped} parcels already covered or known empty");
        }

        /// <summary>Main thread, once per frame from <see cref="MapCaptureFeedSystem" />.</summary>
        public void Update(World world)
        {
            ConsumeActiveRequest(world);
            StartNextRequest(world);
            AttachLodInfo(world);
            LogLodOutcomes(world);
        }

        /// <summary>Why a parcel is not ready yet, for the timeout report.</summary>
        public string DescribeParcel(World world, Vector2Int parcel)
        {
            if (!entitiesByParcel.TryGetValue(parcel, out Entity entity))
                return "no scene entity: the registry never answered for it";

            if (!world.IsAlive(entity))
                return "scene entity destroyed";

            string id = world.Get<SceneDefinitionComponent>(entity).Definition.id;

            if (world.Has<RoadInfo>(entity))
                return $"road {id} not instantiated";

            if (withoutDescriptor.Contains(entity))
                return $"scene {id}: no ISS descriptor";

            ISSDescriptorState descriptorState = world.Get<ISSDescriptor>(entity).CurrentState;

            if (!world.TryGet(entity, out SceneLODInfo lodInfo))
                return $"scene {id}: descriptor {descriptorState}, no LOD info yet";

            if (!lodInfo.IsInitialized())
                return $"scene {id}: descriptor {descriptorState}, LOD info not initialized";

            return $"scene {id}: descriptor {descriptorState}, LOD_0 loading (promise level {lodInfo.CurrentLODLevelPromise}, ISS state {lodInfo.InitialSceneStateLOD.CurrentState})";
        }

        public bool IsParcelReady(World world, Vector2Int parcel, out bool failed)
        {
            failed = false;

            if (emptyParcels.Contains(parcel) || realmData.WorldManifest.IsParcelKnownEmpty(parcel.x, parcel.y))
                return true;

            if (!entitiesByParcel.TryGetValue(parcel, out Entity entity) || !world.IsAlive(entity))
                return false;

            if (world.Has<RoadInfo>(entity))
                return scenesCache.ContainsNonRealScene(parcel);

            if (withoutDescriptor.Contains(entity))
            {
                failed = true;
                return true;
            }

            if (!world.TryGet(entity, out SceneLODInfo lodInfo) || !lodInfo.IsLODInstantiated(0))
                return false;

            failed = SceneLODInfoUtils.HasLODResult(lodInfo.metadata.FailedLODs, 0) || partiallyAssembled.Contains(entity);
            return true;
        }

        public void UnloadAll(World world)
        {
            foreach (Entity entity in awaitingDescriptor)
                if (world.IsAlive(entity))
                    world.Get<AssetPromise<ISSDescriptorMetadata, GetISSDescriptorIntention>>(entity).ForgetLoading(world);

            awaitingDescriptor.Clear();
            awaitingLod.Clear();
            withoutDescriptor.Clear();
            partiallyAssembled.Clear();

            Debug.Log($"[JUANI] Unloading {sceneIds.Count} scene entities");

            foreach (Entity entity in new HashSet<Entity>(entitiesByParcel.Values))
                if (world.IsAlive(entity) && !world.Has<DeleteEntityIntention>(entity))
                    world.Add(entity, new DeleteEntityIntention());

            entitiesByParcel.Clear();
            sceneIds.Clear();
            emptyParcels.Clear();
            pendingRequests.Clear();

            if (!activePromise.HasValue) return;

            activePromise.Value.ForgetLoading(world);
            activePromise = null;
        }

        private bool IsCovered(Vector2Int parcel) =>
            entitiesByParcel.ContainsKey(parcel) || emptyParcels.Contains(parcel) || realmData.WorldManifest.IsParcelKnownEmpty(parcel.x, parcel.y);

        private void StartNextRequest(World world)
        {
            if (activePromise.HasValue || pendingRequests.Count == 0) return;

            List<int2> pointers = pendingRequests.Dequeue();
            definitionsBuffer.Clear();

            string url = urls.Url(DecentralandUrl.EntitiesActive);

            activePromise = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(world,
                new GetSceneDefinitionList(definitionsBuffer, pointers, new CommonLoadingArguments(url)),
                PartitionComponent.TOP_PRIORITY);

            requestCounter++;
            Debug.Log($"[JUANI] Registry request #{requestCounter}: {pointers.Count} pointers starting at ({pointers[0].x},{pointers[0].y}) -> {url}");
        }

        private void ConsumeActiveRequest(World world)
        {
            if (!activePromise.HasValue) return;

            AssetPromise<SceneDefinitions, GetSceneDefinitionList> promise = activePromise.Value;

            if (!promise.TryConsume(world, out StreamableLoadingResult<SceneDefinitions> result)) return;

            activePromise = null;
            int entitiesBefore = sceneIds.Count;

            if (result.Succeeded)
            {
                foreach (SceneEntityDefinition definition in result.Asset.Value)
                    CreateSceneEntity(world, definition);
            }
            else
                Debug.LogWarning($"[JUANI] Registry request #{requestCounter} FAILED: {result.Exception}");

            // Requested parcels no definition claimed hold no scene (or could not be resolved): nothing to wait for there.
            var newlyEmpty = 0;

            foreach (int2 pointer in promise.LoadingIntention.Pointers)
            {
                Vector2Int parcel = pointer.ToVector2Int();

                if (entitiesByParcel.ContainsKey(parcel)) continue;

                emptyParcels.Add(parcel);
                newlyEmpty++;
            }

            Debug.Log($"[JUANI] Registry request #{requestCounter} answered: {(result.Succeeded ? result.Asset.Value.Count : 0)} definitions, {sceneIds.Count - entitiesBefore} new scene entities, {newlyEmpty} of {promise.LoadingIntention.Pointers.Count} requested parcels have no scene");
        }

        private void CreateSceneEntity(World world, SceneEntityDefinition definition)
        {
            if (definition.pointers.Length == 0 || !sceneIds.Add(definition.id)) return;

            var ipfsPath = new IpfsPath(definition.id, URLDomain.FromString(urls.Url(DecentralandUrl.Content)));
            SceneDefinitionComponent component = SceneDefinitionComponentFactory.CreateFromDefinition(definition, ipfsPath, false, limitHeightByParcels: true);

            var partition = new PartitionComponent { Bucket = 0, IsBehind = false, RawSqrDistance = 0f, OutOfRange = false, IsDirty = true };
            ISSDescriptor descriptor = ISSDescriptor.CreateUninitialized();
            Entity entity;
            bool isRoad = roadCoordinates.Contains(definition.metadata.scene.DecodedBase);

            if (isRoad)
                entity = world.Create(component, descriptor, RoadInfo.Create(), SceneLoadingState.CreateRoad(), partition, new MapCaptureSceneTag());
            else
            {
                entity = world.Create(component, descriptor, SceneLoadingState.CreateHighQualityLOD(), partition, new MapCaptureSceneTag(),
                    AssetPromise<ISSDescriptorMetadata, GetISSDescriptorIntention>.Create(world, GetISSDescriptorIntention.For(definition), partition));

                awaitingDescriptor.Add(entity);
            }

            Vector2Int basePosition = definition.metadata.scene.DecodedBase;
            string abVersion = definition.assetBundleManifestVersion?.GetAssetBundleManifestVersion() ?? "none";
            Debug.Log($"[JUANI] Scene {definition.id} base ({basePosition.x},{basePosition.y}) {component.Parcels.Count} parcels sdk7={component.IsSDK7} ab={abVersion} -> {(isRoad ? "road" : "ISS LOD_0")}");

            foreach (Vector2Int parcel in component.Parcels)
                entitiesByParcel[parcel] = entity;
        }

        private void AttachLodInfo(World world)
        {
            for (int i = awaitingDescriptor.Count - 1; i >= 0; i--)
            {
                Entity entity = awaitingDescriptor[i];

                if (!world.IsAlive(entity))
                {
                    awaitingDescriptor.RemoveAt(i);
                    continue;
                }

                // UpdateSceneLODInfoSystem reads the descriptor the moment SceneLODInfo appears; attaching it
                // before the descriptor resolves would send the scene down the legacy LOD path.
                ISSDescriptor descriptor = world.Get<ISSDescriptor>(entity);

                if (descriptor.CurrentState == ISSDescriptorState.Uninitialized) continue;

                string id = world.Get<SceneDefinitionComponent>(entity).Definition.id;
                awaitingDescriptor.RemoveAt(i);

                // Without a descriptor the LOD systems would fall back to a legacy LOD_0 bundle, which abgen never
                // publishes; that is a failed scene, and the reason is in the "ISSDescriptor is unavailable" log line.
                if (descriptor.CurrentState != ISSDescriptorState.Descriptor)
                {
                    Debug.LogWarning($"[JUANI] Descriptor for {id}: NONE -> scene marked failed, no ISS LOD_0 to assemble");
                    withoutDescriptor.Add(entity);
                    continue;
                }

                Debug.Log($"[JUANI] Descriptor for {id}: {descriptor.Assets.Count} assets -> assembling ISS LOD_0");
                world.Add(entity, SceneLODInfo.Create());
                awaitingLod.Add(entity);
            }
        }

        private void LogLodOutcomes(World world)
        {
            for (int i = awaitingLod.Count - 1; i >= 0; i--)
            {
                Entity entity = awaitingLod[i];

                if (!world.IsAlive(entity))
                {
                    awaitingLod.RemoveAt(i);
                    continue;
                }

                if (!world.TryGet(entity, out SceneLODInfo lodInfo) || !lodInfo.IsLODInstantiated(0)) continue;

                string id = world.Get<SceneDefinitionComponent>(entity).Definition.id;
                int total = lodInfo.InitialSceneStateLOD.TotalAssetsToInstantiate;
                int failedAssets = lodInfo.InitialSceneStateLOD.FailedAssetCount();

                if (SceneLODInfoUtils.HasLODResult(lodInfo.metadata.FailedLODs, 0))
                    Debug.LogWarning($"[JUANI] LOD_0 FAILED for {id}");
                else if (failedAssets > 0)
                {
                    Debug.LogWarning($"[JUANI] LOD_0 ready for {id} with {failedAssets} of {total} assets FAILED to load");
                    partiallyAssembled.Add(entity);
                }
                else
                    Debug.Log($"[JUANI] LOD_0 ready for {id}: {total} assets in place");

                // The client wraps each scene in a LODGroup whose thresholds assume a perspective camera at ground
                // level. Seen from an orthographic camera over a whole chunk most scenes fall below the LOD_0
                // threshold and switch to LOD_1, which this capture never loads, so they vanish. Pin LOD_0.
                lodInfo.metadata.LodGroup.ForceLOD(0);

                awaitingLod.RemoveAt(i);
            }
        }
    }
}
