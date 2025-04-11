using UnityEngine;

namespace DCL.Optimization.AdaptivePerformance.Systems
{
    [CreateAssetMenu(fileName = "AdaptivePhysicsSettings", menuName = "DCL/AdaptivePhysicsSettings", order = 0)]
    public class AdaptivePhysicsSettings : ScriptableObject
    {
        [field: SerializeField] internal bool isEnabled = true;

        [field: Space]
        [field: SerializeField] internal float minFixedDelta = 0.01f;
        [field: SerializeField] internal float maxFixedDelta = 0.05f;

        [field: Space]
        [field: SerializeField] internal float changeCooldown = 1;
        [field: SerializeField] internal float minFrameTimeAmount = 30;

        [field: Space]
        [field: SerializeField] internal float topOffset = 6.5f;
        [field: SerializeField] internal float bottomOffset = 3f;
    }
}
