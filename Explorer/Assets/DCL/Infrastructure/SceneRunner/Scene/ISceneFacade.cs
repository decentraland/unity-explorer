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

        /// <summary>
        ///     Queue a JavaScript snippet to be evaluated at the beginning of the next update
        /// </summary>
        void EnqueueJsEvaluation(string jsCode);

        /// <summary>
        ///     Inject code into onUpdate; code runs before (default) or after original update
        /// </summary>
        void InjectOnUpdate(string jsCode, bool runAfterOriginal = false);
    }
}
