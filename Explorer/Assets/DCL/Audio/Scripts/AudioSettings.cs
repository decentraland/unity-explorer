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


        private Dictionary<AudioCategory, AudioCategorySettings> audioCategorySettingsDictionary = new Dictionary<AudioCategory, AudioCategorySettings>();

        public float MasterVolume => masterVolume;


        public AudioCategorySettings GetSettingsForCategory(AudioCategory category)
        {
            if (!audioCategorySettingsDictionary.TryGetValue(category, out var settings))
            {
                settings = audioCategorySettings.Find(s => s.key == category).value;
                audioCategorySettingsDictionary.Add(category, settings);
            }

            return settings;
        }

    }

    [Serializable]
    public class AudioCategorySettingsKeyValuePair
    {
            public AudioCategory key;
            public AudioCategorySettings value;
    }

}

