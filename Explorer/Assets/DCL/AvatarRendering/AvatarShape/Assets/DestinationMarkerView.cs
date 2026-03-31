using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    /// Drives the destination marker animation sequence:
    /// plays the "Start" animation once, then loops "Idle".
    /// </summary>
    public class DestinationMarkerView : MonoBehaviour
    {
        private static readonly int START_STATE = Animator.StringToHash("Start");
        private static readonly int IDLE_STATE = Animator.StringToHash("Idle");

        private Animator animator;
        private bool transitionedToIdle;

        public void Initialize(RuntimeAnimatorController controller)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null) return;

            animator.runtimeAnimatorController = controller;
            animator.Play(START_STATE, 0, 0f);
        }

        private void Update()
        {
            if (transitionedToIdle || animator == null) return;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

            if (info.shortNameHash == START_STATE && info.normalizedTime >= 1f)
            {
                transitionedToIdle = true;
                animator.Play(IDLE_STATE, 0, 0f);
            }
        }
    }
}
