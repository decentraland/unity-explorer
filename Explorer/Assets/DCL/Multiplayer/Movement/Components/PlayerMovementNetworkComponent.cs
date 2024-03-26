using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public struct PlayerMovementNetworkComponent
    {
        public const int MAX_MESSAGES_PER_SEC = 10; // 10 Hz == 10 [msg/sec]

        public readonly CharacterController Character;

        public bool IsFirstMessage;
        public NetworkMovementMessage LastSentMessage;

        public int MessagesSentInSec;
        public float MessagesPerSecResetCooldown;

        public PlayerMovementNetworkComponent(CharacterController character)
        {
            Character = character;
            IsFirstMessage = true;
            LastSentMessage = new NetworkMovementMessage();

            MessagesSentInSec = 0;
            MessagesPerSecResetCooldown = 1;
        }
    }
}
