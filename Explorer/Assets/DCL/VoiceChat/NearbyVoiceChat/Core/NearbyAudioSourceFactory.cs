using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using RichTypes;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Utility;

namespace DCL.VoiceChat.Nearby.Audio
{
    /// <summary>
    ///     Owns construction, reuse, and disposal of Nearby <see cref="LivekitAudioSource"/> instances.
    ///     Backed by a <see cref="GameObjectPool{T}"/> — instances cycle between a LIVE state and a
    ///     fully inert POOLED state under the pool's container (GameObject inactive, both components
    ///     disabled, stream cleared, no event subscriptions). The pool's auto-created container doubles
    ///     as the feature's hierarchy root (renamed to "VoiceChatSources_Nearby") so live and pooled
    ///     instances are siblings under one parent — no factory-side wrapper transform.
    /// </summary>
    public class NearbyAudioSourceFactory
    {
        private const string ROOT_NAME = "VoiceChatSources_Nearby";

        // Hard cap on live pool-managed instances. Beyond this, Create falls through to the legacy
        // instantiate-on-Create / destroy-on-Dispose path. Set to 0 to bypass pooling entirely.
        private const int MAX_LIVE_INSTANCES = 300;

        private readonly VoiceChatConfiguration configuration;
        private readonly GameObjectPool<LivekitAudioSource> pool;

        // Tags instances handed out via the legacy fallback path (pool overflow) so Dispose can
        // route them back through DisposeLegacy instead of the pool.
        private readonly HashSet<LivekitAudioSource> legacyInstances = new (8);

        private int liveCount;

        internal int poolCountInactive => pool.CountInactive;

        public NearbyAudioSourceFactory(VoiceChatConfiguration configuration)
        {
            this.configuration = configuration;

            pool = new GameObjectPool<LivekitAudioSource>(rootContainer: null,
                creationHandler: CreatePooledInstance,
                onRelease: ResetForPool);

            pool.ParentContainer.gameObject.name = ROOT_NAME;
        }

        public LivekitAudioSource Create(StreamKey key, Weak<AudioStream> stream)
        {
            // Cap on simultaneously-live instances. Once exceeded, peel off into the legacy path:
            // those overflow sources get destroyed on Dispose instead of returning to the pool, so
            // the resident set drains back to MAX_LIVE_INSTANCES naturally as users go out of range.
            if (liveCount >= MAX_LIVE_INSTANCES)
                return CreateLegacyTracked(key, stream);

            liveCount++;
            return CreatePooled(key, stream);
        }

        public void Dispose(LivekitAudioSource? source)
        {
            if (source == null) return;

            if (legacyInstances.Remove(source))
            {
                DisposeLegacy(source);
                return;
            }

            pool.Release(source);
            if (liveCount > 0) liveCount--;
        }

        public void DisposeRoot()
        {
            pool.Dispose();
            UnityObjectUtils.SafeDestroyGameObject(pool.ParentContainer);
        }

        // ── Pool path ───────────────────────────────────────────────
        private LivekitAudioSource CreatePooledInstance()
        {
#if UNITY_EDITOR
            LivekitAudioSource lkSource = LivekitAudioSource.New(explicitName: true, isSpatial: true);
#else
            LivekitAudioSource lkSource = LivekitAudioSource.New(explicitName: false, isSpatial: true);
#endif

            AudioSource audioSource = lkSource.AudioSource.EnsureNotNull();
            audioSource.outputAudioMixerGroup = configuration.ChatAudioMixerGroup;
            audioSource.Apply3dAudioSettings(configuration.NearbyCustomRolloffCurve);
            lkSource.ApplySpatialSettings(configuration);
            lkSource.transform.SetParent(pool.ParentContainer, worldPositionStays: false);

            return lkSource;
        }

        private LivekitAudioSource CreatePooled(StreamKey key, Weak<AudioStream> stream)
        {
            LivekitAudioSource lkSource = pool.Get();
            AudioSource audioSource = lkSource.AudioSource.EnsureNotNull();

            // Re-enable BEFORE Construct/Play so OnEnable re-subscribes to AudioSettings.OnAudioConfigurationChanged
            // and refreshes outputSampleRate; Play() then has a wired AudioSource to drive.
            lkSource.enabled = true;
            audioSource.enabled = true;

            lkSource.Construct(stream);

#if UNITY_EDITOR
            lkSource.name = $"LivekitSource_{key.identity}";
#endif

            // Start muted — NearbyAudioPositionSystem unmutes after first position sync to avoid an audio burst at world origin.
            audioSource.mute = true;
            audioSource.volume = 1f;
            lkSource.Play();

            return lkSource;
        }

        private static void ResetForPool(LivekitAudioSource source)
        {
            if (source == null) return;

            AudioSource? audioSource = source.AudioSource;

            // Mute and zero volume first — AudioSource.Stop() cuts the buffer mid-cycle and an
            // unmuted source on full volume would click. Same precaution as the pre-pool Dispose.
            if (audioSource != null)
            {
                audioSource.mute = true;
                audioSource.volume = 0f;
            }

            source.Stop();
            source.Free();

            // Order matters: AudioSource.enabled = false BEFORE LivekitAudioSource.enabled = false so
            // the audio thread stops reading the source before its wrapper drops audio-config subscription.
            if (audioSource != null) audioSource.enabled = false;
            source.enabled = false;
        }

        // ── Legacy path (fallback) ──────────────────────────────────

        private LivekitAudioSource CreateLegacyTracked(StreamKey key, Weak<AudioStream> stream)
        {
            LivekitAudioSource source = CreateLegacy(key, stream);
            legacyInstances.Add(source);
            return source;
        }

        private LivekitAudioSource CreateLegacy(StreamKey key, Weak<AudioStream> stream)
        {
#if UNITY_EDITOR
            LivekitAudioSource lkSource = LivekitAudioSource.New(explicitName: true, isSpatial: true);
#else
            LivekitAudioSource lkSource = LivekitAudioSource.New(explicitName: false, isSpatial: true);
#endif
            lkSource.Construct(stream);

            AudioMixerGroup mixerGroup = configuration.ChatAudioMixerGroup;
            AudioSource audioSource = lkSource.AudioSource.EnsureNotNull();
            audioSource.outputAudioMixerGroup = mixerGroup;
            audioSource.Apply3dAudioSettings(configuration.NearbyCustomRolloffCurve);
            lkSource.ApplySpatialSettings(configuration);

#if UNITY_EDITOR
            lkSource.name = $"LivekitSource_{key.identity}";
#endif
            // Same hierarchy root as the pool path so DisposeRoot's cascade catches legacy instances too.
            lkSource.transform.SetParent(pool.ParentContainer);

            audioSource.mute = true;
            lkSource.Play();

            return lkSource;
        }

        private static void DisposeLegacy(LivekitAudioSource source)
        {
            AudioSource? audioSource = source.AudioSource;

            if (audioSource != null)
                audioSource.mute = true;

            source.SetVolume(0f);
            source.Stop();
            source.Free();
            UnityObjectUtils.SafeDestroyGameObject(source);
        }
    }
}
