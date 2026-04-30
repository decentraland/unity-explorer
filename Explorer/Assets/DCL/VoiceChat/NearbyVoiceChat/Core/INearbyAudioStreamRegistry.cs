using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using RichTypes;
using System;

namespace DCL.VoiceChat.Nearby.Audio
{
    /// <summary>
    /// Thread-safe Nearby-side index of active audio streams per participant identity.
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
        /// Read-only span over the registry's copy-on-write sid array for the wallet.
        /// <c>default</c> (empty span) when the wallet is not indexed; use <see cref="ReadOnlySpan{T}.IsEmpty"/>
        /// to collapse null/empty into one signal. Iterating the span allocates nothing.
        /// </summary>
        ReadOnlySpan<string> GetAudioSids(string walletId);

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
        /// Returns <c>true</c> if <paramref name="walletId"/> was present in the latest
        /// <see cref="LiveKit.Rooms.ActiveSpeakers.IActiveSpeakers.Updated"/> snapshot. Pull-based and
        /// allocation-free so per-frame ECS systems can call it without touching <see cref="LiveKit.Rooms.IRoom"/>.
        /// </summary>
        bool IsActiveSpeaker(string walletId);
    }
}
