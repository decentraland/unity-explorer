using UnityEngine;

namespace DCL.Character
{
    public class CharacterObject : MonoBehaviour, ICharacterObject
    {
        public Transform CameraFocus { get; }

        public Transform Transform => transform;
    }
}
