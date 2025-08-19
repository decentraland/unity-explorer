using RichTypes;
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

#if UNITY_EDITOR
        [field: SerializeField]
        private Transform? lastStandingGround;
#endif
        private Option<CurrentPlatform> standingGround;

        public Option<CurrentPlatform> StandingGround => Controller.isGrounded ? standingGround : Option<CurrentPlatform>.None;

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            standingGround = Option<CurrentPlatform>.Some(new CurrentPlatform(hit.collider));
#if UNITY_EDITOR
            lastStandingGround = hit.collider.transform;
#endif
        }
    }
}
