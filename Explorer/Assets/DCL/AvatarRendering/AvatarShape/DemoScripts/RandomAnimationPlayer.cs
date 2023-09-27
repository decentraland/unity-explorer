using UnityEngine;

public class RandomAnimationPlayer : MonoBehaviour
{
    private AnimationClip[] clips;
    private Animator animator;

    private void Start()
    {
        // Get the animator component
        animator = GetComponent<Animator>();
        // Get all available clips
        clips = animator.runtimeAnimatorController.animationClips;
        string parentName = transform.parent.parent.name;
        int clipToPlay = int.Parse(parentName.Substring(parentName.Length - 1, 1)) % clips.Length;
        animator.Play(clips[clipToPlay].name);
    }

}
