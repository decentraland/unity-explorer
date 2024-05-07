using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.Animator.Behaviours
{
    [RequireComponent(typeof(Animation))]
    public class AnimationInfo : MonoBehaviour
    {
        [SerializeField] private string currentPlaying = null!;
        [SerializeField] private string requiredAnimation = string.Empty;
        [SerializeField] private List<string> availableAnimations = new ();

        private new Animation animation = null!;

        private void Start()
        {
            animation = GetComponent<Animation>()!;
        }

        private void Update()
        {
            availableAnimations.Clear();
            foreach (AnimationState o in animation)
            {
                if (animation.IsPlaying(o.name!))
                    currentPlaying = o.name!;

                availableAnimations.Add(o.name);
            }
        }

        [ContextMenu(nameof(PlayAnimation))]
        public void PlayAnimation()
        {
            animation.Play(requiredAnimation);
            requiredAnimation = string.Empty;
        }
    }
}
