using System;
using UnityEngine;

namespace DCL.Audio
{
    [Serializable]
    [CreateAssetMenu(fileName = "AudioClipConfig", menuName = "SO/Audio/AudioClipConfig")]
    public class AudioClipConfig : ScriptableObject
    {
        [SerializeField] public AudioClip[] audioClips = Array.Empty<AudioClip>();
        [SerializeField] public float relativeVolume = 1;
        [SerializeField] public AudioCategory audioCategory;
        [SerializeField] public float pitchVariation = 0.5f;

        //We need to improve this ->
        [SerializeField] public AudioClipSelectionMode ClipSelectionMode;
        [SerializeField] public AudioClipPlaybackMode ClipPlaybackMode;
        [SerializeField] public AudioClipLoopMode ClipLoopMode;
        [SerializeField] public bool startInRandomPositionInsideClip;
    }

    public enum AudioClipSelectionMode
    {
        Random, //Choose first clip at random
        First // Chooses first clip on array
    }

    public enum AudioClipPlaybackMode
    {
        Once, //Plays only the chosen clip once
        Loop, //Plays the chosen clip and then keeps playing clips depending on the AudioClipLoopMode
    }

    public enum AudioClipLoopMode
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
        None
    }


}
