using UnityEngine;

namespace DCL.Character
{
    public interface ICharacterObject
    {
        CharacterController Controller { get; }

        void Move(Vector3 globalPosition);

        Transform CameraFocus { get; }

        Transform Transform { get; }
    }
}
