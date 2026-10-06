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
        private const int WORLD_POINTERS_BATCH = 1000;

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
        private readonly List<string> failedDescriptors = new ();
        private int requestCounter;

        // A world's scene definitions, resolved once per world the way the client's fixed pointer loader does.
        private readonly Queue<List<int2>> worldPointerBatches = new ();
        private readonly List<AssetPromise<SceneEntityDefinition, GetSceneDefinition>> worldUrnPromises = new ();
        private readonly List<SceneEntityDefinition> worldDefinitions = new ();
        private readonly HashSet<string> worldDefinitionIds = new ();
        private Dictionary<Vector2Int, SceneEntityDefinition>? worldDefinitionsByParcel;
        private AssetPromise<SceneDefinitions, GetSceneDefinitionList>? activeWorldBatch;
        private bool worldDefinitionsRequested;
        private bool resolvingWorldDefinitions;

        private AssetPromise<SceneDefinitions, GetSceneDefinitionList>? activePromise;

        public bool HasRequestsInFlight => activePromise.HasValue || pendingRequests.Count > 0;

        public bool HasSceneEntities => entitiesByParcel.Count > 0;

        /// <summary>Every scene definition of the current world has been answered, successfully or not.</summary>
        public bool WorldDefinitionsResolved => worldDefinitionsByParcel != null;

        public IReadOnlyList<SceneEntityDefinition> WorldDefinitions => worldDefinitions;

        /// <summary>Definition requests of the current world that failed; its scenes behind them are unknown.</summary>
        public int WorldDefinitionFailures { get; private set; }

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

        /// <summary>
        ///     Resolves every scene definition of the configured world, as the client does on entering it: with a world
        ///     manifest, the registry's world entities for its occupied parcels; without one, each of the realm's scene
        ///     URNs from the worlds content server. Once resolved, requests are answered from them.
        /// </summary>
        public void RequestWorldDefinitions()
        {
            ClearWorldDefinitionsState();
            worldDefinitionsRequested = true;
        }

        public void ClearWorldDefinitions(World world)
        {
            activeWorldBatch?.ForgetLoading(world);

            foreach (AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise in worldUrnPromises)
                promise.ForgetLoading(world);

            ClearWorldDefinitionsState();
        }

        /// <summary>Main thread, once per frame from <see cref="MapCaptureFeedSystem" />.</summary>
        public void Update(World world)
        {
            StartWorldDefinitions(world);
            ResolveWorldDefinitions(world);
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

            if (failedDescriptors.Count > 0)
                ReportHub.LogProductionInfo($"[MapCapture] {failedDescriptors.Count} ISS descriptors failed in this load:\n{string.Join("\n", failedDescriptors)}");

            failedDescriptors.Clear();

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

            if (worldDefinitionsByParcel != null)
            {
                CreateFromWorldDefinitions(world, worldDefinitionsByParcel);
                return;
            }

            List<int2> pointers = pendingRequests.Dequeue();
            definitionsBuffer.Clear();

            string url = realmData.IsGenesis() ? urls.Url(DecentralandUrl.EntitiesActive) : WorldEntitiesUrl();

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

            // As the client's pointer loaders: world content lives on the worlds content server, a world's scenes are
            // not height-limited by their parcels, and only Genesis City has roads.
            bool genesis = realmData.IsGenesis();
            var ipfsPath = new IpfsPath(definition.id, URLDomain.FromString(urls.Url(genesis ? DecentralandUrl.Content : DecentralandUrl.WorldContentServer)));
            SceneDefinitionComponent component = SceneDefinitionComponentFactory.CreateFromDefinition(definition, ipfsPath, false, limitHeightByParcels: genesis);

            var partition = new PartitionComponent { Bucket = 0, IsBehind = false, RawSqrDistance = 0f, OutOfRange = false, IsDirty = true };
            ISSDescriptor descriptor = ISSDescriptor.CreateUninitialized();
            Entity entity;
            bool isRoad = genesis && roadCoordinates.Contains(definition.metadata.scene.DecodedBase);

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

        private string WorldEntitiesUrl() =>
            string.Format(urls.Url(DecentralandUrl.WorldEntitiesActive), realmData.RealmName);

        private void StartWorldDefinitions(World world)
        {
            if (!worldDefinitionsRequested) return;

            worldDefinitionsRequested = false;
            resolvingWorldDefinitions = true;

            if (!realmData.WorldManifest.IsEmpty)
            {
                // The client posts every occupied parcel at once; a 300x300 world is split so no single request is huge.
                var batch = new List<int2>(WORLD_POINTERS_BATCH);

                foreach (int2 parcel in realmData.WorldManifest.GetOccupiedParcels())
                {
                    batch.Add(parcel);

                    if (batch.Count < WORLD_POINTERS_BATCH) continue;

                    worldPointerBatches.Enqueue(batch);
                    batch = new List<int2>(WORLD_POINTERS_BATCH);
                }

                if (batch.Count > 0)
                    worldPointerBatches.Enqueue(batch);

                Debug.Log($"[JUANI] World {realmData.RealmName}: manifest with {realmData.WorldManifest.GetOccupiedParcels().Count} occupied parcels, {worldPointerBatches.Count} registry batches -> {WorldEntitiesUrl()}");
                return;
            }

            URLDomain contentServer = URLDomain.FromString(urls.Url(DecentralandUrl.WorldContentServer));

            foreach (string urn in realmData.Ipfs.SceneUrns)
            {
                IpfsPath ipfsPath = IpfsHelper.ParseUrn(urn);

                worldUrnPromises.Add(AssetPromise<SceneEntityDefinition, GetSceneDefinition>.Create(world,
                    new GetSceneDefinition(new CommonLoadingArguments(ipfsPath.GetUrl(contentServer)), ipfsPath), PartitionComponent.TOP_PRIORITY));
            }

            Debug.Log($"[JUANI] World {realmData.RealmName}: no manifest, {worldUrnPromises.Count} scene URNs from the worlds content server");
        }

        private void ResolveWorldDefinitions(World world)
        {
            if (!resolvingWorldDefinitions) return;

            if (activeWorldBatch.HasValue && activeWorldBatch.Value.TryConsume(world, out StreamableLoadingResult<SceneDefinitions> batchResult))
            {
                activeWorldBatch = null;

                if (batchResult.Succeeded)
                    foreach (SceneEntityDefinition definition in batchResult.Asset.Value)
                        AddWorldDefinition(definition);
                else
                {
                    WorldDefinitionFailures++;
                    Debug.LogWarning($"[JUANI] World {realmData.RealmName}: registry batch FAILED: {batchResult.Exception}");
                }
            }

            if (!activeWorldBatch.HasValue && worldPointerBatches.Count > 0)
                activeWorldBatch = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(world,
                    new GetSceneDefinitionList(new List<SceneEntityDefinition>(), worldPointerBatches.Dequeue(), new CommonLoadingArguments(WorldEntitiesUrl())),
                    PartitionComponent.TOP_PRIORITY);

            for (int i = worldUrnPromises.Count - 1; i >= 0; i--)
            {
                if (!worldUrnPromises[i].TryConsume(world, out StreamableLoadingResult<SceneEntityDefinition> result)) continue;

                worldUrnPromises.RemoveAt(i);

                if (result.Succeeded)
                    AddWorldDefinition(result.Asset);
                else
                {
                    WorldDefinitionFailures++;
                    Debug.LogWarning($"[JUANI] World {realmData.RealmName}: scene definition FAILED: {result.Exception}");
                }
            }

            if (activeWorldBatch.HasValue || worldPointerBatches.Count > 0 || worldUrnPromises.Count > 0) return;

            resolvingWorldDefinitions = false;
            var byParcel = new Dictionary<Vector2Int, SceneEntityDefinition>();

            foreach (SceneEntityDefinition definition in worldDefinitions)
            foreach (Vector2Int parcel in definition.metadata.scene.DecodedParcels)
                byParcel[parcel] = definition;

            worldDefinitionsByParcel = byParcel;
            Debug.Log($"[JUANI] World {realmData.RealmName}: {worldDefinitions.Count} scene definitions over {byParcel.Count} parcels, {WorldDefinitionFailures} requests failed");
        }

        private void AddWorldDefinition(SceneEntityDefinition definition)
        {
            if (definition.pointers.Length > 0 && worldDefinitionIds.Add(definition.id))
                worldDefinitions.Add(definition);
        }

        /// <summary>Requests answered from the world's resolved definitions: a parcel no scene claims holds nothing.</summary>
        private void CreateFromWorldDefinitions(World world, Dictionary<Vector2Int, SceneEntityDefinition> byParcel)
        {
            while (pendingRequests.Count > 0)
                foreach (int2 pointer in pendingRequests.Dequeue())
                {
                    Vector2Int parcel = pointer.ToVector2Int();

                    if (entitiesByParcel.ContainsKey(parcel)) continue;

                    if (byParcel.TryGetValue(parcel, out SceneEntityDefinition definition))
                        CreateSceneEntity(world, definition);
                    else
                        emptyParcels.Add(parcel);
                }
        }

        private void ClearWorldDefinitionsState()
        {
            activeWorldBatch = null;
            worldUrnPromises.Clear();
            worldPointerBatches.Clear();
            worldDefinitions.Clear();
            worldDefinitionIds.Clear();
            worldDefinitionsByParcel = null;
            worldDefinitionsRequested = false;
            resolvingWorldDefinitions = false;
            WorldDefinitionFailures = 0;
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
                    string failure = DescribeDescriptorFailure(world.Get<SceneDefinitionComponent>(entity));
                    Debug.LogWarning($"[JUANI] Descriptor FAILED {failure} -> scene marked failed, no ISS LOD_0 to assemble");
                    failedDescriptors.Add(failure);
                    withoutDescriptor.Add(entity);
                    continue;
                }

                Debug.Log($"[JUANI] Descriptor for {id}: {descriptor.Assets.Count} assets -> assembling ISS LOD_0");
                world.Add(entity, SceneLODInfo.Create());
                awaitingLod.Add(entity);
            }
        }

        /// <summary>
        ///     The loader collapses every failure into "no descriptor"; this tells apart a manifest that predates ISS
        ///     (nothing is fetched) from a fetch of the expected file that failed (404, network or parse).
        /// </summary>
        private static string DescribeDescriptorFailure(SceneDefinitionComponent scene)
        {
            SceneEntityDefinition definition = scene.Definition;
            Vector2Int basePosition = definition.metadata.scene.DecodedBase;
            AssetBundleManifestVersion? version = definition.assetBundleManifestVersion;
            string abVersion = version?.GetAssetBundleManifestVersion() ?? "none";

            string reason;

            if (version == null || !version.SupportsISS())
                reason = "manifest predates ISS, nothing fetched";
            else
            {
                string file = version.TryGetLodDescriptorFile(out string digestNamed)
                    ? digestNamed
                    : $"{definition.id.ToLower()}_InitialSceneState.json";

                reason = $"fetch of {file} failed (404, network or parse)";
            }

            return $"{definition.id} base ({basePosition.x},{basePosition.y}) {scene.Parcels.Count} parcels ab={abVersion}: {reason}";
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
