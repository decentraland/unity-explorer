using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public struct PlayerMovementNetworkComponent
    {
        public readonly CharacterController Character;

        public bool IsFirstMessage;
        public FullMovementMessage LastSentMessage;

        public PlayerMovementNetworkComponent(CharacterController character)
        {
            Character = character;
            IsFirstMessage = true;
            LastSentMessage = new FullMovementMessage();
        }
    }
}
