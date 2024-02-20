using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.Multiplayer.Movement.ECS
{
    [CreateAssetMenu(fileName = "MessagePipeSettings", menuName = "DCL/MessagePipeSettings", order = 1)]
    public class MessagePipeSettings : ScriptableObject
    {
        public int InboxCount;

        [Space]
        public int PackageLost;
        public bool StartSending;

        [Space]
        public float PackageSentRate = 0.33f;
        public float PackagesJitter = 0f;

        public float Latency = 1f;
        public float LatencyJitter = 0f;

        [Space]
        public InputAction startButton;
        public InputAction packageLostButton;
        public InputAction packageBlockButton;
    }
}
