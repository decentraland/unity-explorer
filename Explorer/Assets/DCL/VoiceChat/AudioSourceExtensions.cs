using LiveKit.Rooms.Streaming.Audio;
using UnityEngine;

namespace DCL.VoiceChat
{
    public static class VoiceChatExtensions
    {
        public static void ApplySpatialSettings(this LivekitAudioSource lkSource, VoiceChatConfiguration config) =>
            lkSource.SetSpatialSettings(config.nearbySpatialize, config.nearbyIldStrength);

        public static void Apply3dAudioSettings(this AudioSource source, AnimationCurve rolloffCurve)
        {
            source.dopplerLevel = 0;
            source.spread = 0;
            source.spatialBlend = 1f;

            source.rolloffMode = AudioRolloffMode.Custom;
            source.maxDistance = rolloffCurve.MaxDistance();
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloffCurve);
        }

        public static float MaxDistance(this AnimationCurve rolloffCurve) =>
            rolloffCurve[rolloffCurve.length - 1].time;
    }
}
