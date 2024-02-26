using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.MessageBusMock;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.Multiplayer.Movement.Settings
{
    public interface IMultiplayerSpatialStateSettings
    {
        List<SendRuleBase> SendRules { get; set; }

        int InboxCount { get; set; }
        int PassedMessages { get; set; }
        int PackageLost { get; set; }
        bool StartSending { get; set; }
        float PackagesJitter { get; set; }
        float Latency { get; set; }
        float LatencyJitter { get; set; }
        float MinPositionDelta { get; set; }
        float MinTeleportDistance { get; set; }
        InterpolationType InterpolationType { get; set; }
        float SpeedUpFactor { get; set; }
        bool useBlend { get; set; }
        InterpolationType BlendType { get; set; }
        float MaxBlendSpeed { get; set; }
        bool useExtrapolation { get; set; }
        float MinSpeed { get; set; }
        float LinearTime { get; set; }
        int DampedSteps { get; set; }
        InputAction startButton { get; set; }
        InputAction packageLostButton { get; set; }
        InputAction packageBlockButton { get; set; }
        MovementKind LastMove { get; set; }
        bool LastJump { get; set; }

        float TimeScale { get; set; }
        int SamePositionTeleportFilterCount { get; set; }
    }

    public class MultiplayerSpatialStateSettings : ScriptableObject, IMultiplayerSpatialStateSettings
    {
        [field: SerializeField] public float TimeScale { get; set; }
        [field: SerializeField] public int SamePositionTeleportFilterCount { get; set; }
        [field: SerializeField] public int InboxCount { get; set; }
        [field: SerializeField] public int PassedMessages { get; set; }
        [field: SerializeField] public int PackageLost { get; set; }
        [field: SerializeField] public bool StartSending { get; set; }

        [field: Header("SENDING RULES")]
        [field: SerializeField] public List<SendRuleBase> SendRules { get; set; }

        [field: Header("NETWORK")]
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

        [field: Header("CONTROLS")]
        [field: SerializeField] public InputAction startButton { get; set; }
        [field: SerializeField] public InputAction packageLostButton { get; set; }
        [field: SerializeField] public InputAction packageBlockButton { get; set; }
        public MovementKind LastMove { get; set; }
        public bool LastJump { get; set; }
    }
}
