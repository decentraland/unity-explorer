using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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
        bool IsEmpty { get; }

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

        class Fake : ISceneFacade
        {
            public Fake(SceneShortInfo info, ISceneStateProvider sceneStateProvider, SceneEcsExecutor ecsExecutor, bool isEmpty)
            {
                Info = info;
                SceneStateProvider = sceneStateProvider;
                EcsExecutor = ecsExecutor;
                IsEmpty = isEmpty;
            }

            public UniTask DisposeAsync() =>
                throw new NotImplementedException();

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public SceneShortInfo Info { get; }
            public ISceneStateProvider SceneStateProvider { get; }
            public SceneEcsExecutor EcsExecutor { get; }
            public bool IsEmpty { get; }

            public UniTask StartUpdateLoopAsync(int targetFPS, CancellationToken ct) =>
                throw new NotImplementedException();

            public void SetTargetFPS(int fps)
            {
                throw new NotImplementedException();
            }

            public void SetIsCurrent(bool isCurrent)
            {
                throw new NotImplementedException();
            }

            UniTask ISceneFacade.StartScene() =>
                throw new NotImplementedException();

            UniTask ISceneFacade.Tick(float dt) =>
                throw new NotImplementedException();

            public bool Contains(Vector2Int parcel) =>
                throw new NotImplementedException();

            public bool IsSceneReady() =>
                throw new NotImplementedException();
        }
    }
}
