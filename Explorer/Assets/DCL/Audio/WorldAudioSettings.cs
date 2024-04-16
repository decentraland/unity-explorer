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

        private readonly Dictionary<WorldAudioClipType, AudioClipConfig> audioClipConfigs = new ();

        public float DistanceThreshold => distanceThreshold;

        public float MinVolume => minVolume;

        public AudioClipConfig GetAudioClipConfigForType(WorldAudioClipType type)
        {
            audioClipConfigs.TryGetValue(type, out AudioClipConfig clipConfig);
            return clipConfig;
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            audioClipConfigs.Clear();
            foreach (var clipConfig in audioClipConfigsList)
            {
                if (clipConfig.Value != null) { audioClipConfigs.Add(clipConfig.Key, clipConfig.Value); }
            }
        }

        [Serializable]
        private struct AudioClipTypeAndConfigKeyValuePair
        {
            public WorldAudioClipType Key;
            public AudioClipConfig Value;
        }

        public enum WorldAudioClipType
        {
            GladeDay,
            GladeNight,
            OceanDay,
            OceanNight,
            HillsDay,
            HillsNight,
        }
    }
}
