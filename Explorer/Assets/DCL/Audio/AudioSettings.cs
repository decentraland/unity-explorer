using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "AudioSettings", menuName = "SO/Audio/AudioSettings")]
    public class AudioSettings : ScriptableObject , ISerializationCallbackReceiver
    {
        [SerializeField] private List<AudioCategorySettingsKeyValuePair> audioCategorySettings = new ();
        [SerializeField] private float masterVolume = 1;
        [SerializeField] private AudioMixer masterAudioMixer;

        private readonly Dictionary<AudioCategory, AudioCategorySettings> audioCategorySettingsDictionary = new ();

        public float MasterVolume => masterVolume;
        public AudioMixer MasterAudioMixer => masterAudioMixer;

        public AudioCategorySettings GetSettingsForCategory(AudioCategory category)
        {
            audioCategorySettingsDictionary.TryGetValue(category, out AudioCategorySettings settings);
            return settings;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            audioCategorySettingsDictionary.Clear();
            foreach (var audioCategory in audioCategorySettings)
            {
                if (audioCategory.value != null) { audioCategorySettingsDictionary.Add(audioCategory.key, audioCategory.value); }
            }
        }
    }

    [Serializable]
    public class AudioCategorySettingsKeyValuePair
    {
        public AudioCategory key;
        public AudioCategorySettings value;
    }
}
