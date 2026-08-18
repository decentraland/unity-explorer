using Cysharp.Threading.Tasks;
using DCL.Profiling;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System.Threading;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     Shared wait loop for the content-stats MCP tools (<c>get_scene_content_stats</c> and
    ///     <c>get_scene_content_breakdown</c>): both set a demand flag and then block until the scene
    ///     world completes a counting pass. Centralizing the timing here keeps the two tools from
    ///     drifting apart if the cadence ever needs adjusting.
    /// </summary>
    public static class SceneContentStatsPolling
    {
        /// <summary>
        ///     Shape of <see cref="WaitForCollectionAsync" />. The content-stats tools take it as an
        ///     injectable dependency so tests can simulate a pass landing or the scene changing mid-wait.
        /// </summary>
        public delegate UniTask<bool> WaitForCollection(IScenesCache scenesCache, ISceneFacade scene, SceneContentStats stats, long collectionsBefore, int timeoutMs, CancellationToken ct);

        private const int POLL_INTERVAL_MS = 100;

        // Generous: with another consumer already collecting, the next pass can be a full
        // 60-frame cooldown away, which stretches to seconds when the scene runs at low FPS.
        public const int DEFAULT_COLLECTION_TIMEOUT_MS = 10_000;

        /// <summary>
        ///     Polls until the scene world advances <see cref="SceneContentStats.CollectionCount" /> past
        ///     <paramref name="collectionsBefore" /> or the timeout elapses. Returns false when the current
        ///     scene changed mid-wait (the caller reports that as an error); whether a pass actually landed
        ///     is read from <see cref="SceneContentStats.CollectionCount" /> by the caller after this returns.
        /// </summary>
        public static async UniTask<bool> WaitForCollectionAsync(IScenesCache scenesCache, ISceneFacade scene, SceneContentStats stats, long collectionsBefore, int timeoutMs, CancellationToken ct)
        {
            float startTime = UnityEngine.Time.realtimeSinceStartup;

            // Elapsed is measured against the wall clock rather than accumulated from the requested
            // delay: at low FPS each Delay completes a whole frame late, and those overshoots add up.
            while (stats.CollectionCount == collectionsBefore && (UnityEngine.Time.realtimeSinceStartup - startTime) * 1000f < timeoutMs)
            {
                await UniTask.Delay(POLL_INTERVAL_MS, cancellationToken: ct);

                if (scenesCache.CurrentScene.Value != scene)
                    return false;
            }

            return true;
        }
    }
}
