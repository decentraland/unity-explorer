using UnityEngine;

// TODO: DELETE CLASS, TEMPORARY HELPER TO DEMO MULTIPLE ANIMATIONS
namespace DCL.AvatarRendering.DemoScripts
{
    public class RandomAnimatorController : MonoBehaviour
    {
        private AnimationClip[] clips;
        private Animator animator;

        private void Start()
        {
            animator = GetComponent<Animator>();
            clips = animator.runtimeAnimatorController.animationClips;

            //Lets animate depending on the number of the avatar
            string parentName = transform.parent.parent != null ? transform.parent.parent.name : "00";
            int clipToPlay = int.Parse(parentName.Substring(parentName.Length - 1, 1)) % clips.Length;

            animator.Play(clips[clipToPlay].name);
        }
    }
}
