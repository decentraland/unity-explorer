using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using RichTypes;
using System;
using System.Collections.Concurrent;

namespace DCL.VoiceChat.Nearby.Audio
{
    /// <summary>
    /// Thread-safe Nearby-side index of active audio streams per participant identity.
    /// Self-bootstraps from <see cref="LiveKit.Rooms.IRoom"/> connection / subscribe events; not driven by callers.
    /// </summary>
    public interface INearbyAudioStreamRegistry : IDisposable
    {
        /// <summary>
        /// Returns the concrete inner dictionary so callers can iterate with a struct enumerator (allocation-free).
        /// <c>null</c> means the participant is not in the room or has no audio publications.
        /// </summary>
        ConcurrentDictionary<string, byte>? GetAudioSids(string walletId);

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
