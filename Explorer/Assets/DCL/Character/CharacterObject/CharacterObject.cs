using UnityEngine;

namespace DCL.Character
{
    public class CharacterObject : MonoBehaviour, ICharacterObject
    {
        [field: SerializeField]
        public CharacterController Controller { get; private set; }

        [field: SerializeField]
        public Transform CameraFocus { get; private set; }

        public Transform Transform => transform;

        public Vector3 Position => transform.position;
    }
}
