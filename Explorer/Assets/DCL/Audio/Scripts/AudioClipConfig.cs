using System;
using UnityEngine;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "AudioClipConfig", menuName = "SO/Audio/AudioClipConfig")]
    public class AudioClipConfig : ScriptableObject
    {
        [SerializeField] public AudioClip[] audioClips = Array.Empty<AudioClip>();
        [SerializeField] public float relativeVolume = 1;
        [SerializeField] public AudioCategory audioCategory;
        [SerializeField] private bool playClipsAtRandom;
        [SerializeField] private bool playClipsInSequence;
        [SerializeField] public float pitchVariation = 1;
    }

    public enum AudioCategory
    {
        UI,
        CHAT,
        WORLD,
        AVATAR,
        MUSIC,
        OTHER
    }


}
