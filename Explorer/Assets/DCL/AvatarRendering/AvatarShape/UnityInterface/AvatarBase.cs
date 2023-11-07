using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace DCL.AvatarRendering.AvatarShape.UnityInterface
{
    public class AvatarBase : MonoBehaviour, IAvatarView
    {
        [SerializeField] private Animator avatarAnimator;
        [field: SerializeField] public SkinnedMeshRenderer AvatarSkinnedMeshRenderer { get; private set; }

        [field: Header("Feet IK")]
        [field: SerializeField] public Rig FeetIKRig { get; private set; }
        /// <summary>
        ///     This Transform position has the current position of the right leg feet, after the animation kicks in, before the IK. We raycast from this position
        /// </summary>
        [field: SerializeField] public Transform RightLegConstraint { get; private set; }
        /// <summary>
        ///     This transform position decides where we want to put the right leg
        /// </summary>
        [field: SerializeField] public Transform RightLegIKTarget { get; private set; }

        /// <summary>
        ///     Same as Right leg
        /// </summary>
        [field: SerializeField] public Transform LeftLegConstraint { get; private set; }
        /// <summary>
        ///     Same as Right leg
        /// </summary>
        [field: SerializeField] public Transform LeftLegIKTarget { get; private set; }

        [field: SerializeField] public TwoBoneIKConstraint RightLegIK { get; private set; }
        [field: SerializeField] public TwoBoneIKConstraint LeftLegIK { get; private set; }

        /// <summary>
        ///     This constraint Applies an offset to the hips, lowering the avatar position based on the desired feet position
        /// </summary>
        [field: SerializeField] public MultiPositionConstraint HipsConstraint { get; private set; }

        [field: Header("Hands IK")]
        [field: SerializeField] public Rig HandsIKRig { get; private set; }
        [field: SerializeField] public TwoBoneIKConstraint LeftHandIK { get; private set; }
        [field: SerializeField] public Transform LeftHandSubTarget { get; private set; }
        [field: SerializeField] public Transform LeftHandRaycast { get; private set; }
        [field: SerializeField] public TwoBoneIKConstraint RightHandIK { get; private set; }
        [field: SerializeField] public Transform RightHandSubTarget { get; private set; }
        [field: SerializeField] public Transform RightHandRaycast { get; private set; }

        [field: Header("Feet IK")]
        [field: SerializeField] public Rig HeadIKRig { get; private set; }
        [field: SerializeField] public Transform HeadLookAtTarget { get; private set; }
        [field: SerializeField] public Transform HeadPositionConstraint { get; private set; }

        public void SetAnimatorFloat(int hash, float value)
        {
            avatarAnimator.SetFloat(hash, value);
        }

        public void SetAnimatorTrigger(int hash)
        {
            avatarAnimator.SetTrigger(hash);
        }

        public void SetAnimatorBool(int hash, bool value)
        {
            avatarAnimator.SetBool(hash, value);
        }
    }

    public interface IAvatarView
    {
        void SetAnimatorFloat(int hash, float value);

        void SetAnimatorTrigger(int hash);

        void SetAnimatorBool(int hash, bool value);
    }
}
