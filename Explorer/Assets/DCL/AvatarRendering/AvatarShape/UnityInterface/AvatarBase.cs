using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace DCL.AvatarRendering.AvatarShape.UnityInterface
{
    public class AvatarBase : MonoBehaviour, IAvatarView
    {
        [SerializeField] private Animator avatarAnimator;
        [field: SerializeField] public SkinnedMeshRenderer AvatarSkinnedMeshRenderer { get; private set; }

        [field: Header("Feet IK")]

        // This Rig controls the weight of ALL the feet IK constraints
        [field: SerializeField] public Rig FeetIKRig { get; private set; }

        //This Transform position has the current position of the right leg feet, after the animation kicks in, before the IK. We raycast from this position
        [field: SerializeField] public Transform RightLegConstraint { get; private set; }

        // This transform position decides where we want to put the right leg
        [field: SerializeField] public Transform RightLegIKTarget { get; private set; }
        [field: SerializeField] public Transform LeftLegConstraint { get; private set; }
        [field: SerializeField] public Transform LeftLegIKTarget { get; private set; }

        // Constraint that controls the weight of the leg IK
        [field: SerializeField] public TwoBoneIKConstraint RightLegIK { get; private set; }
        [field: SerializeField] public TwoBoneIKConstraint LeftLegIK { get; private set; }

        //This constraint Applies an offset to the hips, lowering the avatar position based on the desired feet position
        [field: SerializeField] public MultiPositionConstraint HipsConstraint { get; private set; }

        [field: Header("Hands IK")]
        [field: SerializeField] public Rig HandsIKRig { get; private set; }
        [field: SerializeField] public TwoBoneIKConstraint LeftHandIK { get; private set; }

        // IK target position and rotation, its called subTarget because the real target has a parent constraint based on this transform to fix some offsets
        [field: SerializeField] public Transform LeftHandSubTarget { get; private set; }

        // Position where we raycast from
        [field: SerializeField] public Transform LeftHandRaycast { get; private set; }
        [field: SerializeField] public TwoBoneIKConstraint RightHandIK { get; private set; }
        [field: SerializeField] public Transform RightHandSubTarget { get; private set; }
        [field: SerializeField] public Transform RightHandRaycast { get; private set; }

        [field: Header("LookAt IK")]
        [field: SerializeField] public Rig HeadIKRig { get; private set; }

        // The LookAt IK is based on 2 constraints, one for horizontal rotation and other for vertical rotation in order to control different bone chains for both of them, Horizontal is applied first
        [field: SerializeField] public Transform HeadLookAtTargetHorizontal { get; private set; }
        [field: SerializeField] public Transform HeadLookAtTargetVertical { get; private set; }

        // Position of the head after the animations
        [field: SerializeField] public Transform HeadPositionConstraint { get; private set; }

        [field: Header("Other")]

        // Anchor points to attach entities to, through the SDK
        [field: SerializeField] public Transform LeftHandAnchorPoint { get; private set; }
        [field: SerializeField] public Transform RightHandAnchorPoint { get; private set; }

        private AnimatorOverrideController overrideController;
        private List<KeyValuePair<AnimationClip,AnimationClip>> animationOverrides;
        private AnimationClip lastEmote;

        private void Awake()
        {
            if (!avatarAnimator)
                return;

            overrideController = new AnimatorOverrideController(avatarAnimator.runtimeAnimatorController);
            animationOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(animationOverrides);

            // to avoid setting all animations to 'null' after replacing an emote, we set all overrides to their original clips
            animationOverrides = animationOverrides.Select(a => new KeyValuePair<AnimationClip, AnimationClip>(a.Key, a.Key)).ToList();
            overrideController.ApplyOverrides(animationOverrides);
        }

        public Transform GetTransform() =>
            transform;

        public void SetAnimatorFloat(int hash, float value)
        {
            avatarAnimator.SetFloat(hash, value);
        }

        public void SetAnimatorTrigger(int hash)
        {
            avatarAnimator.SetTrigger(hash);
        }

        public bool IsAnimatorInTag(int hashTag) =>
            avatarAnimator.GetCurrentAnimatorStateInfo(0).tagHash == hashTag;

        public int GetAnimatorCurrentStateTag() =>
            avatarAnimator.GetCurrentAnimatorStateInfo(0).tagHash;

        public bool GetAnimatorBool(int hash) =>
            avatarAnimator.GetBool(hash);

        public void SetAnimatorBool(int hash, bool value) =>
            avatarAnimator.SetBool(hash, value);

        public float GetAnimatorFloat(int hash) =>
            avatarAnimator.GetFloat(hash);

        public void ReplaceEmoteAnimation(AnimationClip animationClip)
        {
            if (lastEmote == animationClip) return;

            overrideController["Emote"] = animationClip;
            avatarAnimator.runtimeAnimatorController = overrideController;

            lastEmote = animationClip;
        }
    }

    public interface IAvatarView
    {
        Transform GetTransform();
        void SetAnimatorFloat(int hash, float value);

        void SetAnimatorTrigger(int hash);

        void SetAnimatorBool(int hash, bool value);

        bool GetAnimatorBool(int hash);

        void ReplaceEmoteAnimation(AnimationClip animationClip);

        float GetAnimatorFloat(int hash);

        bool IsAnimatorInTag(int hashTag);

        int GetAnimatorCurrentStateTag();
    }
}
