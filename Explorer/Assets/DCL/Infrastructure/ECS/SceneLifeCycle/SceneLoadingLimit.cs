using DCL.FeatureFlags;
using DCL.Optimization.PerformanceBudgeting;
using ECS.SceneLifeCycle.SceneDefinition;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    public class SceneLoadingLimit
    {
        // Reference for worst case scenarios (Using Genesis Plaza as reference):
        // A single scene can take 330MB
        // A single high quality LOD can take 44MB
        // A single low quality LOD can take 11MB
        // To check where this values are assigned, look at SceneDefinitionComponent.cs
        // The following values take into consideration the 'worst scenarios'. Since all scenes dont take do the worst scenario, more will be loaded. This just ensures the upper limit
        private readonly Dictionary<SceneLimitsKey, SceneLimits> sceneLimits = new ()
        {
            // 1 scene, 1 high quality LOD, 10 low quality LODs. Limit: 440MB
            { SceneLimitsKey.LOW_MEMORY, new SceneLimits(330, 11, 110) },

            // 3 scenes, 10 high quality LODs, 10 low quality LODs. Limit: 1890MB
            { SceneLimitsKey.MEDIUM_MEMORY, new SceneLimits(990, 450, 450) },

            // No limits.
            { SceneLimitsKey.MAX_MEMORY, new SceneLimits(float.MaxValue, float.MaxValue, float.MaxValue) },

            // 1 scene, 1 high quality LOD. Only for debugging purposes
            { SceneLimitsKey.SINGLE_SCENE, new SceneLimits(1, 1, 0) },

        };


        private float SceneCurrentMemoryUsageInMB;
        private float LODCurrentMemoryUsageInMB;
        private float QualityReductedLODCurrentMemoryUsageInMB;

        private SceneLimits currentSceneLimits;

        private readonly ISystemMemoryCap systemMemoryCap;
        private readonly bool isEnabled;

        public SceneLoadingLimit(ISystemMemoryCap memoryCap, bool isEnabled)
        {
            systemMemoryCap = memoryCap;
            this.isEnabled = isEnabled;
            UpdateMemoryCap();
        }

        public void ResetCurrentUsage()
        {
            SceneCurrentMemoryUsageInMB = 0;
            LODCurrentMemoryUsageInMB = 0;
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
            if (LODCurrentMemoryUsageInMB < currentSceneLimits.LODMaxAmountOfUsableMemoryInMB)
            {
                LODCurrentMemoryUsageInMB += sceneDefinitionComponent.EstimatedMemoryUsageForLODMB;
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
            if (!isEnabled)
                currentSceneLimits = sceneLimits[SceneLimitsKey.MAX_MEMORY];
            else
            {
                if (systemMemoryCap.MemoryCapInMB < 8_000)
                    currentSceneLimits = sceneLimits[SceneLimitsKey.LOW_MEMORY];
                else if (systemMemoryCap.MemoryCapInMB < 16_000)
                    currentSceneLimits = sceneLimits[SceneLimitsKey.MEDIUM_MEMORY];
                else
                    currentSceneLimits = sceneLimits[SceneLimitsKey.MAX_MEMORY];
            }
        }

        private enum SceneLimitsKey
        {
            SINGLE_SCENE,
            LOW_MEMORY,
            MEDIUM_MEMORY,
            MAX_MEMORY,
        }

        private struct SceneLimits
        {
            public readonly float SceneMaxAmountOfUsableMemoryInMB;
            public readonly float LODMaxAmountOfUsableMemoryInMB;
            public readonly float QualityReductedLODMaxAmountOfUsableMemoryInMB;

            public SceneLimits(float sceneMaxAmountOfUsableMemoryInMB, float lodMaxAmountOfUsableMemoryInMB, float qualityReductedLODMaxAmountOfUsableMemoryInMB)
            {
                SceneMaxAmountOfUsableMemoryInMB = sceneMaxAmountOfUsableMemoryInMB;
                LODMaxAmountOfUsableMemoryInMB = lodMaxAmountOfUsableMemoryInMB;
                QualityReductedLODMaxAmountOfUsableMemoryInMB = qualityReductedLODMaxAmountOfUsableMemoryInMB;
            }
        }
    }
}
