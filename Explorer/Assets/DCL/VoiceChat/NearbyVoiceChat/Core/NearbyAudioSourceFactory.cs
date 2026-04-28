using DCL.Utilities.Extensions;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using RichTypes;
using UnityEngine;
using UnityEngine.Audio;
using Utility;

namespace DCL.VoiceChat.Nearby.Audio
{
    /// <summary>
    ///     Owns construction and disposal of Nearby <see cref="LivekitAudioSource"/> instances.
    ///     Stateless beyond the shared <c>sourcesRoot</c> parent; intended to be replaced by a
    ///     pool-based implementation later without touching the binding/cleanup systems.
    /// </summary>
    public class NearbyAudioSourceFactory
    {
        private readonly VoiceChatConfiguration configuration;
        private readonly Transform sourcesRoot;

        public NearbyAudioSourceFactory(VoiceChatConfiguration configuration)
        {
            this.configuration = configuration;
            sourcesRoot = new GameObject("VoiceChatSources_Nearby").transform;
        }

        public LivekitAudioSource Create(StreamKey key, Weak<AudioStream> stream)
        {
            LivekitAudioSource lkSource = LivekitAudioSource.New(true, isSpatial: true);
            lkSource.Construct(stream);

            AudioMixerGroup mixerGroup = configuration.ChatAudioMixerGroup;
            AudioSource audioSource = lkSource.AudioSource.EnsureNotNull();
            audioSource.outputAudioMixerGroup = mixerGroup;
            audioSource.Apply3dAudioSettings(configuration.NearbyCustomRolloffCurve);
            lkSource.ApplySpatialSettings(configuration);

            lkSource.name = $"LivekitSource_{key.identity}";
            lkSource.transform.SetParent(sourcesRoot);

            // Start muted — NearbyAudioPositionSystem unmutes after first position sync to avoid an audio burst at world origin.
            audioSource.mute = true;
            lkSource.Play();

            return lkSource;
        }

        public void Dispose(LivekitAudioSource? source)
        {
            if (source == null) return;

            // Force mute before teardown to avoid an audio click — AudioSource.Stop() cuts the buffer mid-cycle.
            AudioSource? audioSource = source.AudioSource;
            if (audioSource != null) audioSource.mute = true;
            source.SetVolume(0f);

            source.Stop();
            source.Free();
            UnityObjectUtils.SafeDestroyGameObject(source);
        }

        public void DisposeRoot()
        {
            UnityObjectUtils.SafeDestroyGameObject(sourcesRoot);
        }
    }
}
