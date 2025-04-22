using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Utilities;
using ECS.SceneLifeCycle.SceneDefinition;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    // Following the limits from the docs. One parcel gives 15MB, capped at 300MB. Im considering here the 'worst scenarios' possible
    // https://docs.decentraland.org/creator/development-guide/sdk7/scene-limitations/#scene-limitation-rules
    // We add an estimation factor (1.1f); assets loaded in memory do not have the same size as in disk

    // IE:
    // A single scene can take 330MB
    // A single high quality LOD can take 121MB (MaxSceneSize/3 + MaxSceneSize/30)
    // A single low quality LOD can take 11MB (MaxSceneSize/30)

    // The following values take into consideration the 'worst scenarios', built using GP as reference.
    // Since all scenes dont take do the worst scenario, more will be loaded. This just ensures the upper limit
    public class SceneLoadingMemoryConstants
    {
        private static readonly float RUNTIME_MEMORY_COEFFICENT = 1.1f;
        public static float LOD_REDUCTION = 3;
        public static float QUALITY_REDUCTED_LOD_REDUCTION = 30;

        public static float MAX_SCENE_SIZE = 300 * RUNTIME_MEMORY_COEFFICENT;
        public static float MAX_SCENE_LOD = (MAX_SCENE_SIZE / LOD_REDUCTION) + (MAX_SCENE_SIZE / QUALITY_REDUCTED_LOD_REDUCTION);
        public static float MAX_SCENE_LOWQUALITY_LOD = MAX_SCENE_SIZE / QUALITY_REDUCTED_LOD_REDUCTION;
    }

    public class SceneLoadingLimit
    {

        private readonly Dictionary<SceneLimitsKey, SceneLimits> sceneLimits = new ()
        {
            // 1 scene, 1 high quality LOD, 10 low quality LODs. Limit: 561MB
            { SceneLimitsKey.LOW_MEMORY, new SceneLimits(SceneLoadingMemoryConstants.MAX_SCENE_SIZE + SceneLoadingMemoryConstants.MAX_SCENE_LOD, 10 * SceneLoadingMemoryConstants.MAX_SCENE_LOWQUALITY_LOD) },

            // 3 scenes, 5 high quality LODs, 30 low quality LODs. Limit: 1925MB
            { SceneLimitsKey.MEDIUM_MEMORY, new SceneLimits((3 * SceneLoadingMemoryConstants.MAX_SCENE_SIZE) + (5 * SceneLoadingMemoryConstants.MAX_SCENE_LOD), 30 * SceneLoadingMemoryConstants.MAX_SCENE_LOWQUALITY_LOD) },

            // No limits.
            { SceneLimitsKey.MAX_MEMORY, new SceneLimits(float.MaxValue, float.MaxValue) },

            // 1 scene, 1 high quality LOD. Could be useful for debugging single scenes
            { SceneLimitsKey.WARNING, new SceneLimits(1, 5 * SceneLoadingMemoryConstants.MAX_SCENE_LOWQUALITY_LOD) },
        };


        private float SceneCurrentMemoryUsageInMB;
        private float QualityReductedLODCurrentMemoryUsageInMB;

        private SceneLimits currentSceneLimits;
        private SceneLimitsKey initialKey;
        private bool ReducedLimitDueToMemory;


        private readonly ISystemMemoryCap systemMemoryCap;
        private bool isEnabled;


        public SceneLoadingLimit(ISystemMemoryCap memoryCap)
        {
            systemMemoryCap = memoryCap;
            isEnabled = false;
            UpdateMemoryCap();
        }

        public void ResetCurrentUsage()
        {
            SceneCurrentMemoryUsageInMB = 0;
            QualityReductedLODCurrentMemoryUsageInMB = 0;
        }

        public bool CanLoadScene(SceneDefinitionComponent sceneDefinitionComponent)
        {
            //We will let it overflow, only once. Avoid deadlock
            if (SceneCurrentMemoryUsageInMB < currentSceneLimits.SceneMaxAmountOfUsableMemoryInMB)
            {
                SceneCurrentMemoryUsageInMB += sceneDefinitionComponent.EstimatedMemoryUsageInMB;
                return true;
            }

            return false;
        }

        public bool CanLoadLOD(SceneDefinitionComponent sceneDefinitionComponent)
        {
            //We will let it overflow, only once. Avoid deadlock
            if (SceneCurrentMemoryUsageInMB < currentSceneLimits.SceneMaxAmountOfUsableMemoryInMB)
            {
                SceneCurrentMemoryUsageInMB += sceneDefinitionComponent.EstimatedMemoryUsageForLODMB;
                return true;
            }

            return false;
        }

        public bool CanLoadQualityReductedLOD(SceneDefinitionComponent sceneDefinitionComponent)
        {
            //We will let it overflow, only once. Avoid deadlock
            if (QualityReductedLODCurrentMemoryUsageInMB < currentSceneLimits.QualityReductedLODMaxAmountOfUsableMemoryInMB)
            {
                QualityReductedLODCurrentMemoryUsageInMB += sceneDefinitionComponent.EstimatedMemoryUsageForQualityReductedLODMB;
                return true;
            }

            return false;
        }

        public void UpdateMemoryCap()
        {
            if (isEnabled)
            {
                if (systemMemoryCap.MemoryCapInMB < 10_000)
                    initialKey = SceneLimitsKey.LOW_MEMORY;
                else if (systemMemoryCap.MemoryCapInMB < 17_000)
                    initialKey = SceneLimitsKey.MEDIUM_MEMORY;
                else
                    initialKey = SceneLimitsKey.MAX_MEMORY;
            }
            else
                initialKey = SceneLimitsKey.MAX_MEMORY;

            currentSceneLimits = sceneLimits[initialKey];
        }


        public void WarnAbundance(bool isAbunding)
        {
            if (!isEnabled)
                return;

            if (ReducedLimitDueToMemory && isAbunding)
            {
                ReducedLimitDueToMemory = false;
                easingCancellationTokenSource = easingCancellationTokenSource.SafeRestart();
                EaseSceneLimits(easingCancellationTokenSource.Token, currentSceneLimits, sceneLimits[initialKey]).Forget();
            }
        }

        public void IsInMemoryWarning(bool isInMemoryWarning)
        {
            if (!isEnabled)
                return;

            if (!ReducedLimitDueToMemory && isInMemoryWarning)
            {
                ReducedLimitDueToMemory = true;
                easingCancellationTokenSource = easingCancellationTokenSource.SafeRestart();
                EaseSceneLimits(easingCancellationTokenSource.Token, currentSceneLimits, sceneLimits[SceneLimitsKey.WARNING]).Forget();
            }
        }

        private CancellationTokenSource easingCancellationTokenSource;
        private readonly float totalFramesToComplete = 500;

        private async UniTask EaseSceneLimits(CancellationToken cancellationToken, SceneLimits start, SceneLimits end)
        {
            float currentFrames = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                float interpolationProgress = Mathf.Lerp(0, 1, currentFrames / totalFramesToComplete);
                currentSceneLimits = SceneLimits.Lerp(start, end, interpolationProgress);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

                if (currentFrames / totalFramesToComplete >= 1f)
                    break;
                currentFrames++;
            }
        }


        public void SetEnabled(bool isEnabled)
        {
            this.isEnabled = isEnabled;
            UpdateMemoryCap();
        }

        private enum SceneLimitsKey
        {
            WARNING,
            LOW_MEMORY,
            MEDIUM_MEMORY,
            MAX_MEMORY,
        }

        private struct SceneLimits
        {
            public readonly float SceneMaxAmountOfUsableMemoryInMB;
            public readonly float QualityReductedLODMaxAmountOfUsableMemoryInMB;

            public SceneLimits(float sceneMaxAmountOfUsableMemoryInMB, float qualityReductedLODMaxAmountOfUsableMemoryInMB)
            {
                SceneMaxAmountOfUsableMemoryInMB = sceneMaxAmountOfUsableMemoryInMB;
                QualityReductedLODMaxAmountOfUsableMemoryInMB = qualityReductedLODMaxAmountOfUsableMemoryInMB;
            }

            public static SceneLimits Lerp(SceneLimits a, SceneLimits b, float t) =>
                new (
                    Mathf.Lerp(a.SceneMaxAmountOfUsableMemoryInMB, b.SceneMaxAmountOfUsableMemoryInMB, t),
                    Mathf.Lerp(a.QualityReductedLODMaxAmountOfUsableMemoryInMB, b.QualityReductedLODMaxAmountOfUsableMemoryInMB, t)
                );
        }
    }
}
