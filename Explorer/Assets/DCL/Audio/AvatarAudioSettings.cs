using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "AvatarAudioSettings", menuName = "SO/Audio/AvatarAudioSettings")]
    public class AvatarAudioSettings : AudioCategorySettings, ISerializationCallbackReceiver
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
            audioClipConfigs.TryGetValue(type, out AudioClipConfig clipConfig);
            return clipConfig;
        }

        public void OnBeforeSerialize()
        {
        }

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
            HardLanding,
            ClothesRustleShort,
            Clap,
            FootstepLight,
            FootstepWalkRight,
            FootstepWalkLeft,
            Hohoho,
            BlowKiss,
            ThrowMoney,
            Snowflakes,
        }
    }
}
