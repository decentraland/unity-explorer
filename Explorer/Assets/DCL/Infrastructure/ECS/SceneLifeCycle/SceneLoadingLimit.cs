using DCL.Optimization.PerformanceBudgeting;
using ECS.SceneLifeCycle.SceneDefinition;
using System.Collections.Generic;

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
            { SceneLimitsKey.LOW_MEMORY, new SceneLimits(SceneLoadingMemoryConstants.MAX_SCENE_SIZE, SceneLoadingMemoryConstants.MAX_SCENE_LOD, 10 * SceneLoadingMemoryConstants.MAX_SCENE_LOWQUALITY_LOD) },

            // 3 scenes, 5 high quality LODs, 30 low quality LODs. Limit: 1925MB
            { SceneLimitsKey.MEDIUM_MEMORY, new SceneLimits(3 * SceneLoadingMemoryConstants.MAX_SCENE_SIZE, 5 * SceneLoadingMemoryConstants.MAX_SCENE_LOD, 30 * SceneLoadingMemoryConstants.MAX_SCENE_LOWQUALITY_LOD) },

            // No limits.
            { SceneLimitsKey.MAX_MEMORY, new SceneLimits(float.MaxValue, float.MaxValue, float.MaxValue) },

            // 1 scene, 1 high quality LOD. Only for debugging purposes
            { SceneLimitsKey.WARNING, new SceneLimits(1, 0, 10 * SceneLoadingMemoryConstants.MAX_SCENE_LOWQUALITY_LOD) },

        };


        private float SceneCurrentMemoryUsageInMB;
        private float LODCurrentMemoryUsageInMB;
        private float QualityReductedLODCurrentMemoryUsageInMB;

        private SceneLimits currentSceneLimits;
        private SceneLimitsKey currentKey;

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
                if (systemMemoryCap.MemoryCapInMB < 10_000)
                {
                    currentKey = SceneLimitsKey.LOW_MEMORY;
                    currentSceneLimits = sceneLimits[SceneLimitsKey.LOW_MEMORY];
                }
                else if (systemMemoryCap.MemoryCapInMB < 16_000)
                {
                    currentKey = SceneLimitsKey.MEDIUM_MEMORY;
                    currentSceneLimits = sceneLimits[SceneLimitsKey.MEDIUM_MEMORY];
                }
                else
                {
                    currentKey = SceneLimitsKey.MAX_MEMORY;
                    currentSceneLimits = sceneLimits[SceneLimitsKey.MAX_MEMORY];
                }
            }
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
            public readonly float LODMaxAmountOfUsableMemoryInMB;
            public readonly float QualityReductedLODMaxAmountOfUsableMemoryInMB;

            public SceneLimits(float sceneMaxAmountOfUsableMemoryInMB, float lodMaxAmountOfUsableMemoryInMB, float qualityReductedLODMaxAmountOfUsableMemoryInMB)
            {
                SceneMaxAmountOfUsableMemoryInMB = sceneMaxAmountOfUsableMemoryInMB;
                LODMaxAmountOfUsableMemoryInMB = lodMaxAmountOfUsableMemoryInMB;
                QualityReductedLODMaxAmountOfUsableMemoryInMB = qualityReductedLODMaxAmountOfUsableMemoryInMB;
            }
        }

        public void WarnMemoryFull(bool isInMemoryWarning)
        {
            //If we are in memory full, we must only load one scene
            if (isInMemoryWarning)
                currentSceneLimits = sceneLimits[SceneLimitsKey.WARNING];
            else
                currentSceneLimits = sceneLimits[currentKey];
        }
    }
}
