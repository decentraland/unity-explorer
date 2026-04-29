using DCL.Optimization.Pools;
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
    ///     Owns construction, reuse, and disposal of Nearby <see cref="LivekitAudioSource"/> instances.
    ///     Backed by a <see cref="GameObjectPool{T}"/> — instances cycle between a LIVE state and a
    ///     fully inert POOLED state under the pool's container (GameObject inactive, both components
    ///     disabled, stream cleared, no event subscriptions). The pool's auto-created container doubles
    ///     as the feature's hierarchy root (renamed to "VoiceChatSources_Nearby") so live and pooled
    ///     instances are siblings under one parent — no factory-side wrapper transform.
    ///     <para>External API is unchanged from the pre-pool implementation; callers
    ///     (<c>NearbyAudioBindingSystem</c>, <c>NearbyAudioCleanupSystem</c>) need not know pooling exists.</para>
    /// </summary>
    public class NearbyAudioSourceFactory
    {
        private const string ROOT_NAME = "VoiceChatSources_Nearby";

        // Emergency fallback toggle. Flip to false in this branch to bypass pooling and run the
        // pre-A2 instantiate-on-Create / destroy-on-Dispose path — useful as a quick revert if the
        // pool path uncovers a regression mid-debug. Both paths still use the pool's container as
        // their hierarchy root; the pool object stays empty when USE_POOL is false.
        // static readonly (not const) so flipping the toggle doesn't trip CS0162 unreachable-code
        // warnings on the legacy branches.
        private static readonly bool USE_POOL = true;

        private readonly VoiceChatConfiguration configuration;
        private readonly GameObjectPool<LivekitAudioSource> pool;

        // Single hierarchy root. Equals pool.Container — there is no separate factory-owned wrapper.
        // Live and pooled instances are both children of this transform; pooled distinguished by
        // gameObject.activeSelf == false.
        internal Transform sourcesRoot => pool.Container;

        internal int poolCountInactive => pool.CountInactive;

        public NearbyAudioSourceFactory(VoiceChatConfiguration configuration)
        {
            this.configuration = configuration;

            // rootContainer: null — pool creates its container at scene root. We rename it from the
            // default "POOL_CONTAINER_LivekitAudioSource" so the editor hierarchy reflects feature
            // identity instead of the generic pool-wrapper label.
            pool = new GameObjectPool<LivekitAudioSource>(
                rootContainer: null,
                creationHandler: CreatePooledInstance,
                onRelease: ResetForPool);

            pool.Container.gameObject.name = ROOT_NAME;
        }

        public LivekitAudioSource Create(StreamKey key, Weak<AudioStream> stream) =>
            USE_POOL ? CreatePooled(key, stream) : CreateLegacy(key, stream);

        public void Dispose(LivekitAudioSource? source)
        {
            if (source == null) return;

            if (USE_POOL)
                pool.Release(source);
            else
                DisposeLegacy(source);
        }

        public void DisposeRoot()
        {
            if (USE_POOL) pool.Dispose();

            // Cascade-destroys any remaining children (live instances that survived to teardown).
            UnityObjectUtils.SafeDestroyGameObject(pool.Container);
        }

        // ── Pool path ───────────────────────────────────────────────

        private LivekitAudioSource CreatePooledInstance()
        {
            // explicitName gated to editor: GameObject.name setter marshals to native and allocates in
            // IL2CPP — only the editor hierarchy benefits from the per-creation counter suffix.
#if UNITY_EDITOR
            LivekitAudioSource lkSource = LivekitAudioSource.New(explicitName: true, isSpatial: true);
#else
            LivekitAudioSource lkSource = LivekitAudioSource.New(explicitName: false, isSpatial: true);
#endif

            AudioSource audioSource = lkSource.AudioSource.EnsureNotNull();
            audioSource.outputAudioMixerGroup = configuration.ChatAudioMixerGroup;
            audioSource.Apply3dAudioSettings(configuration.NearbyCustomRolloffCurve);
            lkSource.ApplySpatialSettings(configuration);

            // Park under the pool's container immediately. LivekitAudioSource.New() makes a parentless
            // GameObject; pool.HandleRelease will reparent on first Dispose, but until then a fresh
            // live instance would sit at scene root and DisposeRoot wouldn't cascade through it.
            // One-time SetParent per instance (creation, not per acquire) — cheap and keeps the
            // hierarchy invariant: every live or pooled source is a child of pool.Container.
            lkSource.transform.SetParent(pool.Container, worldPositionStays: false);

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

            // Per-owner name is editor-only: GameObject.name setter allocates in IL2CPP and runs on
            // every audible-range entry under the pool. The pool's HandleRelease also renames to
            // POOL_OBJECT_<T> on every release — out of scope for A2 (shared utility).
#if UNITY_EDITOR
            lkSource.name = $"LivekitSource_{key.identity}";
#endif
            // No SetParent here — instances are always under pool.Container (parented in
            // creationHandler, kept there by HandleRelease). Skipping the per-acquire SetParent saves
            // hierarchy-dirty + world-matrix recalc + OnTransformParentChanged on every audible-range
            // entry; Unity gates audio on activeInHierarchy, not on parent identity. Editor hierarchy
            // stays inspectable: live and pooled siblings under one named container, distinguished by
            // activeSelf.

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
            lkSource.transform.SetParent(pool.Container);

            audioSource.mute = true;
            lkSource.Play();

            return lkSource;
        }

        private static void DisposeLegacy(LivekitAudioSource source)
        {
            AudioSource? audioSource = source.AudioSource;
            if (audioSource != null) audioSource.mute = true;
            source.SetVolume(0f);

            source.Stop();
            source.Free();
            UnityObjectUtils.SafeDestroyGameObject(source);
        }
    }
}
