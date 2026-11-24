using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "WorldAudioSettings", menuName = "DCL/Audio/World Audio Settings")]
    public class WorldAudioSettings : AudioCategorySettings, ISerializationCallbackReceiver
    {
        [SerializeField] private List<AudioClipTypeAndConfigKeyValuePair> audioClipConfigsList = new ();
        private readonly Dictionary<WorldAudioClipType, AudioClipDayNightVariants> audioClipConfigs = new ();

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            audioClipConfigs.Clear();

            foreach (AudioClipTypeAndConfigKeyValuePair clipConfig in audioClipConfigsList)
            {
                if (!audioClipConfigs.TryAdd(clipConfig.Key, clipConfig.Value))
                    ReportHub.LogError(ReportCategory.AUDIO, $"Audio settings list the world audio clip type {clipConfig.Key} more than once, the first entry is used");
            }
        }

        public AudioClipDayNightVariants GetAudioClipConfigForType(WorldAudioClipType type)
        {
            audioClipConfigs.TryGetValue(type, out AudioClipDayNightVariants clipConfig);
            return clipConfig;
        }

        [Serializable]
        public struct AudioClipDayNightVariants
        {
            public AudioClipConfig DayClip;
            public AudioClipConfig NightClip;
        }

        [Serializable]
        private struct AudioClipTypeAndConfigKeyValuePair
        {
            public WorldAudioClipType Key;
            public AudioClipDayNightVariants Value;
        }
    }

    public enum WorldAudioClipType
    {
        Landscape,
        Ocean,
    }
}
