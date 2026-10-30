using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System;
using System.Threading;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     Waits for a reloaded scene to become observable: the reload controller returns once the old scene is
    ///     disposed (or, in local scene development, once the new one is loading), before the new scene has run a
    ///     tick or rendered a frame, so a capture taken right after it shows the previous scene.
    /// </summary>
    public static class SceneSettleWait
    {
        /// <summary>
        ///     Shape of <see cref="WaitForFreshSceneAsync" />. <c>reload_scene</c> takes it as an injectable
        ///     dependency so tests can script the settle without a scene world.
        /// </summary>
        public delegate UniTask<Result> WaitForFreshScene(IScenesCache scenesCache, ISceneFacade? previous, int minTicks, float deadlineSec, CancellationToken ct);

        /// <summary>Per-frame yield; injectable so tests can drive the poll loop synchronously.</summary>
        internal delegate UniTask YieldFrame(CancellationToken ct);

        public readonly struct Result
        {
            public readonly bool Settled;
            public readonly SceneState State;
            public readonly uint TicksWaited;
            public readonly int WaitedMs;

            public Result(bool settled, SceneState state, uint ticksWaited, int waitedMs)
            {
                Settled = settled;
                State = state;
                TicksWaited = ticksWaited;
                WaitedMs = waitedMs;
            }
        }

        /// <summary>
        ///     Polls until the current scene is a different facade than <paramref name="previous" /> in
        ///     <see cref="SceneState.Running" /> and its tick counter advanced by <paramref name="minTicks" />.
        ///     A scene that lands in an error state ends the wait early, unsettled; so does the deadline.
        /// </summary>
        public static UniTask<Result> WaitForFreshSceneAsync(IScenesCache scenesCache, ISceneFacade? previous, int minTicks, float deadlineSec, CancellationToken ct) =>
            WaitForFreshSceneAsync(scenesCache, previous, minTicks, deadlineSec, ct, YieldOneFrameAsync);

        internal static async UniTask<Result> WaitForFreshSceneAsync(IScenesCache scenesCache, ISceneFacade? previous, int minTicks, float deadlineSec, CancellationToken ct, YieldFrame yieldFrame)
        {
            float start = UnityEngine.Time.realtimeSinceStartup;
            float deadline = start + deadlineSec;

            ISceneFacade? fresh = null;
            uint baseTick = 0;
            uint ticksWaited = 0;
            var state = SceneState.NotStarted;

            while (true)
            {
                ISceneFacade? current = scenesCache.CurrentScene.Value;

                if (current != null && !ReferenceEquals(current, previous))
                {
                    state = current.SceneStateProvider.State.Value();

                    if (state is SceneState.JavaScriptError or SceneState.EcsError or SceneState.EngineError)
                        return new Result(false, state, ticksWaited, ElapsedMs(start));

                    if (state == SceneState.Running)
                    {
                        if (!ReferenceEquals(fresh, current))
                        {
                            fresh = current;
                            baseTick = current.SceneStateProvider.TickNumber;
                        }

                        ticksWaited = current.SceneStateProvider.TickNumber - baseTick;

                        if (ticksWaited >= minTicks)
                            return new Result(true, state, ticksWaited, ElapsedMs(start));
                    }
                }

                if (UnityEngine.Time.realtimeSinceStartup >= deadline)
                    return new Result(false, state, ticksWaited, ElapsedMs(start));

                await yieldFrame(ct);
            }
        }

        private static UniTask YieldOneFrameAsync(CancellationToken ct) =>
            UniTask.Yield(PlayerLoopTiming.Update, ct);

        private static int ElapsedMs(float start) =>
            (int)Math.Round((UnityEngine.Time.realtimeSinceStartup - start) * 1000f);
    }
}
