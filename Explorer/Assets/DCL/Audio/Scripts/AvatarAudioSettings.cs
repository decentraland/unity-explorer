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
        [SerializeField] private float avatarAudioVolume = 0.5f;
        [SerializeField] private int avatarAudioPriority = 125;

        public float MovementBlendThreshold => movementBlendThreshold;
        public float AvatarAudioVolume => avatarAudioVolume;
        public int AvatarAudioPriority => avatarAudioPriority;


        [Serializable]
        public class AvatarAudioClipKeyValuePair
        {
            public AvatarAudioPlaybackController.AvatarAudioClipTypes key;
            public AudioClipConfig value;
        }

        [CustomPropertyDrawer(typeof(AvatarAudioClipKeyValuePair))]
        public class AudioClipKeyValuePairDrawer : KeyValuePairCustomDrawer
        { }

        public AudioClipConfig GetAudioClipForType(AvatarAudioPlaybackController.AvatarAudioClipTypes type)
        {
            foreach (var pair in audioClipList)
            {
                if (pair.key == type) { return pair.value; }
            }

            return null;
        }
    }
}
