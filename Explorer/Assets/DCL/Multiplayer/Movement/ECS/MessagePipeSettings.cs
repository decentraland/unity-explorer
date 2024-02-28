using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.Multiplayer.Movement.ECS
{
    public class MessagePipeSettings : ScriptableObject, IMultiplayerSpatialStateSettings
    {
        [field: SerializeField] public int InboxCount { get; set; }
        [field: SerializeField] public int PassedMessages { get; set; }
        [field: SerializeField] public int PackageLost { get; set; }
        [field: SerializeField] public bool StartSending { get; set; }

        [field: Header("NETWORK")]
        [field: SerializeField] public float PackageSentRate { get; set; } = 0.33f;
        [field: SerializeField] public float PackagesJitter { get; set; }
        [field: SerializeField] public float Latency { get; set; } = 1f;
        [field: SerializeField] public float LatencyJitter { get; set; }

        [field: Header("TELEPORTATION")]
        [field: SerializeField] public float MinPositionDelta { get; set; } = 0.1f;
        [field: SerializeField] public float MinTeleportDistance { get; set; } = 50f;

        [field: Header("INTERPOLATION")]
        [field: SerializeField] public InterpolationType InterpolationType { get; set; }
        [field: SerializeField] public float SpeedUpFactor { get; set; } = 1;
        [field: SerializeField] public bool useBlend { get; set; } = true;
        [field: SerializeField] public InterpolationType BlendType { get; set; }
        [field: SerializeField] public float MaxBlendSpeed { get; set; } = 30;

        [field: Header("EXTRAPOLATION")]
        [field: SerializeField] public bool useExtrapolation { get; set; } = true;
        [field: SerializeField] public float MinSpeed { get; set; } = 0.01f;
        [field: SerializeField] public float LinearTime { get; set; } = 0.33f;
        [field: SerializeField] public int DampedSteps { get; set; } = 1;

        [field: Space]
        [field: SerializeField] public InputAction startButton { get; set; }
        [field: SerializeField] public InputAction packageLostButton { get; set; }
        [field: SerializeField] public InputAction packageBlockButton { get; set; }
    }
}
