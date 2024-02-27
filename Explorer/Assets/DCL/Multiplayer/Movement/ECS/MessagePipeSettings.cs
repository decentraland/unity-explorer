using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace DCL.Multiplayer.Movement.ECS
{
    [CreateAssetMenu(fileName = "MessagePipeSettings", menuName = "DCL/MessagePipeSettings", order = 1)]
    public class MessagePipeSettings : ScriptableObject
    {
        public int InboxCount;
        public int PassedMessages;
        public int PackageLost;
        public bool StartSending;

        [Header("NETWORK")]
        public float PackageSentRate = 0.33f;
        public float PackagesJitter;
        public float Latency = 1f;
        public float LatencyJitter;

        [Header("TELEPORTATION")]
        public float MinPositionDelta = 0.1f;
        public float MinTeleportDistance = 50f;

        [Header("INTERPOLATION")]
        public InterpolationType InterpolationType;
        public float SpeedUpFactor = 1;
        public bool useBlend = true;
        public InterpolationType BlendType;
        public float MaxBlendSpeed = 30;

        [Header("EXTRAPOLATION")]
        public bool useExtrapolation = true;
        public float MinSpeed = 0.01f;
        public float LinearTime = 0.33f;
        public int DampedSteps = 1;

        [Space]
        public InputAction startButton;
        public InputAction packageLostButton;
        public InputAction packageBlockButton;
    }
}
