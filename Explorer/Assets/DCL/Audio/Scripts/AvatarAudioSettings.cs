using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "AvatarAudioSettings", menuName = "SO/Audio/AvatarAudioSettings")]
    public class AvatarAudioSettings : AudioCategorySettings
    {
        /// <summary>
        ///     This threshold indicates at what point in the animation movement blend we stop producing sounds.
        ///     This avoids unwanted sounds from "ghost" steps produced by the animation blending.
        /// </summary>
        [SerializeField] private float movementBlendThreshold = 0.05f;

        [SerializeField] private List<AudioClipTypeAndConfigKeyValuePair> audioClipConfigsList = new ();

        private readonly Dictionary<AvatarAudioClipType, AudioClipConfig> audioClipConfigs = new ();

        public float MovementBlendThreshold => movementBlendThreshold;

        public AudioClipConfig GetAudioClipConfigForType(AvatarAudioClipType type)
        {
            if (!audioClipConfigs.TryGetValue(type, out AudioClipConfig clipConfig))
            {
                clipConfig = audioClipConfigsList.Find(c => c.Key == type).Value;
                audioClipConfigs.Add(type, clipConfig);
            }

            return clipConfig;
        }

        [Serializable]
        private struct AudioClipTypeAndConfigKeyValuePair
        {
            public AvatarAudioClipType Key;
            public AudioClipConfig Value;
        }

        public enum AvatarAudioClipType
        {
            JumpStartRun,
            JumpStartJog,
            JumpStartWalk,
            StepRun,
            StepWalk,
            StepJog,
            JumpLandJog,
            JumpLandRun,
            JumpLandWalk,
            LongFall,
            ShortFall,
        }
    }
}
