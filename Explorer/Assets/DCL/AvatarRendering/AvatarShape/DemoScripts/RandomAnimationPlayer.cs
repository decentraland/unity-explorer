using System.Collections.Generic;
using UnityEngine;

public class RandomAnimationPlayer : MonoBehaviour
{
    public List<AnimationClip> AnimationClips;

    private void Start()
    {
        GetComponent<Animation>().Play(AnimationClips[Random.Range(0, AnimationClips.Count)].name);
    }
}
