using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public struct PlayerMovementNetSendComponent
    {
        public readonly CharacterController Character;

        public bool IsFirstMessage;
        public FullMovementMessage LastSentMessage;

        public PlayerMovementNetSendComponent(CharacterController character)
        {
            Character = character;
            IsFirstMessage = true;
            LastSentMessage = new FullMovementMessage();
        }
    }
}
