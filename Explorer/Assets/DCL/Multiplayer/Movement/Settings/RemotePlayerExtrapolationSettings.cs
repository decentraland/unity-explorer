using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class RemotePlayerExtrapolationSettings : ScriptableObject
    {
        [field: SerializeField] public float MinSpeed { get; set; } = 0.01f;
        [field: SerializeField] public float LinearTime { get; set; } = 0.33f;
        [field: SerializeField] public int DampedSteps { get; set; } = 1;

        public float TotalMoveDuration => LinearTime + (LinearTime * DampedSteps);
    }
}
