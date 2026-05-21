using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using RichTypes;
using System;

namespace DCL.VoiceChat.Nearby.Audio
{
    /// <summary>
    /// Thread-safe Nearby-side index of active audio streams per participant identity. // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
    /// Self-bootstraps from <see cref="LiveKit.Rooms.IRoom"/> connection / subscribe events; not driven by callers.
    /// </summary>
    public interface INearbyAudioStreamRegistry : IDisposable
    {
        /// <summary>
        /// Point-lookup: returns <c>true</c> if the wallet currently has at least one indexed audio sid.
        /// Allocation-free, used by <see cref="LiveKit.Rooms.IRoom"/>-driven hot-path filters that only
        /// need a presence signal.
        /// </summary>
        bool HasAudioStream(string walletId);

        /// <summary>
        /// Resolves a stream lazily. Must be called from the main thread — <see cref="AudioStream"/>'s constructor
        /// reads Unity audio settings and performs a synchronous FFI request.
        /// </summary>
        Weak<AudioStream> GetActiveStream(StreamKey key);

        /// <summary>
        /// <b>Call-site discipline.</b> Main-thread only — the multi-candidate branch performs one FFI hop per sid.
        /// The single active sid for an identity. Returns <c>null</c> if the identity has no sids.
        /// Single-candidate fast path: returned eagerly without consulting the frame oracle (a lone candidate is active by definition).
        /// Multi-candidate: picks the sid that most recently emitted a media frame; returns <c>null</c> if none of the candidates has
        /// ever emitted a frame — a transient "not-yet-decided" window that the bridge re-polls next tick and self-heals.
        /// </summary>
        string? GetActiveSid(string walletId);

        /// <summary>
        /// <c>true</c> when <paramref name="key"/>.sid is the resolver's current pick for <paramref name="key"/>.identity.
        /// Cleanup uses this in place of "sid disappeared from snapshot" — it also reaps demoted ghost sids that
        /// still exist in the registry but lost to a fresher candidate.
        /// Shares the cost profile and main-thread discipline of <see cref="GetActiveSid"/>: same resolver call underneath.
        /// </summary>
        bool IsActiveSid(StreamKey key);

        /// <summary>
        /// Returns <c>true</c> if <paramref name="walletId"/> was present in the latest
        /// <see cref="LiveKit.Rooms.ActiveSpeakers.IActiveSpeakers.Updated"/> snapshot. Pull-based and
        /// allocation-free so per-frame ECS systems can call it without touching <see cref="LiveKit.Rooms.IRoom"/>.
        /// </summary>
        bool IsActiveSpeaker(string walletId);

        /// <summary>
        /// Monotonic pull-based freshness signal — bumped on Unity output-device change
        /// (<see cref="UnityEngine.AudioSettings.OnAudioConfigurationChanged"/>, <c>deviceWasChanged: true</c>).
        /// A value different from last tick means consumers must reconcile their device-bound state.
        /// </summary>
        int RebuildEpoch { get; }
    }
}
