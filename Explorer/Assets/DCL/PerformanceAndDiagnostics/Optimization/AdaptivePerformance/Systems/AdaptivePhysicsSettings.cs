using System;
using UnityEngine;

namespace DCL.Optimization.AdaptivePerformance.Systems
{
    [Serializable]
    public enum PhysSimulationMode
    {
        DEFAULT,
        ADAPTIVE,
        MANUAL
    }


    [CreateAssetMenu(fileName = "AdaptivePhysicsSettings", menuName = "DCL/AdaptivePhysicsSettings", order = 0)]
    public class AdaptivePhysicsSettings : ScriptableObject
    {
        [field: SerializeField] internal PhysSimulationMode Mode = PhysSimulationMode.MANUAL;

        [field: Space]
        [field: SerializeField] internal float minFixedDelta = 0.02f;
        [field: SerializeField] internal float maxFixedDelta = 0.05f;

        [field: Space]
        [field: SerializeField] internal float changeCooldown = 1; // sec
        [field: SerializeField] internal float minFrameTimeAmount = 30;

        [field: Space]
        [field: SerializeField] internal float topOffset = 6.5f;
        [field: SerializeField] internal float bottomOffset = 3f;
    }
}
