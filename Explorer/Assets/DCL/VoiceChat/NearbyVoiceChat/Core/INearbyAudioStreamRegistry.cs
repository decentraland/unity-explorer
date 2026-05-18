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
        /// Raw reference to the registry's copy-on-write sid array. <c>null</c> when the wallet is not indexed.
        /// Used by <see cref="LiveKit.Rooms.IRoom"/>-driven systems that need to compare snapshots via
        /// <see cref="object.ReferenceEquals(object,object)"/> — a different reference ↔ content changed.
        /// <b>Never mutate.</b> Treat the returned array as immutable from the caller's perspective.
        /// </summary>
        string[]? GetAudioSidsArray(string walletId);

        /// <summary>
        /// Resolves a stream lazily. Must be called from the main thread — <see cref="AudioStream"/>'s constructor
        /// reads Unity audio settings and performs a synchronous FFI request.
        /// </summary>
        Weak<AudioStream> GetActiveStream(StreamKey key);

        bool IsStreamGone(StreamKey key);

        /// <summary>
        /// The single active sid for an identity (the candidate that most recently emitted a media frame across all
        /// known sids), or <c>null</c> if the identity has no sids OR none of its candidates have ever emitted a frame.
        /// The latter is a transient "not-yet-decided" window: the bridge will re-poll next tick and self-heal.
        /// </summary>
        string? GetActiveSid(string walletId);

        /// <summary>
        /// <c>true</c> when <paramref name="sid"/> is the resolver's current pick for <paramref name="walletId"/>.
        /// Cleanup uses this in place of "sid disappeared from snapshot" — it also reaps demoted ghost sids that
        /// still exist in the registry but lost to a fresher candidate.
        /// </summary>
        bool IsActiveSid(string walletId, string sid);

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
