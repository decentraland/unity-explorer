using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DCL.Audio
{


    [CreateAssetMenu(fileName = "UIAudioSettings", menuName = "SO/Audio/UIAudioSettings")]
    public class UIAudioSettings : ScriptableObject
    {
        [SerializeField] private List<UIAudioClipKeyValuePair> audioClipList = new ();
        [SerializeField] private AudioClip defaultAudioClip;
        [SerializeField] private float uiAudioVolume = 0.5f;
        [SerializeField] private int uiAudioPriority = 125;

        public float UIAudioVolume => uiAudioVolume;

        public AudioClip GetAudioClipForType(UIAudioType type)
        {
            foreach (UIAudioClipKeyValuePair pair in audioClipList)
            {
                if (pair.key == type) { return pair.value; }
            }

            ReportHub.Log(ReportCategory.AUDIO, $"Audio Clip for type {type} not found, returning default audio clip {defaultAudioClip}");
            return defaultAudioClip;
        }

        [Serializable]
        public class UIAudioClipKeyValuePair
        {
            public UIAudioType key;
            public AudioClip value;
        }

        [CustomPropertyDrawer(typeof(UIAudioClipKeyValuePair))]
        public class AudioClipKeyValuePairDrawer : KeyValuePairCustomDrawer { }
    }
}
