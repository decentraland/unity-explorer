using LiveKit.Audio;
using LiveKit.Rooms.Tracks;
using RichTypes;
using System;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Wraps a published microphone track with owned lifecycle management.
    ///     Shared between Core and Proximity publishers.
    /// </summary>
    internal readonly struct MicrophoneTrack : IDisposable
    {
        private readonly Owned<MicrophoneRtcAudioSource> source;

        public ITrack Track { get; }

        public Weak<MicrophoneRtcAudioSource> Source => source.Downgrade();

        public MicrophoneTrack(ITrack track, Owned<MicrophoneRtcAudioSource> source)
        {
            Track = track;
            this.source = source;
        }

        public void Dispose()
        {
            source.Dispose(out MicrophoneRtcAudioSource? inner);
            inner?.Dispose();
        }
    }
}
