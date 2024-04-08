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

        //[SerializeField] public AudioClipPlaybackMode ClipPlaybackMode;
        //[SerializeField] public AudioClipLoopMode ClipLoopMode;
        //[SerializeField] public bool startInRandomPositionInsideClip;
    }

    public enum AudioClipSelectionMode
    {
        Random, //Choose first clip at random
        First, // Chooses first clip on array
    }

    public enum AudioClipPlaybackMode //WIP
    {
        Once, //Plays only the chosen clip once
        Loop, //Plays the chosen clip and then keeps playing clips depending on the AudioClipLoopMode
    }

    public enum AudioClipLoopMode //WIP
    {
        Loop, //Plays the same clip over and over until stopped
        Contiguous, //Plays clips following the order they are in the array, when reaching the last one, starts over, until stopped
        Random, //Gets a random clip from the array, when it finishes gets another randomly and so on, until stopped
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
