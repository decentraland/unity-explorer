using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Runtime spatial audio settings shared between <see cref="ProximityVoiceChatManager"/>
    /// and <see cref="ProximityAudioPositionSystem"/>. Seeded from <see cref="VoiceChatConfiguration"/>
    /// at startup; syncs from SO each frame so Inspector changes take effect immediately.
    /// Also modifiable at runtime through the debug panel.
    /// </summary>
    public class ProximityAudioSettings
    {
        public float SpatialBlend = 1f;
        public float DopplerLevel;
        public float MinDistance = 2f;
        public float MaxDistance = 50f;
        public float Spread;
        public AudioRolloffMode RolloffMode = AudioRolloffMode.Logarithmic;

        public bool IsDirty;

        private VoiceChatConfiguration? config;

        public void ApplyTo(AudioSource source)
        {
            source.spatialBlend = SpatialBlend;
            source.dopplerLevel = DopplerLevel;
            source.minDistance = MinDistance;
            source.maxDistance = MaxDistance;
            source.spread = Spread;
            source.rolloffMode = RolloffMode;
        }

        public void SetConfig(VoiceChatConfiguration voiceChatConfiguration)
        {
            config = voiceChatConfiguration;
            SyncFromConfig();
        }

        /// <summary>
        /// Checks if the ScriptableObject values differ from current runtime values.
        /// Returns true if settings were updated (caller should refresh debug bindings).
        /// </summary>
        public bool SyncFromConfig()
        {
            if (config == null) return false;

            bool changed = false;

            if (!Mathf.Approximately(SpatialBlend, config.ProximitySpatialBlend))
            { SpatialBlend = config.ProximitySpatialBlend; changed = true; }

            if (!Mathf.Approximately(DopplerLevel, config.ProximityDopplerLevel))
            { DopplerLevel = config.ProximityDopplerLevel; changed = true; }

            if (!Mathf.Approximately(MinDistance, config.ProximityMinDistance))
            { MinDistance = config.ProximityMinDistance; changed = true; }

            if (!Mathf.Approximately(MaxDistance, config.ProximityMaxDistance))
            { MaxDistance = config.ProximityMaxDistance; changed = true; }

            if (!Mathf.Approximately(Spread, config.ProximitySpread))
            { Spread = config.ProximitySpread; changed = true; }

            if (RolloffMode != config.ProximityRolloffMode)
            { RolloffMode = config.ProximityRolloffMode; changed = true; }

            if (changed)
                IsDirty = true;

            return changed;
        }
    }
}
