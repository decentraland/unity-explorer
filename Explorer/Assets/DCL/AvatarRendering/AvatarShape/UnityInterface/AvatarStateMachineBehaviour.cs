using System;
using UnityEngine;
using Utility.Animations;

namespace DCL.AvatarRendering.AvatarShape.UnityInterface
{
    /// <summary>
    /// Handles events sent by the state machine of the AnimationController (attached to CharacterAnimator).
    /// </summary>
    public class AvatarStateMachineBehaviour : StateMachineBehaviour
    {
        /// <summary>
        /// Raised when leaving the Emote and Emote Loop states.
        /// </summary>
        public Action EmoteStateExiting;

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if ((stateInfo.tagHash == AnimationHashes.EMOTE || stateInfo.tagHash == AnimationHashes.EMOTE_LOOP) &&
                animator.GetCurrentAnimatorStateInfo(0).tagHash != AnimationHashes.EMOTE &&
                animator.GetCurrentAnimatorStateInfo(0).tagHash != AnimationHashes.EMOTE_LOOP)
            {
                EmoteStateExiting?.Invoke();
            }

            base.OnStateExit(animator, stateInfo, layerIndex);
        }
    }
}
