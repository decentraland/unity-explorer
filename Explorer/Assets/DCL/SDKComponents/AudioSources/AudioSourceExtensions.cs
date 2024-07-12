#nullable enable

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

            if (!PlayingChanged()) return;

            if (pbAudioSource.Playing)
                audioSource.Play();
            else
                audioSource.Stop();

            return;

            bool PlayingChanged() =>
                pbAudioSource.HasPlaying && pbAudioSource.Playing != audioSource.isPlaying && audioSource.clip != null;
        }

        public static float GetVolume(this PBAudioSource pbAudioSource) =>
            pbAudioSource.HasVolume ? pbAudioSource.Volume : Default.VOLUME;

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
