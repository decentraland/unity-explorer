using UnityEngine;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    public struct SceneLimits
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
