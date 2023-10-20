using DCL.AvatarRendering.AvatarShape.DemoScripts;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    public class AvatarBase : MonoBehaviour
    {
        [field: SerializeField]
        public Animator avatarAnimator;

        [SerializeField]
        private RuntimeAnimatorController playerAnimator;
        [SerializeField]
        private RuntimeAnimatorController randomAnimator;
        [field: SerializeField]
        public SkinnedMeshRenderer AvatarSkinnedMeshRenderer { get; private set; }

        //Debug stuff, remove after demo
        public void SetAsMainPlayer(bool isMainPlayer)
        {
            if (isMainPlayer)
                avatarAnimator.runtimeAnimatorController = playerAnimator;
            else
            {
                avatarAnimator.runtimeAnimatorController = randomAnimator;
                avatarAnimator.gameObject.AddComponent<RandomAnimatorController>();
            }
        }
    }
}
