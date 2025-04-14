using ECS.SceneLifeCycle.SceneDefinition;
using System;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    public class SceneLoadingLimit
    {
        internal int MaximumAmountOfScenesThatCanLoad;
        internal int MaximumAmountOfReductedLoDsThatCanLoad;
        internal int MaximumAmountOfLODsThatCanLoad;

        private int SceneCurrentMemoryUsageInMB;
        private int LODCurrentMemoryUsageInMB;
        private int QualityReductedLODCurrentMemoryUsageInMB;

        //1GB for scene usage
        private readonly int SceneMaxAmountOfUsableMemoryInMB = 300;
        private readonly int LODMaxAmountOfUsableMemoryInMB = 100;
        private readonly int QualityReductedLODMaxAmountOfUsableMemoryInMB = 100;

        public void Reset()
        {
            SceneCurrentMemoryUsageInMB = 0;
            LODCurrentMemoryUsageInMB = 0;
            QualityReductedLODCurrentMemoryUsageInMB = 0;
        }

        public static SceneLoadingLimit CreateMax() =>
            new ()
            {
                MaximumAmountOfScenesThatCanLoad = int.MaxValue,
                MaximumAmountOfReductedLoDsThatCanLoad = int.MaxValue,
                MaximumAmountOfLODsThatCanLoad = int.MaxValue,
            };

        public bool CanLoadScene(SceneDefinitionComponent sceneDefinitionComponent)
        {
            //We will let it overflow, only once. Avoid deadlock
            if (SceneCurrentMemoryUsageInMB < SceneMaxAmountOfUsableMemoryInMB)
            {
                SceneCurrentMemoryUsageInMB += sceneDefinitionComponent.EstimatedMemoryUsageInMB;
                return true;
            }

            return false;
        }

        public bool CanLoadLOD(SceneDefinitionComponent sceneDefinitionComponent)
        {
            //We will let it overflow, only once. Avoid deadlock
            if (LODCurrentMemoryUsageInMB < LODMaxAmountOfUsableMemoryInMB)
            {
                LODCurrentMemoryUsageInMB += sceneDefinitionComponent.EstimatedMemoryUsageForLODMB;
                return true;
            }

            return false;
        }

        public bool CanLoadQualityReductedLOD(SceneDefinitionComponent sceneDefinitionComponent)
        {
            //We will let it overflow, only once. Avoid deadlock
            if (QualityReductedLODCurrentMemoryUsageInMB < QualityReductedLODMaxAmountOfUsableMemoryInMB)
            {
                QualityReductedLODCurrentMemoryUsageInMB += sceneDefinitionComponent.EstimatedMemoryUsageForQualityReductedLODMB;
                return true;
            }

            return false;
        }
    }
}
