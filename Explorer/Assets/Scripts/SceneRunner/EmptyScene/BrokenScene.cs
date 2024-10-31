using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PluginSystem.World;
using SceneRunner.Scene;
using UnityEngine;

namespace SceneRunner.EmptyScene
{
    /// <summary>
    ///     An SDK7 scene flagged as broken
    /// </summary>
    public class BrokenScene : ISceneFacade
    {
        public BrokenScene(ISceneData sceneData)
        {
            SceneData = sceneData;
            SceneStateProvider = new SceneStateProvider();
            SceneStateProvider.State = SceneState.EngineError;
        }

        public void Dispose()
        {
            // TODO release managed resources here
        }

        public UniTask DisposeAsync()
        {
            return UniTask.CompletedTask;
        }

        public SceneShortInfo Info { get; }
        public ISceneStateProvider SceneStateProvider { get; }
        public SceneEcsExecutor EcsExecutor { get; }
        public PersistentEntities PersistentEntities { get; }
        public ISceneData SceneData { get; }
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
    }
}