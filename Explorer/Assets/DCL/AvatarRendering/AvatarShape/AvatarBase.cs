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
        private RuntimeAnimatorController playerAnimator;
        [SerializeField]
        private RuntimeAnimatorController randomAnimator;

        //Debug stuff, remove after demo
        public void SetAsMainPlayer(bool isMainPlayer)
        {
            if (isMainPlayer)
            {
                avatarAnimator.transform.localPosition = new Vector3(0, -1.1f, 0);
                avatarAnimator.runtimeAnimatorController = playerAnimator;
                avatarAnimator.gameObject.AddComponent<PlayerAnimatorController>();
            }
            else
            {
                avatarAnimator.runtimeAnimatorController = randomAnimator;
                avatarAnimator.gameObject.AddComponent<RandomAnimatorController>();
            }
        }
    }
}
