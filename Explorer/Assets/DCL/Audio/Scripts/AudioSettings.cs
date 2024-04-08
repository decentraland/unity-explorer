using Decentraland.Kernel.Comms.V1;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "AudioSettings", menuName = "SO/Audio/AudioSettings")]
    public class AudioSettings : ScriptableObject
    {
        [SerializeField] private List<AudioCategorySettingsKeyValuePair> audioCategorySettings = new List<AudioCategorySettingsKeyValuePair>();
        [SerializeField] private float masterVolume = 1;
        [SerializeField] private AudioMixer masterAudioMixer;

        public float MasterVolume => masterVolume;
        public List<AudioCategorySettingsKeyValuePair> CategorySettings => audioCategorySettings;

    }

    [Serializable]
    public class AudioCategorySettingsKeyValuePair
    {
            public AudioCategory key;
            public AudioCategorySettings value;
    }

}

