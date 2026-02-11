using Cysharp.Threading.Tasks;
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
                () => animator.GetCurrentAnimatorStateInfo(layer).shortNameHash == stateHash,
                cancellationToken: ct);

            await UniTask.WaitWhile(() =>
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
                return info.shortNameHash == stateHash && info.normalizedTime < 1f;
            }, cancellationToken: ct);
        }
    }
}
