using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class MultiplayerDebugSettings : ScriptableObject
    {
        [field: Header("TEST NETWORK")]
        [field: SerializeField] public bool SelfSending { get; set; }

        [field: Min(0)]
        [field: SerializeField] public float Latency { get; set; } = 0.1f;

        [field: Min(0)]
        [field: SerializeField] public float LatencyJitter { get; set; } = 10;
    }
}
