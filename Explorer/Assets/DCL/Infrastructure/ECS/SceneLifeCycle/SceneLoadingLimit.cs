using DCL.FeatureFlags;
using DCL.Optimization.PerformanceBudgeting;
using ECS.SceneLifeCycle.SceneDefinition;
using System;
using System.Runtime.CompilerServices;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    public class SceneLoadingLimit
    {
        private float SceneCurrentMemoryUsageInMB;
        private float LODCurrentMemoryUsageInMB;
        private float QualityReductedLODCurrentMemoryUsageInMB;

        //1GB for scene usage
        private float SceneMaxAmountOfUsableMemoryInMB = 300;
        private float LODMaxAmountOfUsableMemoryInMB = 100;
        private float QualityReductedLODMaxAmountOfUsableMemoryInMB = 100;

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

        public void UpdateMemoryCap()
        {
            if (!isEnabled)
                SetMax();
            else
            {
                if (systemMemoryCap.MemoryCapInMB < 16_000)
                {
                    //Put limits under 16GB
                }
                else
                    SetMax();
            }
        }

        private void SetMax()
        {
            SceneMaxAmountOfUsableMemoryInMB = float.MaxValue;
            LODMaxAmountOfUsableMemoryInMB = float.MaxValue;
            QualityReductedLODMaxAmountOfUsableMemoryInMB = float.MaxValue;
        }
    }
}
