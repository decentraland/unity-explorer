using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class RemotePlayerExtrapolationSettings : ScriptableObject
    {
        [field: Min(0)]
        [field: Tooltip("Minimal movement speed for extrapolation to start. If player has speed below this value, then he won't be moved.")]
        [field: SerializeField] public float MinSpeed { get; set; } = 0.01f;

        [field: Min(0)]
        [field: Tooltip("Time in seconds when extrapolation goes with initial (last) speed. No velocity damping applied.")]
        [field: SerializeField] public float LinearTime { get; set; } = 0.33f;

        [field: Min(0)]
        [field: Tooltip("Damping is applied after LinearTime passed. Damping time is defined as LinearTime * DampedSteps.")]
        [field: SerializeField] public int DampedSteps { get; set; } = 1;

        public float TotalMoveDuration => LinearTime + (LinearTime * DampedSteps);
    }
}
