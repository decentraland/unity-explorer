using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.Multiplayer.Movement.Settings
{
    public interface IMultiplayerSpatialStateSettings
    {
        float MinAnimPackageTime { get; set; }
        float MinPositionPackageTime { get; set; }
        float MaxSentDelay { get; set; }
        int MoveBlendTiersDiff { get; set; }
        float MinSlideBlendDiff { get; set; }
        float VelocityCosAngleChangeThreshold { get; set; }
        float VelocityChangeThreshold { get; set; }
        float PositionChangeThreshold { get; set; }

        float ProjVelocityChangeThreshold { get; set; }
        float ProjPositionChangeThreshold { get; set; }

        float WalkSqrSpeed { get; set; }

        float WalkSentRate { get; set; }

        float RunSqrSpeed { get; set; }

        float RunSentRate { get; set; }

        float SprintSqrSpeed { get; set; }

        float SprintSentRate { get; set; }

        /// Old settings
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
    }

    public class MultiplayerSpatialStateSettings : ScriptableObject, IMultiplayerSpatialStateSettings
    {

        [field: SerializeField] public int InboxCount { get; set; }
        [field: SerializeField] public int PassedMessages { get; set; }
        [field: SerializeField] public int PackageLost { get; set; }
        [field: SerializeField] public bool StartSending { get; set; }

        [field: Header("SENDING ANIM")]
        [field: SerializeField] public float MinAnimPackageTime { get; set; }
        [field: SerializeField] public int MoveBlendTiersDiff { get; set; }
        [field: SerializeField] public float MinSlideBlendDiff { get; set; }
        [field: Header("SENDING POSITION")]
        [field: SerializeField] public float MinPositionPackageTime { get; set; }
        [field: SerializeField] public float VelocityCosAngleChangeThreshold { get; set; }
        [field: SerializeField] public float VelocityChangeThreshold { get; set; }
        [field: SerializeField] public float PositionChangeThreshold { get; set; }
        [field: Header("SENDING PROJECTIVE")]

        [field: SerializeField] public float ProjVelocityChangeThreshold { get; set; }
        [field: SerializeField] public float ProjPositionChangeThreshold { get; set; }

        [field: Header("SENDING VELOCITY TIERS")]

        [field: SerializeField] public float WalkSqrSpeed { get; set; }
        [field: SerializeField] public float WalkSentRate { get; set; }
        [field: SerializeField] public float RunSqrSpeed { get; set; }
        [field: SerializeField] public float RunSentRate { get; set; }
        [field: SerializeField] public float SprintSqrSpeed { get; set; }
        [field: SerializeField] public float SprintSentRate { get; set; }
        [field: Header("NETWORK")]
        [field: SerializeField] public float MaxSentDelay { get; set; }
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
    }
}
