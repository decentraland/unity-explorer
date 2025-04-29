using System;
using UnityEngine;

namespace DCL.Optimization.AdaptivePerformance.Systems
{
    [Serializable]
    public enum PhysSimulationMode
    {
        DEFAULT, // [Fixed time step] Unity default approach - Physics.Simulate called in FixedUpdate with fixedDeltaTime (0.02 sec by default)
        ADAPTIVE, // [Semi-fixed time step] We adjust fixedDeltaTime to the median frame rate
        MANUAL, // [Variable time step] We call Physics.Simulate manually in FixedUpdate with the Time.deltaTime, ensuring to have only one call per frame
    }

    [CreateAssetMenu(fileName = "AdaptivePhysicsSettings", menuName = "DCL/AdaptivePhysicsSettings", order = 0)]
    public class AdaptivePhysicsSettings : ScriptableObject
    {
        [field: Tooltip("Select which physics simulation mode to use:\n" +
                        "DEFAULT: Uses Unity's default fixed step approach.\n" +
                        "ADAPTIVE: Dynamically adjusts fixedDeltaTime based on the median frame rate.\n" +
                        "MANUAL: Simulates physics manually in each frame.")]
        [field: SerializeField] internal PhysSimulationMode Mode = PhysSimulationMode.MANUAL;

        [field: Header("Fixed DeltaTime Clamp")]
        [field: Tooltip("The minimum value that Time.fixedDeltaTime can be clamped to, preventing the simulation from running too frequently.")]
        [field: SerializeField] internal float minFixedDelta = 0.02f;
        [field: Tooltip("The maximum value that Time.fixedDeltaTime can be clamped to, preventing the simulation from slowing down excessively.")]
        [field: SerializeField] internal float maxFixedDelta = 0.05f;

        [field: Header("Update Criteria")]
        [field: Tooltip("The minimum interval (in seconds) before another Time.fixedDeltaTime adjustment can occur, preventing overly frequent changes.")]
        [field: SerializeField] internal float changeCooldown = 1; // sec
        [field: Tooltip("The minimum number of frame time samples required before Time.fixedDeltaTime adjustments are considered, ensuring enough data is collected.")]
        [field: SerializeField] internal float minFrameTimeAmount = 30;

        [field: Header("Adaptive Change Buffer Range")]
        [field: Tooltip("How much (in ms) the median frame time can exceed the current Time.fixedDeltaTime before it’s considered \"too high\" and triggers a recalculation. Larger values allow more fluctuation before recalculation.")]
        [field: SerializeField] internal float topOffset = 6.5f;
        [field: Tooltip("How much (in ms) is subtracted from the median frame time to buffer small fluctuations, so that fixedDeltaTime isn't frequently adjusted for minor changes.")]
        [field: SerializeField] internal float bottomOffset = 3f;
    }
}
