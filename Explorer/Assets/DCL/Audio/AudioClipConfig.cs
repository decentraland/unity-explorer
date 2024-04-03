using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "UIAudioConfig", menuName = "SO/Audio/UIAudioConfig")]
    public class AudioClipConfig : ScriptableObject
    {
        [SerializeField] public AudioClip[] audioClips = Array.Empty<AudioClip>();
        [SerializeField] public float volume = 1;
        [SerializeField] public float priority = 1;
        [SerializeField] public AudioCategory audioCategory;
    }

    public enum AudioCategory
    {
        GENERAL,
        CHAT,
        BACKPACK,
        MAP,
        MENU,
        ENVIRONMENT,
        AVATAR,
        MUSIC,
        OTHER
    }


}
