using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "AudioSettings", menuName = "DCL/Audio/Audio Settings")]
    public class AudioSettings : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField] private List<AudioCategorySettingsKeyValuePair> audioCategorySettings = new ();
        [SerializeField] private float masterVolume = 1;
        [SerializeField] private AudioMixer masterAudioMixer;

        private readonly Dictionary<AudioCategory, AudioCategorySettings> audioCategorySettingsDictionary = new ();

        public float MasterVolume => masterVolume;
        public AudioMixer MasterAudioMixer => masterAudioMixer;

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            audioCategorySettingsDictionary.Clear();

            foreach (AudioCategorySettingsKeyValuePair audioCategory in audioCategorySettings)
            {
                if (audioCategory.value != null && !audioCategorySettingsDictionary.TryAdd(audioCategory.key, audioCategory.value))
                    ReportHub.LogError(ReportCategory.AUDIO, $"Audio settings list the audio category {audioCategory.key} more than once, the first entry is used");
            }
        }

        public AudioCategorySettings GetSettingsForCategory(AudioCategory category)
        {
            audioCategorySettingsDictionary.TryGetValue(category, out AudioCategorySettings settings);
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
