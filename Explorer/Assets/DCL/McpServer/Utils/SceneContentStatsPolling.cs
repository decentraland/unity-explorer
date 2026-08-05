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
            var elapsedMs = 0;

            while (stats.CollectionCount == collectionsBefore && elapsedMs < timeoutMs)
            {
                await UniTask.Delay(POLL_INTERVAL_MS, cancellationToken: ct);
                elapsedMs += POLL_INTERVAL_MS;

                if (scenesCache.CurrentScene.Value != scene)
                    return false;
            }

            return true;
        }
    }
}
