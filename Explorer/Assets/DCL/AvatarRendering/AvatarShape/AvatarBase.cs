using UnityEditor.Animations;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    public class AvatarBase : MonoBehaviour
    {
        [field: SerializeField]
        public SkinnedMeshRenderer AvatarSkinnedMeshRenderer { get; private set; }

        [SerializeField]
        private Animator avatarAnimator;

        [SerializeField]
        private AnimatorController playerAnimator;

        public void SetAsMainPlayer(bool isMainPlayer)
        {
            if (isMainPlayer)
            {
                avatarAnimator.transform.localPosition = new Vector3(0, -1.1f, 0);
                avatarAnimator.runtimeAnimatorController = playerAnimator;
            }
            else
                avatarAnimator.gameObject.AddComponent<RandomAnimationPlayer>();
        }
    }
}
