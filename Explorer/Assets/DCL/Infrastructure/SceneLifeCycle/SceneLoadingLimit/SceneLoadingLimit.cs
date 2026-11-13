using DCL.Optimization.PerformanceBudgeting;
using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;

namespace ECS.SceneLifeCycle.IncreasingRadius
{


    public class SceneLoadingLimit
    {

        // 1 scene, 1 high quality LOD, 10 low quality LODs. Limit: 561MB
        private static readonly SceneLimits LOW_MEMORY_LIMITS = new (SceneLoadingMemoryConstants.MAX_SCENE_SIZE + SceneLoadingMemoryConstants.MAX_SCENE_LOD, 10 * SceneLoadingMemoryConstants.MAX_SCENE_LOWQUALITY_LOD);

        // 3 scenes, 5 high quality LODs, 30 low quality LODs. Limit: 1925MB
        private static readonly SceneLimits MEDIUM_MEMORY_LIMITS = new ((3 * SceneLoadingMemoryConstants.MAX_SCENE_SIZE) + (5 * SceneLoadingMemoryConstants.MAX_SCENE_LOD), 30 * SceneLoadingMemoryConstants.MAX_SCENE_LOWQUALITY_LOD);

        // No limits.
        private static readonly SceneLimits MAX_MEMORY_LIMITS = new (float.MaxValue, float.MaxValue);

        // 1 scene, 1 high quality LOD. Could be useful for debugging single scenes
        private static readonly SceneLimits WARNING_LIMITS = new (1, 5 * SceneLoadingMemoryConstants.MAX_SCENE_LOWQUALITY_LOD);

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
            sceneTransitionState = SceneTransitionState.Normal;
            systemMemoryCap = memoryCap;

            initialKey = SceneLimitsKey.MaxMemory;
            currentSceneLimits = GetSceneLimits(initialKey);
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
                return;

            if (systemMemoryCap.MemoryCapInMB < SceneLoadingMemoryConstants.LOW_MEMORY_RIG_THRESHOLD)
                initialKey = SceneLimitsKey.LowMemory;
            else if (systemMemoryCap.MemoryCapInMB < SceneLoadingMemoryConstants.MEDIUM_MEMORY_RIGH_THRESHOLD)
                initialKey = SceneLimitsKey.MediumMemory;
            else
                initialKey = SceneLimitsKey.MaxMemory;

            //We reset any possible transition and let it re-acomodate again
            currentSceneLimits = GetSceneLimits(initialKey);
            sceneTransitionState = SceneTransitionState.Normal;
            currentTransitionFrames = 0;
        }


        public void ReportMemoryState(bool isMemoryNormal, bool isAbundance)
        {
            if (!isEnabled)
                return;

            if (!isMemoryNormal)
            {
                if (sceneTransitionState is SceneTransitionState.Normal or SceneTransitionState.TransitioningToNormal)
                {
                    currentTransitionFrames = 0;
                    sceneTransitionState = SceneTransitionState.TransitioningToReduced;
                    transitionStartSceneLimits = currentSceneLimits;
                }

                if (sceneTransitionState == SceneTransitionState.TransitioningToReduced)
                {
                    currentTransitionFrames++;
                    float interpolationProgress = Mathf.Lerp(0, 1, currentTransitionFrames / totalFramesToComplete);
                    currentSceneLimits = SceneLimits.Lerp(transitionStartSceneLimits, WARNING_LIMITS, interpolationProgress);

                    if (currentTransitionFrames >= totalFramesToComplete)
                    {
                        sceneTransitionState = SceneTransitionState.Reduced;
                        currentSceneLimits = WARNING_LIMITS;
                    }
                }
            }

            if (isAbundance)
            {
                if (sceneTransitionState is SceneTransitionState.Reduced or SceneTransitionState.TransitioningToReduced)
                {
                    currentTransitionFrames = 0;
                    sceneTransitionState = SceneTransitionState.TransitioningToNormal;
                    transitionStartSceneLimits = currentSceneLimits;
                }

                if (sceneTransitionState == SceneTransitionState.TransitioningToNormal)
                {
                    currentTransitionFrames++;
                    float interpolationProgress = Mathf.Lerp(0, 1, currentTransitionFrames / totalFramesToComplete);
                    currentSceneLimits = SceneLimits.Lerp(transitionStartSceneLimits, GetSceneLimits(initialKey), interpolationProgress);

                    if (currentTransitionFrames >= totalFramesToComplete)
                    {
                        sceneTransitionState = SceneTransitionState.Normal;
                        currentSceneLimits = GetSceneLimits(initialKey);
                    }
                }
            }
        }


        public void SetEnabled(bool isEnabled)
        {
            this.isEnabled = isEnabled;
            UpdateMemoryCap();
        }

        private static SceneLimits GetSceneLimits(SceneLimitsKey key) =>
            key switch
            {
                SceneLimitsKey.Warning => WARNING_LIMITS,
                SceneLimitsKey.LowMemory => LOW_MEMORY_LIMITS,
                SceneLimitsKey.MediumMemory => MEDIUM_MEMORY_LIMITS,
                _ => MAX_MEMORY_LIMITS,
            };

    }
}
