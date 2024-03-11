using DCL.CharacterMotion.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class MultiplayerMovementSettings : ScriptableObject, IMultiplayerMovementSettings
    {
        public MovementKind LastMove { get; set; }
        public bool LastJump { get; set; }

        [field: SerializeField] public int InboxCount { get; set; }

        [field: Header("SENDING RULES")]
        [field: SerializeField] public List<SendRuleBase> SendRules { get; set; }

        [field: Header("NETWORK")]
        [field: SerializeField] public float Latency { get; set; } = 1f;
        [field: SerializeField] public float LatencyJitter { get; set; }

        [field: Header("TELEPORTATION")]
        [field: SerializeField] public float MinPositionDelta { get; set; } = 0.1f;
        [field: SerializeField] public float MinTeleportDistance { get; set; } = 50f;
        [field: SerializeField] public int SkipSamePositionBatch { get; set; }
        [field: SerializeField] public int SkipOldMessagesBatch { get; set; }

        [field: Space]
        [field: SerializeField] public RemotePlayerInterpolationSettings InterpolationSettings { get; set; }

        [field: Header("EXTRAPOLATION")]
        [field: SerializeField] public bool useExtrapolation { get; set; } = true;
        [field: SerializeField] public RemotePlayerExtrapolationSettings ExtrapolationSettings { get; set; }
    }
}
