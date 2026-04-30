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
        /// Point-lookup. <c>true</c> iff the wallet currently has at least one subscribed audio sid.
        /// Allocation-free; safe on per-frame ECS hot paths.
        /// </summary>
        bool HasAudioStream(string walletId);

        /// <summary>
        /// Read-only span over the wallet's current sid set. Returns <c>default</c> (empty span)
        /// when the wallet is absent. The underlying storage is copy-on-write — the span is valid
        /// for the current observation window only; do not retain across registry mutations.
        /// </summary>
        ReadOnlySpan<string> GetAudioSids(string walletId);

        /// <summary>
        /// Raw reference to the wallet's current copy-on-write sid array, or <c>null</c> when absent.
        /// Reference identity is the version signal: a different reference ↔ content changed.
        /// Used <b>only</b> by <see cref="DCL.VoiceChat.Nearby.Systems.NearbyLivekitBridgeSystem"/>
        /// for <see cref="object.ReferenceEquals(object, object)"/> comparison against the snapshot
        /// cached in <see cref="DCL.VoiceChat.Nearby.StreamingAudioComponent"/>. Never mutate.
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
