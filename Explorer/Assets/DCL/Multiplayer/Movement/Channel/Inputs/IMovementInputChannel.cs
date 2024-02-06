using UnityEngine;

namespace DCL.Multiplayer.Movement.Channel
{
    public interface IMovementInputChannel
    {
        void Send(Vector2 pose);
    }
}
