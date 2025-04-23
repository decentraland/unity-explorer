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


    public class SceneLoadingLimit
    {

        private readonly Dictionary<SceneLimitsKey, SceneLimits> constantSceneLimits = new ()
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


        public SceneLimits currentSceneLimits { get; private set; }

        private float SceneCurrentMemoryUsageInMB;
        private float QualityReductedLODCurrentMemoryUsageInMB;
        private SceneLimitsKey initialKey;
        private SceneTransitionState sceneTransitionState;
        private readonly ISystemMemoryCap systemMemoryCap;
        private bool isEnabled;


        public SceneLoadingLimit(ISystemMemoryCap memoryCap)
        {
            sceneTransitionState = SceneTransitionState.NORMAL;
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
                if (systemMemoryCap.MemoryCapInMB < SceneLoadingMemoryConstants.LOW_MEMORY_RIG_THRESHOLD)
                    initialKey = SceneLimitsKey.LOW_MEMORY;
                else if (systemMemoryCap.MemoryCapInMB < SceneLoadingMemoryConstants.MEDIUM_MEMORY_RIGH_THRESHOLD)
                    initialKey = SceneLimitsKey.MEDIUM_MEMORY;
                else
                    initialKey = SceneLimitsKey.MAX_MEMORY;
            }
            else
                initialKey = SceneLimitsKey.MAX_MEMORY;

            currentSceneLimits = constantSceneLimits[initialKey];
        }


        public void ReportMemoryState(bool isMemoryNormal, bool isAbundance)
        {
            if (!isEnabled)
                return;

            if (!isMemoryNormal && sceneTransitionState is SceneTransitionState.NORMAL or SceneTransitionState.TRANSITIONING_TO_NORMAL)
            {
                easingCancellationTokenSource = easingCancellationTokenSource.SafeRestart();
                EaseSceneLimitsAsync(easingCancellationTokenSource.Token, currentSceneLimits, constantSceneLimits[SceneLimitsKey.WARNING], SceneTransitionState.REDUCED).Forget();
                sceneTransitionState = SceneTransitionState.TRANSITIONING_TO_REDUCED;
            }

            if (isMemoryNormal && sceneTransitionState == SceneTransitionState.TRANSITIONING_TO_REDUCED)
                easingCancellationTokenSource.Cancel();

            if (isMemoryNormal && isAbundance && sceneTransitionState is SceneTransitionState.REDUCED or SceneTransitionState.TRANSITIONING_TO_REDUCED or SceneTransitionState.TRANSITIONING_TO_NORMAL)
            {
                easingCancellationTokenSource = easingCancellationTokenSource.SafeRestart();
                EaseSceneLimitsAsync(easingCancellationTokenSource.Token, currentSceneLimits, constantSceneLimits[initialKey], SceneTransitionState.NORMAL).Forget();
                sceneTransitionState = SceneTransitionState.TRANSITIONING_TO_NORMAL;
            }

            if (isMemoryNormal && !isAbundance && sceneTransitionState == SceneTransitionState.TRANSITIONING_TO_NORMAL)
                easingCancellationTokenSource.Cancel();
        }


        private CancellationTokenSource easingCancellationTokenSource;
        private readonly float totalFramesToComplete = 500;

        private async UniTask EaseSceneLimitsAsync(CancellationToken cancellationToken, SceneLimits start, SceneLimits end, SceneTransitionState finalState)
        {
            float currentFrames = 0;
            while (!cancellationToken.IsCancellationRequested)
            {

                float interpolationProgress = Mathf.Lerp(0, 1, currentFrames / totalFramesToComplete);
                currentSceneLimits = SceneLimits.Lerp(start, end, interpolationProgress);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                currentFrames++;
                if (currentFrames / totalFramesToComplete >= 1f)
                    break;
            }
            sceneTransitionState = finalState;
        }


        public void SetEnabled(bool isEnabled)
        {
            this.isEnabled = isEnabled;
            UpdateMemoryCap();
        }







    }
}
