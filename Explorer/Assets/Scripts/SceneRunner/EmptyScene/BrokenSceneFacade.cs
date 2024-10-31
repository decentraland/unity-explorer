using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.PluginSystem.World;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Pool;

namespace SceneRunner.EmptyScene
{
    /// <summary>
    ///     An SDK7 scene flagged as broken
    /// </summary>
    public class BrokenSceneFacade : ISceneFacade
    {
        private static readonly IObjectPool<BrokenSceneFacade> POOL = new ThreadSafeObjectPool<BrokenSceneFacade>(() => new BrokenSceneFacade(), defaultCapacity: PoolConstants.BROKEN_SCENES_COUNT);

        private BrokenSceneFacade() { }
        
      
        public void Dispose()
        {
            POOL.Release(this);
        }

        public async UniTask DisposeAsync()
        {
            await UniTask.SwitchToThreadPool();
            Dispose();
        }

        public SceneShortInfo Info { get; }
        public ISceneStateProvider SceneStateProvider { get; private set; }
        public SceneEcsExecutor EcsExecutor { get; }
        public PersistentEntities PersistentEntities { get; }
        public ISceneData SceneData { get; private set; }
        public bool IsEmpty => false;
        public bool IsBrokenScene => true;

        public bool Contains(Vector2Int parcel)
        {
            return SceneData.Parcels.Contains(parcel);
        }

        public bool IsSceneReady()
        {
            return true;
        }

        public void Initialize() { }

        public UniTask StartUpdateLoopAsync(int targetFPS, CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

        public void SetTargetFPS(int fps)
        {
            // has no effect
        }

        public void SetIsCurrent(bool isCurrent)
        {
            // has no effect
        }

        UniTask ISceneFacade.StartScene()
        {
            // Should be never called as it corresponds to JS logic
            throw new NotImplementedException();
        }

        UniTask ISceneFacade.Tick(float dt)
        {
            return UniTask.CompletedTask;
        }

        public static BrokenSceneFacade Create(ISceneData sceneData, Exception e)
        {
            var brokenScene = POOL.Get();
            brokenScene.SceneData = sceneData;
            var sceneStateProvider = new SceneStateProvider();
            sceneStateProvider.State = SceneState.EngineError;
            brokenScene.SceneStateProvider = sceneStateProvider;

            ReportHub.LogError(ReportCategory.SCENE_LOADING, $"Scene {sceneData.SceneEntityDefinition.metadata.scene.DecodedBase} failed to load with exception {e.Message}");
            return brokenScene;
        }
    }
}