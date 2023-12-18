using UnityEngine;

namespace DCL.Character
{
    public interface ICharacterObject
    {
        CharacterController Controller { get; }

        Transform CameraFocus { get; }

        Transform Transform { get; }
    }
}
