using Arch.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Byte-weighted progress accumulator for a scene's GLTF assets.
    ///     Tracks per-entity ContentLength and weights assets whose size was never observed
    ///     as <see cref="UNKNOWN_ASSET_BYTES"/> so they still contribute a fixed share to total progress.
    /// </summary>
    internal class SceneByteProgressTracker : IDisposable
    {
        // Flat weight for assets with no observed ContentLength (cache hits, HEAD failures).
        // Kept at 1 byte so unknowns don't skew the bar away from real-download progress: cache hits
        // are effectively free, just held by finalize budget, and shouldn't drag the percentage down.
        private const long UNKNOWN_ASSET_BYTES = 1;

        private readonly Dictionary<Entity, long> sizes;

        private long completedBytes;
        private long totalBytesExpected;
        private int entitiesWithKnownSize;

        // Per-frame accumulator: weighted bytes for entities still loading.
        // Reset by ComputeAndClamp at frame end.
        private long inProgressWeightedBytes;

        // Byte-weighted math can dip when a small entity finishes while a new large unknown entity registers.
        // Clamping locally avoids visual regression without affecting other AsyncLoadProcessReport callers (e.g. teleport retry).
        private float maxReportedProgress;

        public SceneByteProgressTracker()
        {
            sizes = DictionaryPool<Entity, long>.Get();
        }

        public void Dispose()
        {
            DictionaryPool<Entity, long>.Release(sizes);
        }

        /// <summary>
        ///     First sighting with known length: accumulate ContentLength into totalBytesExpected exactly once
        ///     and remember it so the finish path doesn't need to re-read the loading state.
        ///     Idempotent for already-tracked entities or unknown sizes.
        /// </summary>
        public void RegisterIfNew(Entity entity, long contentLength)
        {
            if (contentLength > 0 && sizes.TryAdd(entity, contentLength))
            {
                totalBytesExpected += contentLength;
                entitiesWithKnownSize++;
            }
        }

        /// <summary>
        ///     Clean finish: credit the entity's stored ContentLength from registration,
        ///     or <see cref="UNKNOWN_ASSET_BYTES"/> if its size was never observed.
        /// </summary>
        public void CreditFinish(Entity entity)
        {
            if (sizes.Remove(entity, out long contentLength))
                completedBytes += contentLength;
            else
                completedBytes += UNKNOWN_ASSET_BYTES;
        }

        /// <summary>
        ///     Sudden death (no clean finish observed): credit the entity's registered ContentLength so
        ///     totalBytesExpected stays balanced. No-op if the entity was never registered.
        /// </summary>
        public void CreditDeath(Entity entity)
        {
            if (sizes.Remove(entity, out long contentLength))
                completedBytes += contentLength;
        }

        /// <summary>
        ///     Accumulate weighted in-progress bytes for an entity that is still loading this frame.
        /// </summary>
        public void AccumulateInProgress(float entityProgress, long contentLength)
        {
            if (contentLength > 0)
                inProgressWeightedBytes += (long)(entityProgress * contentLength);
        }

        // Asymptotic smoothing. state.Progress per entity reports only the main AB's UWR progress (not its
        // dependencies), so when each main AB finishes the aggregate steps up by (1 - last) * contentLength.
        // Lerping toward target hides those spikes; conclude path still snaps to 1.0.
        private const float SMOOTHING_FACTOR = 0.15f;

        /// <summary>
        ///     Returns this frame's progress value, guaranteed never to go down between frames.
        ///     Call once per frame: it also clears the per-frame in-progress accumulator.
        /// </summary>
        public float ComputeAndClamp(int totalAssetsToResolve)
        {
            float target = Compute(totalAssetsToResolve);
            inProgressWeightedBytes = 0;
            float smoothed = Mathf.Lerp(maxReportedProgress, target, SMOOTHING_FACTOR);
            maxReportedProgress = Mathf.Max(maxReportedProgress, smoothed);
            return maxReportedProgress;
        }

        // Cap below 1.0 because AsyncLoadProcessReport.SetProgress(>=1f) auto-resolves its completion source,
        // which closes the loading screen. In-progress credits can saturate effectiveTotal during the finalize-wait
        // window (entities are downloaded but still in LoadingState.Loading); only the explicit conclude path
        // in GatherGltfAssetsSystem should report 1.0.
        private const float MAX_IN_PROGRESS = 0.99f;

        private float Compute(int totalAssetsToResolve)
        {
            int unknownCount = Math.Max(0, totalAssetsToResolve - entitiesWithKnownSize);
            long effectiveTotal = totalBytesExpected + (UNKNOWN_ASSET_BYTES * unknownCount);

            return effectiveTotal > 0
                ? Mathf.Min(MAX_IN_PROGRESS, (float)(completedBytes + inProgressWeightedBytes) / effectiveTotal)
                : 0f;
        }
    }
}
