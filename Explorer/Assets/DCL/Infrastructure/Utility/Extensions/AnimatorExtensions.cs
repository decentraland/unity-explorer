using Cysharp.Threading.Tasks;
using DCL.UnityFacades;
using System.Threading;
using UnityEngine;

namespace DCL.Utility.Extensions
{
    public static class AnimatorExtensions
    {
        public static void ResetAnimator(this Animator animator)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        public static void ResetAndDeactivateAnimator(this Animator animator)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.gameObject.SetActive(false);
        }

        public static async UniTask PlayAndAwaitAsync(this Animator animator,
            int triggerHash, int stateHash, int layer = 0, CancellationToken ct = default)
        {
            animator.SetTrigger(triggerHash);

            await UniTask.WaitUntil(
                () => animator.IsInState(layer, stateHash),
                cancellationToken: ct);

            await UniTask.WaitWhile(() =>
            {
                animator.GetState(layer, out int currentState, out float normalizedTime);
                return currentState == stateHash && normalizedTime < 1f;
            }, cancellationToken: ct);
        }
    }
}
