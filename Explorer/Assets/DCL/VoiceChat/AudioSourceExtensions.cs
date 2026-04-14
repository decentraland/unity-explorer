using LiveKit.Rooms.Streaming.Audio;
using UnityEngine;

namespace DCL.VoiceChat
{
    public static class VoiceChatExtensions
    {
        public static void ApplySpatialSettings(this LivekitAudioSource lkSource, VoiceChatConfiguration config) =>
            lkSource.SetSpatialSettings(config.nearbySpatialize, config.nearbyIldStrength, config.nearbySmoothPanning);

        public static void Apply3dAudioSettings(this AudioSource source, AnimationCurve rolloffCurve)
        {
            source.dopplerLevel = 0;
            source.spread = 0;
            source.spatialBlend = 1f;

            source.rolloffMode = AudioRolloffMode.Custom;
            source.maxDistance = rolloffCurve[rolloffCurve.length - 1].time;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloffCurve);
        }
    }
}
