using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PluginSystem.World;
using System;
using System.Threading;
using UnityEngine;

namespace SceneRunner.Scene
{
    public interface ISceneFacade : IUniTaskAsyncDisposable, IDisposable
    {
        SceneShortInfo Info { get; }
        ISceneStateProvider SceneStateProvider { get; }
        SceneEcsExecutor EcsExecutor { get; }
        PersistentEntities PersistentEntities { get; }
        ISceneData SceneData { get; }
        bool IsEmpty { get; }

        void Initialize();

        /// <summary>
        ///     Start an update loop with a given FPS
        /// </summary>
        UniTask StartUpdateLoopAsync(int targetFPS, CancellationToken ct);

        /// <summary>
        ///     Change the target fps while the scene is running.
        ///     It will take effect right after the next update
        /// </summary>
        /// <param name="fps">Target FPS</param>
        void SetTargetFPS(int fps);

        void SetIsCurrent(bool isCurrent);

        internal UniTask StartScene();

        internal UniTask Tick(float dt);

        bool Contains(Vector2Int parcel);

        bool IsSceneReady();

        class FakeSceneFacade : ISceneFacade
        {
            public UniTask DisposeAsync() => UniTask.CompletedTask;

            public void Dispose() { }

            public SceneShortInfo Info { get; }
            public ISceneStateProvider SceneStateProvider { get; }
            public SceneEcsExecutor EcsExecutor { get; }
            public PersistentEntities PersistentEntities { get; }
            public ISceneData SceneData { get; } = new ISceneData.Fake();
            public bool IsEmpty { get; }

            public void Initialize() { }

            public UniTask StartUpdateLoopAsync(int targetFPS, CancellationToken ct) => UniTask.CompletedTask;

            public void SetTargetFPS(int fps) { }

            public void SetIsCurrent(bool isCurrent) { }

            UniTask ISceneFacade.StartScene() => UniTask.CompletedTask;

            UniTask ISceneFacade.Tick(float dt) => UniTask.CompletedTask;

            public bool Contains(Vector2Int parcel) =>
                true;

            public bool IsSceneReady() =>
                true;
        }
    }
}
