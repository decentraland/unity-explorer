using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "WorldAudioSettings", menuName = "SO/Audio/WorldAudioSettings")]
    public class WorldAudioSettings : AudioCategorySettings, ISerializationCallbackReceiver
    {
        [SerializeField] private float distanceThreshold = 10000f;
        [SerializeField] private float minVolume = 0.01f;


        [SerializeField] private List<AudioClipTypeAndConfigKeyValuePair> audioClipConfigsList = new ();

        private readonly Dictionary<WorldAudioClipType, AudioClipDayNightVariants> audioClipConfigs = new ();

        public float DistanceThreshold => distanceThreshold;

        public float MinVolume => minVolume;

        public AudioClipDayNightVariants GetAudioClipConfigForType(WorldAudioClipType type)
        {
            audioClipConfigs.TryGetValue(type, out AudioClipDayNightVariants clipConfig);
            return clipConfig;
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            audioClipConfigs.Clear();
            foreach (var clipConfig in audioClipConfigsList)
            {
                audioClipConfigs.Add(clipConfig.Key, clipConfig.Value);
            }
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
        Glade,
        Ocean,
        Hills,
    }

}
