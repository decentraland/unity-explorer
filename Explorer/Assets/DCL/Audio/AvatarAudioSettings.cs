using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "AvatarAudioSettings", menuName = "SO/Audio/AvatarAudioSettings")]
    public partial class AvatarAudioSettings : ScriptableObject
    {
        //This threshold indicates at what point in the animation movement blend we stop producing sounds. This avoids unwanted sounds from "ghost" steps produced by the animation blending.
        [SerializeField] private float movementBlendThreshold = 0.05f;
        [SerializeField] private List<AvatarAudioClipKeyValuePair> audioClipList = new List<AvatarAudioClipKeyValuePair>();
        [SerializeField] private AudioClip defaultAudioClip;
        [SerializeField] private float avatarAudioVolume = 0.5f;
        [SerializeField] private int avatarAudioPriority = 125;

        public float MovementBlendThreshold => movementBlendThreshold;
        public float AvatarAudioVolume => avatarAudioVolume;
        public int AvatarAudioPriority => avatarAudioPriority;


        [Serializable]
        public class AvatarAudioClipKeyValuePair
        {
            public AvatarAudioSourceManager.AvatarAudioClipTypes key;
            public AudioClip value;
        }

        [CustomPropertyDrawer(typeof(AvatarAudioClipKeyValuePair))]
        public class AudioClipKeyValuePairDrawer : KeyValuePairCustomDrawer
        { }

        public AudioClip GetAudioClipForType(AvatarAudioSourceManager.AvatarAudioClipTypes type)
        {
            foreach (var pair in audioClipList)
            {
                if (pair.key == type) { return pair.value; }
            }

            ReportHub.Log(ReportCategory.AUDIO, $"Audio Clip for type {type} not found, returning default audio clip {defaultAudioClip}");
            return defaultAudioClip;
        }
    }
}
