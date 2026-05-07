using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using RichTypes;

namespace DCL.VoiceChat.Nearby.Audio
{
    /// <summary>
    ///     Boundary between Nearby audio systems and the concrete <see cref="LivekitAudioSource"/> pool.
    ///     Lets EditMode tests substitute a fake that skips <see cref="LivekitAudioSource.Construct"/> /
    ///     <see cref="LivekitAudioSource.Play"/>, keeping Unity's audio thread out of test runs.
    /// </summary>
    public interface INearbyAudioSourceFactory
    {
        LivekitAudioSource Create(StreamKey key, Weak<AudioStream> stream);

        void Dispose(LivekitAudioSource? source);

        void DisposeRoot();

        void InvalidateForDeviceChange();
    }
}
