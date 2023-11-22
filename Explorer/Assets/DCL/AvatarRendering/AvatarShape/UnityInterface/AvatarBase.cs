using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.UnityInterface
{
    public class AvatarBase : MonoBehaviour, IAvatarView
    {
        [SerializeField] private Animator avatarAnimator;
        [field: SerializeField] public SkinnedMeshRenderer AvatarSkinnedMeshRenderer { get; private set; }

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
