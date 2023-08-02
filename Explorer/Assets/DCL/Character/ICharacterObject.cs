using UnityEngine;

namespace DCL.Character
{
    public interface ICharacterObject
    {
        Transform CameraFocus { get; }

        Transform Transform { get; }
    }
}
