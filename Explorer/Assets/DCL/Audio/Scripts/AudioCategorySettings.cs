using System;
using UnityEngine;
using UnityEngine.Audio;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "AudioCategorySettings", menuName = "SO/Audio/AudioCategorySettings")]
    [Serializable]
    public class AudioCategorySettings : ScriptableObject
    {
        [SerializeField] private float categoryVolume = 1;
        [SerializeField] private int audioPriority = 125;
        [SerializeField] public AudioMixerGroup audioMixerGroup;

        public float CategoryVolume => categoryVolume;
        public int AudioPriority => audioPriority;
    }
}
