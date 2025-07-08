using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using LiveKit.Internal;
using LiveKit.Rooms.Streaming.Audio;
using Livekit.Utils;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Audio
{
    public class OptimizedThreadedAudioRemixConveyor : IAudioRemixConveyor
    {
        private AudioResampler.ThreadSafe resampler = new ();
        private uint lastInputSampleRate;
        private uint lastInputChannels;

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

            try
            {
                if (ownedAudioFrame.sampleRate != lastInputSampleRate ||
                    ownedAudioFrame.numChannels != lastInputChannels)
                {
                    resampler?.Dispose();
                    resampler = new AudioResampler.ThreadSafe();

                    lastInputSampleRate = ownedAudioFrame.sampleRate;
                    lastInputChannels = ownedAudioFrame.numChannels;
                }

                byte[] audioData = ownedAudioFrame.AsSpan().ToArray();
                bool isEmptyFrame = IsFrameSilentOrEmpty(audioData);

                if (isEmptyFrame)
                {
                    var targetSamples = (int)(audioData.Length / sizeof(short) / ownedAudioFrame.numChannels * numChannels);
                    var silenceData = new byte[targetSamples * sizeof(short)];

                    WriteToBuffer(outputBuffer, silenceData);
                    return;
                }

                if (ownedAudioFrame.numChannels == numChannels && ownedAudioFrame.sampleRate == sampleRate) { WriteToBuffer(outputBuffer, audioData); }
                else
                {
                    using OwnedAudioFrame uFrame = resampler.RemixAndResample(ownedAudioFrame, numChannels, sampleRate);
                    Write(uFrame, outputBuffer);
                }
            }
            catch (Exception ex)
            {
                ReportHub.LogError(new ReportData(ReportCategory.AUDIO), $"ThreadedAudioRemixConveyor: ASYNC PROCESSING FAILED - Error: {ex.Message}");
                throw;
            }
        }

        private static bool IsFrameSilentOrEmpty(byte[] audioData)
        {
            if (audioData.Length == 0) return true;

            Span<short> samples = MemoryMarshal.Cast<byte, short>(audioData.AsSpan());

            const short SILENCE_THRESHOLD = 32;

            for (var i = 0; i < samples.Length; i++)
            {
                if (Math.Abs(samples[i]) > SILENCE_THRESHOLD) { return false; }
            }

            return true;
        }

        private static void WriteToBuffer(Mutex<RingBuffer> outputBuffer, byte[] data)
        {
            using Mutex<RingBuffer>.Guard guard = outputBuffer.Lock();
            guard.Value.Write(data);
        }

        private static void Write(OwnedAudioFrame frame, Mutex<RingBuffer> buffer)
        {
            Span<byte> data = frame.AsSpan();
            using Mutex<RingBuffer>.Guard guard = buffer.Lock();
            guard.Value.Write(data);
        }
    }
}
