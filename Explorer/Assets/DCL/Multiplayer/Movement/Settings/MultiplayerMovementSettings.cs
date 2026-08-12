using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class MultiplayerMovementSettings : ScriptableObject, IMultiplayerMovementSettings
    {
                [field: SerializeField] public CharacterControllerSettings CharacterControllerSettings { get; set; } = null!;
        [field: SerializeField] public int InboxCount { get; set; }
        [field: SerializeField] public bool UseCompression { get; set; }

                [field: SerializeField] public MessageEncodingSettings EncodingSettings { get; set; } = null!;

        [field: Header("SENDING RULES")]
        [field: SerializeField] public float MoveSendRate { get; set; }
        [field: SerializeField] public float StandSendRate { get; set; }
                [field: SerializeField] public float[] VelocityTiers { get; set;} = null!;

                [field: SerializeField] public List<SendRuleBase> SendRules { get; set; } = null!;

        [field: Header("TELEPORTATION")]
        [field: Min(0)]
        [field: Tooltip("Minimal position (sqr) delta to consider a new position. "
                        + "If delta is less then this value, then player will just teleport to this position without any transition.")]
        [field: SerializeField] public float MinPositionDelta { get; set; } = 0.001f;
        [field: SerializeField] public float MinRotationDelta { get; set; } = 0.01f;

        [field: Min(0)]
        [field: Tooltip("Minimal distance after which player will be teleported instead of interpolated. "
                        + "If distance is more then this value, then player will just teleport to this position without any transition.")]
        [field: SerializeField] public float MinTeleportDistance { get; set; } = 50f;

        [field: Space]
                [field: SerializeField] public RemotePlayerInterpolationSettings InterpolationSettings { get; set; } = null!;

        [field: Header("EXTRAPOLATION")]
        [field: SerializeField] public bool UseExtrapolation { get; set; } = true;
                [field: SerializeField] public RemotePlayerExtrapolationSettings ExtrapolationSettings { get; set; } = null!;
        [field: SerializeField] public float AccelerationTimeThreshold { get; private set; }
        [field: SerializeField] public float IdleSlowDownSpeed { get; private set; }

        private static readonly IReadOnlyDictionary<MovementKind, float> MOVE_KIND_BY_DISTANCE =
            new ReadOnlyDictionary<MovementKind, float>(new Dictionary<MovementKind, float>
            {
                { MovementKind.Walk, 1f },
                { MovementKind.Jog, 2f },
            });

        public IReadOnlyDictionary<MovementKind, float> MoveKindByDistance => MOVE_KIND_BY_DISTANCE;
    }
}
