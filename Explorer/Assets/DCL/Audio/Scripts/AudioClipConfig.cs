using System;
using UnityEngine;

namespace DCL.Audio
{
    [Serializable]
    [CreateAssetMenu(fileName = "AudioClipConfig", menuName = "SO/Audio/AudioClipConfig")]
    public class AudioClipConfig : ScriptableObject
    {
        [SerializeField] private AudioClip[] audioClips = Array.Empty<AudioClip>();
        [SerializeField] private float relativeVolume = 1;
        [SerializeField] private AudioCategory audioCategory;
        [SerializeField] private float pitchVariation = 0.5f;
        [SerializeField] private AudioClipSelectionMode clipSelectionMode;
        public float PitchVariation => pitchVariation;
        public AudioCategory Category => audioCategory;
        public float RelativeVolume => relativeVolume;
        public AudioClip[] AudioClips => audioClips;

        public AudioClipSelectionMode ClipSelectionMode => clipSelectionMode;
    }

    public enum AudioClipSelectionMode
    {
        Random, //Choose first clip at random
        First, // Chooses first clip on array
    }

    public enum AudioCategory
    {
        UI,
        Chat,
        World,
        Avatar,
        Music,
        None,
    }
}
