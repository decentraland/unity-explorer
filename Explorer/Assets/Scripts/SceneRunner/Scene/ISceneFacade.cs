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
    }
}
