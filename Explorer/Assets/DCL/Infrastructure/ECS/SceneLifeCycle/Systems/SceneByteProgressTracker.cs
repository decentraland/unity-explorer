using Arch.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Byte-weighted progress accumulator for a scene's GLTF assets.
    ///     Tracks per-entity ContentLength and computes a download-data aware progress value,
    ///     falling back to a count-based estimate when no byte data is available.
    /// </summary>
    internal class SceneByteProgressTracker : IDisposable
    {
        private readonly HashSet<Entity> tracked;

        private long completedBytes;
        private long totalBytesExpected;
        private int entitiesWithKnownSize;

        // Byte-weighted math can dip when a small entity finishes while a new large unknown entity registers (avg shifts up).
        // Clamping locally avoids visual regression without affecting other AsyncLoadProcessReport callers (e.g. teleport retry).
        private float maxReportedProgress;

        public SceneByteProgressTracker()
        {
            tracked = HashSetPool<Entity>.Get();
        }

        public void Dispose()
        {
            HashSetPool<Entity>.Release(tracked);
        }

        /// <summary>
        ///     First sighting with known length: accumulate ContentLength into totalBytesExpected exactly once.
        ///     Idempotent for already-tracked entities or unknown sizes.
        /// </summary>
        public void RegisterIfNew(Entity entity, long contentLength)
        {
            if (contentLength > 0 && tracked.Add(entity))
            {
                totalBytesExpected += contentLength;
                entitiesWithKnownSize++;
            }
        }

        /// <summary>
        ///     Clean finish: credit the entity's actual ContentLength.
        ///     Falls back to average size when length is unknown.
        /// </summary>
        public void CreditFinish(Entity entity, long contentLength)
        {
            if (contentLength > 0)
                completedBytes += contentLength;
            else
                CreditAverageSize();

            tracked.Remove(entity);
        }

        /// <summary>
        ///     Sudden death (no clean finish observed): credit average size if entity was previously tracked.
        /// </summary>
        public void CreditDeath(Entity entity)
        {
            if (tracked.Remove(entity))
                CreditAverageSize();
        }

        /// <summary>
        ///     Compute byte-weighted progress and clamp it to non-regressing.
        ///     Falls back to count-based progress when no byte data is available.
        /// </summary>
        public float ComputeAndClamp(long inProgressWeightedBytes, int totalAssetsToResolve, int assetsResolved)
        {
            float progress = Compute(inProgressWeightedBytes, totalAssetsToResolve, assetsResolved);
            maxReportedProgress = Mathf.Max(maxReportedProgress, progress);
            return maxReportedProgress;
        }

        private void CreditAverageSize()
        {
            if (entitiesWithKnownSize > 0 && totalBytesExpected > 0)
                completedBytes += totalBytesExpected / entitiesWithKnownSize;
        }

        private float Compute(long inProgressWeightedBytes, int totalAssetsToResolve, int assetsResolved)
        {
            // Fallback: count-based progress when no byte data is available
            if (entitiesWithKnownSize <= 0 || totalBytesExpected <= 0)
                return totalAssetsToResolve != 0 ? assetsResolved / (float)totalAssetsToResolve : 1f;

            // Estimate unknown assets using average size of known assets
            long avgSize = totalBytesExpected / entitiesWithKnownSize;
            int unknownCount = totalAssetsToResolve - entitiesWithKnownSize;
            long effectiveTotal = totalBytesExpected + avgSize * Math.Max(0, unknownCount);

            return effectiveTotal > 0
                ? Mathf.Clamp01((float)(completedBytes + inProgressWeightedBytes) / effectiveTotal)
                : 0f;
        }
    }
}
