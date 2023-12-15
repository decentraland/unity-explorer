using UnityEngine;

namespace DCL.Character
{
    public class CharacterObject : MonoBehaviour, ICharacterObject
    {
        [field: SerializeField]
        public CharacterController Controller { get; private set; }

        public void Move(Vector3 globalPosition)
        {
            Vector3 delta = globalPosition - transform.position;
            Controller.Move(delta);
        }

        [field: SerializeField]
        public Transform CameraFocus { get; private set; }

        public Transform Transform => transform;
    }
}
