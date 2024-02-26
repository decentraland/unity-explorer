using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.Multiplayer.Movement.ECS
{
    public class MessagePipeSettings : ScriptableObject, IMultiplayerSpatialStateSettings
    {
        public List<SendRuleBase> SendRules { get; set; }
        public float MinAnimPackageTime { get; set; }
        public float MinPositionPackageTime { get; set; }
        public float MaxSentDelay { get; set; }
        public int MoveBlendTiersDiff { get; set; }
        public float MinSlideBlendDiff { get; set; }
        public float VelocityCosAngleChangeThreshold { get; set; }
        public float VelocityChangeThreshold { get; set; }
        public float PositionChangeThreshold { get; set; }
        public float ProjVelocityChangeThreshold { get; set; }
        public float ProjPositionChangeThreshold { get; set; }
        public float WalkSqrSpeed { get; set; }
        public float WalkSentRate { get; set; }
        public float RunSqrSpeed { get; set; }
        public float RunSentRate { get; set; }
        public float SprintSqrSpeed { get; set; }
        public float SprintSentRate { get; set; }

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
        public MovementKind LastMove { get; set; }
        public bool LastJump { get; set; }
        public float TimeScale { get; set; }
        public int SamePositionTeleportFilterCount { get; set; }
    }
}
