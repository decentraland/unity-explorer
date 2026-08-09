using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle
{
    /// <summary>
    ///     The single GLTF model a local-development hot reload reported as changed, carried from the
    ///     dev server's websocket message so the reload can evict just that asset instead of the whole cache.
    /// </summary>
    public readonly struct ChangedGltfModel
    {
        public readonly string Src;
        public readonly string Hash;

        public ChangedGltfModel(string src, string hash)
        {
            Src = src;
            Hash = hash;
        }
    }

    public class ECSReloadScene
    {
        private const double DEFINITION_REFRESH_TIMEOUT_SECS = 3;
        private const string B64_ID_PREFIX = "b64-";

        private readonly IScenesCache scenesCache;

        private readonly Entity playerEntity;
        private readonly World world;
        private readonly bool localSceneDevelopment;
        private readonly ICacheCleaner cacheCleaner;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;

        public ECSReloadScene(IScenesCache scenesCache,
            World world,
            Entity playerEntity,
            bool localSceneDevelopment,
            ICacheCleaner cacheCleaner,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource)
        {
            this.scenesCache = scenesCache;
            this.world = world;
            this.playerEntity = playerEntity;
            this.localSceneDevelopment = localSceneDevelopment;
            this.cacheCleaner = cacheCleaner;
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
        }

        public async UniTask<ISceneFacade?> TryReloadSceneAsync(CancellationToken ct)
        {
            var parcel = world.Get<CharacterTransform>(playerEntity).Transform.ParcelPosition();
            if (!scenesCache.TryGetByParcel(parcel, out var sceneInCache)) return null;

            var foundEntity = FindSceneEntity(sceneInCache);
            if (foundEntity == Entity.Null) return null;

            await DisposeAndRestartAsync(foundEntity, sceneInCache, null, ct);

            return sceneInCache;
        }

        public async UniTask<ISceneFacade?> TryReloadSceneAsync(CancellationToken ct, string sceneId, ChangedGltfModel? changedModel = null)
        {
            if (!scenesCache.TryGetBySceneId(sceneId, out var sceneInCache)) return null;

            var foundEntity = FindSceneEntity(sceneInCache!);
            if (foundEntity == Entity.Null) return null;

            await DisposeAndRestartAsync(foundEntity, sceneInCache!, changedModel, ct);

            return sceneInCache;
        }

        private Entity FindSceneEntity(ISceneFacade targetScene)
        {
            var sceneEntity = Entity.Null;

            world.Query(in new QueryDescription().WithAll<ISceneFacade, SceneDefinitionComponent>(),
                (Entity entity, ref ISceneFacade sceneFacade) =>
                {
                    if (sceneFacade.Equals(targetScene)) { sceneEntity = entity; }
                });

            return sceneEntity;
        }

        private async UniTask DisposeAndRestartAsync(Entity entity, ISceneFacade currentScene, ChangedGltfModel? changedModel, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            float reloadStart = Time.realtimeSinceStartup;
            float disposeDone, drainDone, refreshDone;
            string drainMode;

            SceneEntityDefinition? cachedDefinition = null;

            // Gate facade promise recreation on the kept definition entity: UnloadSceneSystem strips
            // the facade components as soon as the unload starts, and without the gate
            // ResolveStaticPointersSystem would recreate the promise before the refreshed
            // definition lands.
            if (localSceneDevelopment)
            {
                cachedDefinition = world.Get<SceneDefinitionComponent>(entity).Definition;
                world.Add<SceneDefinitionRefreshPending>(entity);
            }

            try
            {
                // The definition's content list changes when files are added, removed or renamed
                // (in local scene development the file path is the content hash). Fetch the fresh
                // definition in parallel so it lands within the dispose wait below.
                UniTask<SceneEntityDefinition?> definitionRefresh = localSceneDevelopment
                    ? FetchRefreshedDefinitionAsync(entity, ct)
                    : UniTask.FromResult<SceneEntityDefinition?>(null);

                //There is a lingering promise we need to remove, and add the DeleteEntityIntention to make the standard unload flow.
                world.Add<DeleteEntityIntention>(entity);

                //We wait until scene is fully disposed
                await UniTask.WaitUntil(() => currentScene.SceneStateProvider.State.Value() == SceneState.Disposed, cancellationToken: ct);

                if (world.IsAlive(entity))
                {
                    SceneLoadingState sceneLoadingState = world.Get<SceneLoadingState>(entity);
                    sceneLoadingState.VisualSceneState = VisualSceneState.Uninitialized;
                    sceneLoadingState.PromiseCreated = false;
                }

                if (!localSceneDevelopment) return;

                disposeDone = Time.realtimeSinceStartup;

                // Await the refresh before the cache drain: the fetch completed during the dispose
                // wait, while Resources.UnloadUnusedAssets hitches the main thread for several frames
                // and would stall the await's main-thread continuation.
                SceneEntityDefinition? refreshedDefinition = await definitionRefresh;
                ApplyRefreshedDefinition(entity, refreshedDefinition);

                refreshDone = Time.realtimeSinceStartup;

                SceneEntityDefinition? definitionInEffect = refreshedDefinition ?? cachedDefinition;

                // Content-versioned ids (the sdk server embeds the file's mtime) give an edited file a
                // new id, so every cache key derived from it self-invalidates and the caches stay warm
                // across the reload — nothing to drain.
                if (HasContentVersionedIds(definitionInEffect))
                    drainMode = "skipped: content-versioned ids";
                else if (changedModel is { } model && IsRawGltfModel(definitionInEffect, model.Hash))
                {
                    // The dev server told us exactly which model changed. In raw-GLTF development its
                    // cache key is the bare content hash, so evict just that asset and let every other
                    // cache stay warm across the reload.
                    cacheCleaner.EvictGltfModel(model.Hash, model.Src);
                    drainMode = $"scoped evict: {model.Src}";
                }
                else
                {
                    // Path-only ids keep the same cache key when a file changes, and without a per-file
                    // change signal we can't tell what is stale: drain every cache so edits show up.
                    cacheCleaner.UnloadCache(budgeted: false);
                    Resources.UnloadUnusedAssets();
                    drainMode = "full drain";
                }

                drainDone = Time.realtimeSinceStartup;
            }
            finally
            {
                if (localSceneDevelopment && world.IsAlive(entity))
                    world.Remove<SceneDefinitionRefreshPending>(entity);
            }

            await WaitUntilNewSceneIsFullyLoadedAsync();

            float loadDone = Time.realtimeSinceStartup;

            ReportHub.LogProductionInfo(
                $"JUANI: Scene reload completed in {loadDone - reloadStart:F2}s (dispose {disposeDone - reloadStart:F2}s, definition refresh +{refreshDone - disposeDone:F2}s, cache [{drainMode}] {drainDone - refreshDone:F2}s, load {loadDone - drainDone:F2}s)");

            return;

            async UniTask WaitUntilNewSceneIsFullyLoadedAsync()
            {
                await UniTask.WaitUntil(() =>
                {
                    var isLoadCompleted = false;

                    // TODO: filter by scene coord/id? We currently assume that only one scene will be running during local scene development
                    world.Query(in new QueryDescription().WithAll<ISceneFacade>().WithNone<DeleteEntityIntention>(),
                        (ref ISceneFacade newScene) =>
                        {
                            if (newScene.SceneStateProvider.State.Value() is SceneState.JavaScriptError
                                or SceneState.EcsError)
                            {
                                isLoadCompleted = true;
                                return;
                            }

                            isLoadCompleted = newScene.SceneStateProvider.State.Value() is SceneState.Running
                                              // Consider GLTF models in the initial loading phase since they're not tracked by SceneStateProvider.State.
                                              // This prevents the character from falling through unloaded colliders during scene reload.
                                              && newScene.SceneData.SceneLoadingConcluded;
                        });

                    return isLoadCompleted;
                }, cancellationToken: ct);
            }
        }

        private async UniTask<SceneEntityDefinition?> FetchRefreshedDefinitionAsync(Entity entity, CancellationToken ct)
        {
            Vector2Int baseParcel = world.Get<SceneDefinitionComponent>(entity).Definition.metadata.scene.DecodedBase;

            try
            {
                SceneEntityDefinition[] definitions = await webRequestController
                                                           .PostAsync(new CommonArguments(URLAddress.FromString(urlsSource.Url(DecentralandUrl.EntitiesActive))),
                                                                GenericPostArguments.CreateJson($"{{\"pointers\":[\"{baseParcel.x},{baseParcel.y}\"]}}"),
                                                                ct, ReportCategory.SCENE_LOADING)
                                                           .CreateFromJson<SceneEntityDefinition[]>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool)
                                                           .Timeout(TimeSpan.FromSeconds(DEFINITION_REFRESH_TIMEOUT_SECS));

                await UniTask.SwitchToMainThread(ct);

                return definitions.Length > 0 ? definitions[0] : null;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                await UniTask.SwitchToMainThread(ct);
                ReportHub.LogWarning(ReportCategory.SCENE_LOADING, $"Scene reload: definition refresh failed, reusing the cached definition: {e.Message}");
                return null;
            }
        }

        /// <summary>
        ///     True when the scene's content ids embed a version marker: a NUL byte separating the
        ///     file path from its mtime, minted by sdk-commands' content-versioned hashing. Such ids
        ///     change whenever the file changes, so cache keys derived from them never go stale. The
        ///     marker is unforgeable by accident — neither file paths nor hostnames can contain a NUL
        ///     byte, so plain path-derived ids can never decode to one. Any doubt (non-b64 id, empty
        ///     content, malformed base64) returns false.
        /// </summary>
        internal static bool HasContentVersionedIds(SceneEntityDefinition? definition)
        {
            ContentDefinition[]? content = definition?.content;

            if (content == null || content.Length == 0)
                return false;

            // One entry decides for the whole definition: the sdk server mints every file id
            // with the same hashing function within a single response.
            string hash = content[0].hash;

            if (string.IsNullOrEmpty(hash) || !hash.StartsWith(B64_ID_PREFIX, StringComparison.Ordinal))
                return false;

            // Ids have been minted with both the standard and the url-safe base64 alphabets
            // across sdk-commands versions; normalize to the standard one.
            string payload = hash.Substring(B64_ID_PREFIX.Length).Replace('-', '+').Replace('_', '/');

            int remainder = payload.Length % 4;

            if (remainder == 1)
                return false;

            if (remainder > 0)
                payload += new string('=', 4 - remainder);

            try
            {
                byte[] decoded = Convert.FromBase64String(payload);
                return Array.IndexOf(decoded, (byte)0) >= 0;
            }
            catch (FormatException) { return false; }
        }

        /// <summary>
        ///     True when the hash addresses a raw GLTF, i.e. no asset-bundle manifest maps it. The GLTF
        ///     container cache is keyed by <see cref="Ipfs.AssetBundleManifestVersion.ComposeCacheKey" />,
        ///     which returns the bare hash only in that case; under <c>--local-ab</c> the key differs and
        ///     the model lives in the asset-bundle caches instead, so scoped eviction must not be used.
        /// </summary>
        internal static bool IsRawGltfModel(SceneEntityDefinition? definition, string hash)
        {
            if (definition == null || string.IsNullOrEmpty(hash))
                return false;

            return definition.AssetBundleManifestVersionOrFailed.ComposeCacheKey(hash) == hash;
        }

        internal void ApplyRefreshedDefinition(Entity entity, SceneEntityDefinition? refreshed)
        {
            if (refreshed == null || !world.IsAlive(entity)) return;

            SceneEntityDefinition cached = world.Get<SceneDefinitionComponent>(entity).Definition;

            // A parcel layout change invalidates the precomputed scene geometry on the kept
            // component: fall back to full re-discovery, which re-creates the definition entity.
            if (!PointersEqual(cached.pointers, refreshed.pointers))
            {
                world.Destroy(entity);

                world.Query(in new QueryDescription().WithAll<RealmComponent>(),
                    (ref StaticScenePointers staticScenePointers) => { staticScenePointers.Promise = null; });

                return;
            }

            // Same parcels: adopt the fresh content list so files added, removed or renamed since
            // the definition was cached resolve correctly on this reload.
            cached.content = refreshed.content;
        }

        private static bool PointersEqual(string[] cached, string[] refreshed)
        {
            if (cached.Length != refreshed.Length) return false;

            for (var i = 0; i < cached.Length; i++)
            {
                if (cached[i] != refreshed[i])
                    return false;
            }

            return true;
        }
    }
}
