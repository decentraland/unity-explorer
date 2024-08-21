using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.PluginSystem.World;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace SceneRunner.EmptyScene
{
    public class EmptySceneFacade : ISceneFacade
    {
        private static readonly IObjectPool<EmptySceneFacade> POOL = new ThreadSafeObjectPool<EmptySceneFacade>(() => new EmptySceneFacade(), defaultCapacity: PoolConstants.EMPTY_SCENES_COUNT);

        private Args args;

        private EmptySceneFacade() { }

        public SceneShortInfo Info => args.ShortInfo;
        public ISceneStateProvider SceneStateProvider { get; }
        public SceneEcsExecutor EcsExecutor { get; }
        public PersistentEntities PersistentEntities => default;

        public bool IsEmpty => true;

        public void Dispose()
        {
            POOL.Release(this);
            args = default(Args);
        }

        public async UniTask DisposeAsync()
        {
            await UniTask.SwitchToThreadPool();
            Dispose();
        }

        public bool IsSceneReady() =>
            true;

        public void Initialize() { }

        public UniTask StartUpdateLoopAsync(int targetFPS, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void SetTargetFPS(int fps)
        {
            // has no effect
        }

        public void SetIsCurrent(bool isCurrent)
        {
            // has no effect
        }

        UniTask ISceneFacade.StartScene() =>

            // Should be never called as it corresponds to JS logic
            throw new NotImplementedException();

        UniTask ISceneFacade.Tick(float dt) =>
            UniTask.CompletedTask;

        public bool Contains(Vector2Int parcel) =>
            args.ShortInfo.BaseParcel == parcel;

        public static EmptySceneFacade Create(Args args)
        {
            EmptySceneFacade f = POOL.Get();
            f.args = args;
            return f;
        }

        public readonly struct Args
        {
            public readonly SceneShortInfo ShortInfo;

            public Args(SceneShortInfo shortInfo)
            {
                ShortInfo = shortInfo;
            }
        }
    }
}
