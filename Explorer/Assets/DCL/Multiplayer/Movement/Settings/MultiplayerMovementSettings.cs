using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class MultiplayerMovementSettings : ScriptableObject, IMultiplayerMovementSettings
    {
        [field: SerializeField] public int InboxCount { get; set; }

        [field: Header("SENDING RULES")]
        [field: SerializeField] public List<SendRuleBase> SendRules { get; set; }

        [field: Header("TEST NETWORK")]
        [field: SerializeField] public bool SelfSending { get; set; } = false;

        [field: Min(0)]
        [field: SerializeField] public float Latency { get; set; } = 0.1f;

        [field: Min(0)]
        [field: SerializeField] public float LatencyJitter { get; set; } = 10;

        [field: Header("TELEPORTATION")]
        [field: Min(0)]
        [field: Tooltip("Minimal position (sqr) delta to consider a new position. "
                        + "If delta is less then this value, then player will just teleport to this position without any transition.")]
        [field: SerializeField] public float MinPositionDelta { get; set; } = 0.001f;

        [field: Min(0)]
        [field: Tooltip("Minimal distance after which player will be teleported instead of interpolated. "
                        + "If distance is more then this value, then player will just teleport to this position without any transition.")]
        [field: SerializeField] public float MinTeleportDistance { get; set; } = 50f;

        [field: Space]
        [field: SerializeField] public RemotePlayerInterpolationSettings InterpolationSettings { get; set; }

        [field: Header("EXTRAPOLATION")]
        [field: SerializeField] public bool UseExtrapolation { get; set; } = true;
        [field: SerializeField] public RemotePlayerExtrapolationSettings ExtrapolationSettings { get; set; }
    }
}
