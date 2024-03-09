using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.MessageBusMock;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class MultiplayerMovementSettings : ScriptableObject, IMultiplayerMovementSettings
    {
        public MovementKind LastMove { get; set; }
        public bool LastJump { get; set; }

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
        [field: SerializeField] public RemotePlayerExtrapolationSettings ExtrapolationSettings { get; set; }
    }
}
