using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "AudioSettings", menuName = "SO/Audio/AudioSettings")]
    public class AudioSettings : ScriptableObject
    {
        [SerializeField] private Dictionary<AudioCategory, AudioCategorySettings> audioCategorySettings = new Dictionary<AudioCategory, AudioCategorySettings>();
    }

    [Serializable]
    public class AudioCategorySettings
    {
        [SerializeField] private float audioVolume = 0.5f;
        [SerializeField] private int audioPriority = 125;

        public float AudioVolume => audioVolume;
        public int AudioPriority => audioPriority;
    }
}

