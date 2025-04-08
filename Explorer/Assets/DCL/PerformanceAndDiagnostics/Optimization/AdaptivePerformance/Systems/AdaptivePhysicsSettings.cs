using UnityEngine;

namespace DCL.Optimization.AdaptivePerformance.Systems
{
    [CreateAssetMenu(fileName = "AdaptivePhysicsSettings", menuName = "DCL/AdaptivePhysicsSettings", order = 0)]
    public class AdaptivePhysicsSettings : ScriptableObject
    {
        [field: SerializeField] internal float alpha = 0.05f;

        [field: Space]
        [field: SerializeField] internal float bigDelta = 0.005f;
        [field: SerializeField] internal float smallDelta = 0.001f;

        [field: Space]
        [field: SerializeField] internal  float minFixedDelta = 0.01f;
        [field: SerializeField] internal  float maxFixedDelta = 0.05f;

        [field: Space]
        [field: SerializeField] internal  float highThreshold = 1.3f;
        [field: SerializeField] internal  float lowThreshold  = 1f;
    }
}
