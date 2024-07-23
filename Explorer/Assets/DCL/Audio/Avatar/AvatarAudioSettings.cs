using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Audio.Avatar
{
    [CreateAssetMenu(fileName = "AvatarAudioSettings", menuName = "SO/Audio/AvatarAudioSettings")]
    public class AvatarAudioSettings : AudioCategorySettings, ISerializationCallbackReceiver
    {
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
            BlowKiss,
            ThrowMoney,
            FootstepSlide,
        }

        /// <summary>
        ///     This threshold indicates at what point in the animation movement blend we stop producing sounds.
        ///     This avoids unwanted sounds from "ghost" steps produced by the animation blending.
        /// </summary>
        [SerializeField] private float movementBlendThreshold = 0.05f;

        [SerializeField] private List<AudioClipTypeAndConfigKeyValuePair> audioClipConfigsList = new ();

        private readonly Dictionary<AvatarAudioClipType, AudioClipConfig> audioClipConfigs = new ();

        public float MovementBlendThreshold => movementBlendThreshold;

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            audioClipConfigs.Clear();

            foreach (AudioClipTypeAndConfigKeyValuePair clipConfig in audioClipConfigsList)
            {
                if (clipConfig.Value != null) { audioClipConfigs.Add(clipConfig.Key, clipConfig.Value); }
            }
        }

        public AudioClipConfig GetAudioClipConfigForType(AvatarAudioClipType type)
        {
            audioClipConfigs.TryGetValue(type, out AudioClipConfig clipConfig);
            return clipConfig;
        }

        [Serializable]
        private struct AudioClipTypeAndConfigKeyValuePair
        {
            public AvatarAudioClipType Key;
            public AudioClipConfig Value;
        }
    }
}
