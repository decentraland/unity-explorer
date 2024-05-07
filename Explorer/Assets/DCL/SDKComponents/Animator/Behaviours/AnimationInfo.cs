using UnityEngine;

namespace DCL.SDKComponents.Animator.Behaviours
{
    [RequireComponent(typeof(Animation))]
    public class AnimationInfo : MonoBehaviour
    {
        [SerializeField] private string currentPlaying = null!;

        private new Animation animation = null!;

        private void Start()
        {
            animation = GetComponent<Animation>()!;
        }

        private void Update()
        {
            foreach (AnimationState o in animation)
                if (animation.IsPlaying(o.name))
                    currentPlaying = o.name;
        }
    }
}
