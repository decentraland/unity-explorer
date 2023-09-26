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

        animator.Play(clips[Random.Range(0, clips.Length)].name);
    }

}
