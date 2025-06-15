using Cysharp.Threading.Tasks;
using LiveKit.Internal;
using LiveKit.Rooms.Streaming.Audio;
using Livekit.Utils;
using System;

namespace DCL.Multiplayer.Connections.Audio
{
    public class ThreadedAudioRemixConveyor : IAudioRemixConveyor
    {
        private readonly AudioResampler.ThreadSafe resampler = new ();

        public void Dispose()
        {
            resampler.Dispose();
        }

        public void Process(
            OwnedAudioFrame ownedAudioFrame,
            Mutex<RingBuffer> outputBuffer,
            uint numChannels,
            uint sampleRate
        )
        {
            ProcessAsync(ownedAudioFrame, outputBuffer, numChannels, sampleRate).Forget();
        }

        private async UniTaskVoid ProcessAsync(
            OwnedAudioFrame ownedAudioFrame,
            Mutex<RingBuffer> outputBuffer,
            uint numChannels,
            uint sampleRate
        )
        {
            await UniTask.SwitchToThreadPool();
            using OwnedAudioFrame uFrame = resampler.RemixAndResample(ownedAudioFrame, numChannels, sampleRate);
            Write(uFrame, outputBuffer);
        }

        private static void Write(OwnedAudioFrame frame, Mutex<RingBuffer> buffer)
        {
            Span<byte> data = frame.AsSpan();
            using Mutex<RingBuffer>.Guard guard = buffer.Lock();
            guard.Value.Write(data);
        }
    }
}
