using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public struct PlayerMovementNetworkComponent
    {
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
