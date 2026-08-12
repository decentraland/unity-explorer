using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.Ipfs;
using DCL.ResourcesUnloading;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle
{
    public class ECSReloadScene
    {
        private readonly IScenesCache scenesCache;

        private readonly Entity playerEntity;
        private readonly World world;
        private readonly bool localSceneDevelopment;
        private readonly ICacheCleaner cacheCleaner;

        public ECSReloadScene(IScenesCache scenesCache,
            World world,
            Entity playerEntity,
            bool localSceneDevelopment,
            ICacheCleaner cacheCleaner)
        {
            this.scenesCache = scenesCache;
            this.world = world;
            this.playerEntity = playerEntity;
            this.localSceneDevelopment = localSceneDevelopment;
            this.cacheCleaner = cacheCleaner;
        }

        public async UniTask<ISceneFacade?> TryReloadSceneAsync(CancellationToken ct)
        {
            ResetStalePointAt();

            var parcel = world.Get<CharacterTransform>(playerEntity).Transform.ParcelPosition();
            if (!scenesCache.TryGetByParcel(parcel, out var sceneInCache)) return null;

            var foundEntity = FindSceneEntity(sceneInCache);
            if (foundEntity == Entity.Null) return null;

            await DisposeAndRestartAsync(foundEntity, sceneInCache, null, ct);

            return sceneInCache;
        }

        public async UniTask<ISceneFacade?> TryReloadSceneAsync(CancellationToken ct, string sceneId, string? changedModelSrc = null)
        {
            ResetStalePointAt();

            if (!scenesCache.TryGetBySceneId(sceneId, out var sceneInCache)) return null;

            var foundEntity = FindSceneEntity(sceneInCache!);
            if (foundEntity == Entity.Null) return null;

            await DisposeAndRestartAsync(foundEntity, sceneInCache!, changedModelSrc, ct);

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

        private void ResetStalePointAt()
        {
            if (!world.Has<HandPointAtComponent>(playerEntity)) return;

            ref var pointAt = ref world.Get<HandPointAtComponent>(playerEntity);
            if (pointAt.IsPointing) pointAt.StopPointing();
        }

        private async UniTask DisposeAndRestartAsync(Entity entity, ISceneFacade currentScene, string? changedModelSrc, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // Captured before the teardown below strips the scene entity's components.
            SceneEntityDefinition? definition = localSceneDevelopment
                ? world.Get<SceneDefinitionComponent>(entity).Definition
                : null;

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

            if (localSceneDevelopment)
            {
                world.Query(in new QueryDescription().WithAll<RealmComponent>(),
                    (ref StaticScenePointers staticScenePointers) => { staticScenePointers.Promise = null; });

                if (changedModelSrc != null
                    && TryResolveContentHash(definition, changedModelSrc, out string contentHash)
                    && IsRawGltfModel(definition, contentHash))
                {
                    // The dev server named the exact model that changed. In raw-GLTF development its
                    // cache key is the bare content hash, so evict just that asset and let every other
                    // cache stay warm across the reload.
                    cacheCleaner.EvictGltfModel(contentHash);
                }
                else
                {
                    // Force-drain dereferenced caches on LSD reload. The local dev server derives hashes
                    // from the file path, not content, so an updated model keeps the same hash and cache
                    // hits would return stale assets. Draining guarantees fresh loads.
                    cacheCleaner.UnloadCache(budgeted: false);
                    _ = Resources.UnloadUnusedAssets();
                }

                await WaitUntilNewSceneIsFullyLoadedAsync();
            }

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

        /// <summary>
        ///     Resolve the changed file to the hash the caches are actually keyed on. The reload
        ///     message's own hash is minted from the watcher-relative path, while every cache key
        ///     derives from the content-mapping hash (minted from the absolute path) — the two never
        ///     match, so the file must be joined to the definition's content list by its src instead.
        /// </summary>
        internal static bool TryResolveContentHash(SceneEntityDefinition? definition, string src, out string hash)
        {
            hash = string.Empty;

            ContentDefinition[]? content = definition?.content;

            if (content == null || string.IsNullOrEmpty(src))
                return false;

            foreach (ContentDefinition entry in content)
            {
                if (ContentPathEquals(entry.file, src))
                {
                    hash = entry.hash;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Case- and separator-insensitive content path comparison. Content mappings always spell
        ///     paths with '/', while the local dev server's file watcher reports the platform separator —
        ///     on Windows that is '\', so an ordinal comparison never matches there even though the two
        ///     name the same file. Compared in place to keep the reload path allocation-free.
        /// </summary>
        private static bool ContentPathEquals(string? contentFile, string src)
        {
            if (contentFile == null || contentFile.Length != src.Length)
                return false;

            for (var i = 0; i < contentFile.Length; i++)
            {
                char a = contentFile[i];
                char b = src[i];

                if (a == '\\') a = '/';
                if (b == '\\') b = '/';

                if (a != b && char.ToLowerInvariant(a) != char.ToLowerInvariant(b))
                    return false;
            }

            return true;
        }

        /// <summary>
        ///     True when the hash addresses a raw GLTF, i.e. no asset-bundle manifest maps it. The GLTF
        ///     container cache is keyed by <see cref="AssetBundleManifestVersion.ComposeCacheKey" />,
        ///     which returns the bare hash only in that case; under <c>--local-ab</c> the key differs and
        ///     the model lives in the asset-bundle caches instead, so scoped eviction must not be used.
        /// </summary>
        internal static bool IsRawGltfModel(SceneEntityDefinition? definition, string hash)
        {
            if (definition == null || string.IsNullOrEmpty(hash))
                return false;

            return definition.AssetBundleManifestVersionOrFailed.ComposeCacheKey(hash) == hash;
        }
    }
}
