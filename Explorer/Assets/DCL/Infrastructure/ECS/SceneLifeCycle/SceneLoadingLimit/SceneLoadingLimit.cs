using DCL.Optimization.PerformanceBudgeting;
using ECS.SceneLifeCycle.SceneDefinition;
using System.Collections.Generic;
using UnityEngine;

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

        //Initial setup
        private bool isEnabled;
        private readonly ISystemMemoryCap systemMemoryCap;
        private SceneLimitsKey initialKey;

        //Runtime evaluation usage
        private float sceneCurrentMemoryUsageInMB;
        private float qualityReductedLODCurrentMemoryUsageInMB;
        public SceneLimits currentSceneLimits { get; private set; }

        //Transition helpers
        private SceneTransitionState sceneTransitionState;
        private SceneLimits transitionStartSceneLimits;
        private int currentTransitionFrames;
        private readonly float totalFramesToComplete = 500;


        public SceneLoadingLimit(ISystemMemoryCap memoryCap)
        {
            sceneTransitionState = SceneTransitionState.NORMAL;
            systemMemoryCap = memoryCap;
            isEnabled = false;
            UpdateMemoryCap();
        }

        public void ResetCurrentUsage()
        {
            sceneCurrentMemoryUsageInMB = 0;
            qualityReductedLODCurrentMemoryUsageInMB = 0;
        }

        public bool CanLoadScene(SceneDefinitionComponent sceneDefinitionComponent)
        {
            //We will let it overflow, only once. Avoid deadlock
            if (sceneCurrentMemoryUsageInMB < currentSceneLimits.SceneMaxAmountOfUsableMemoryInMB)
            {
                sceneCurrentMemoryUsageInMB += sceneDefinitionComponent.EstimatedMemoryUsageInMB;
                return true;
            }

            return false;
        }

        public bool CanLoadLOD(SceneDefinitionComponent sceneDefinitionComponent)
        {
            //We will let it overflow, only once. Avoid deadlock
            if (sceneCurrentMemoryUsageInMB < currentSceneLimits.SceneMaxAmountOfUsableMemoryInMB)
            {
                sceneCurrentMemoryUsageInMB += sceneDefinitionComponent.EstimatedMemoryUsageForLODMB;
                return true;
            }

            return false;
        }

        public bool CanLoadQualityReductedLOD(SceneDefinitionComponent sceneDefinitionComponent)
        {
            //We will let it overflow, only once. Avoid deadlock
            if (qualityReductedLODCurrentMemoryUsageInMB < currentSceneLimits.QualityReductedLODMaxAmountOfUsableMemoryInMB)
            {
                qualityReductedLODCurrentMemoryUsageInMB += sceneDefinitionComponent.EstimatedMemoryUsageForQualityReductedLODMB;
                return true;
            }

            return false;
        }

        public void UpdateMemoryCap()
        {
            if (!isEnabled)
            {
                initialKey = SceneLimitsKey.MAX_MEMORY;
                return;
            }

            if (systemMemoryCap.MemoryCapInMB < SceneLoadingMemoryConstants.LOW_MEMORY_RIG_THRESHOLD)
                initialKey = SceneLimitsKey.LOW_MEMORY;
            else if (systemMemoryCap.MemoryCapInMB < SceneLoadingMemoryConstants.MEDIUM_MEMORY_RIGH_THRESHOLD)
                initialKey = SceneLimitsKey.MEDIUM_MEMORY;
            else
                initialKey = SceneLimitsKey.MAX_MEMORY;

            //We reset any possible transition and let it re-acomodate again
            currentSceneLimits = constantSceneLimits[initialKey];
            sceneTransitionState = SceneTransitionState.NORMAL;
            currentTransitionFrames = 0;
        }


        public void ReportMemoryState(bool isMemoryNormal, bool isAbundance)
        {
            if (!isEnabled)
                return;

            if (!isMemoryNormal)
            {
                if (sceneTransitionState is SceneTransitionState.NORMAL or SceneTransitionState.TRANSITIONING_TO_NORMAL)
                {
                    currentTransitionFrames = 0;
                    sceneTransitionState = SceneTransitionState.TRANSITIONING_TO_REDUCED;
                    transitionStartSceneLimits = currentSceneLimits;
                }

                if (sceneTransitionState == SceneTransitionState.TRANSITIONING_TO_REDUCED)
                {
                    currentTransitionFrames++;
                    float interpolationProgress = Mathf.Lerp(0, 1, currentTransitionFrames / totalFramesToComplete);
                    currentSceneLimits = SceneLimits.Lerp(transitionStartSceneLimits, constantSceneLimits[SceneLimitsKey.WARNING], interpolationProgress);

                    if (currentTransitionFrames >= totalFramesToComplete)
                    {
                        sceneTransitionState = SceneTransitionState.REDUCED;
                        currentSceneLimits = constantSceneLimits[SceneLimitsKey.WARNING];
                    }
                }
            }

            if (isAbundance)
            {
                if (sceneTransitionState is SceneTransitionState.REDUCED or SceneTransitionState.TRANSITIONING_TO_REDUCED)
                {
                    currentTransitionFrames = 0;
                    sceneTransitionState = SceneTransitionState.TRANSITIONING_TO_NORMAL;
                    transitionStartSceneLimits = currentSceneLimits;
                }

                if (sceneTransitionState == SceneTransitionState.TRANSITIONING_TO_NORMAL)
                {
                    currentTransitionFrames++;
                    float interpolationProgress = Mathf.Lerp(0, 1, currentTransitionFrames / totalFramesToComplete);
                    currentSceneLimits = SceneLimits.Lerp(transitionStartSceneLimits, constantSceneLimits[initialKey], interpolationProgress);

                    if (currentTransitionFrames >= totalFramesToComplete)
                    {
                        sceneTransitionState = SceneTransitionState.NORMAL;
                        currentSceneLimits = constantSceneLimits[initialKey];
                    }
                }
            }
        }


        public void SetEnabled(bool isEnabled)
        {
            this.isEnabled = isEnabled;
            UpdateMemoryCap();
        }

    }
}
