using DCL.ECSComponents;
using UnityEngine;

namespace DCL.SDKComponents.AudioSources
{
    public static class AudioSourceExtensions
    {
        public static void FromPBAudioSourceWithClip(this AudioSource audioSource, PBAudioSource pbAudioSource, AudioClip clip)
        {
            audioSource.clip = clip;

            audioSource.spatialize = true;
            audioSource.spatialBlend = 1;
            audioSource.dopplerLevel = 0.1f;
            audioSource.playOnAwake = false;

            audioSource.ApplyPBAudioSource(pbAudioSource);
        }

        public static void ApplyPBAudioSource(this AudioSource audioSource, PBAudioSource pbAudioSource)
        {
            audioSource.loop = pbAudioSource.HasLoop && pbAudioSource.Loop;
            audioSource.pitch = pbAudioSource.HasPitch ? pbAudioSource.Pitch : Default.PITCH;
            audioSource.volume = pbAudioSource.HasVolume ? pbAudioSource.Volume : Default.VOLUME;

            if (pbAudioSource.HasGlobal)
                audioSource.spatialBlend = pbAudioSource.Global ? 0 : 1;
            else
                audioSource.spatialBlend = 1;
        }

        public static float GetVolume(this PBAudioSource pbAudioSource) =>
            pbAudioSource.HasVolume ? pbAudioSource.Volume : Default.VOLUME;

        /// <summary>The media state the audio events report for the source: what its clip promise and Unity source are doing now.</summary>
        public static MediaState GetMediaState(this in AudioSourceComponent audioSourceComponent)
        {
            // Check if clip is still loading
            if (!audioSourceComponent.ClipPromise.IsConsumed)
                return MediaState.MsLoading;

            // The promise result is retained after consumption, so a failed load is reported as an error
            if (audioSourceComponent.ClipPromise.Result is { Succeeded: false })
                return MediaState.MsError;

            AudioSource? audioSource = audioSourceComponent.AudioSource;

            if (audioSource == null || audioSource.clip == null)
                return !string.IsNullOrEmpty(audioSourceComponent.AudioClipUrl)
                    ? MediaState.MsError // the promise was consumed but produced no clip for the requested URL
                    : MediaState.MsNone;

            // Check if audio is playing, otherwise is ready
            return audioSource.isPlaying ? MediaState.MsPlaying : MediaState.MsReady;
        }

        /// <summary>
        ///     Default constant values for audio source properties, that rewrite protobuf defaults
        /// </summary>
        private static class Default
        {
            public const float VOLUME = 1.0f;
            public const float PITCH = 1.0f;
        }
    }
}
