using DCL.Diagnostics;
using LiveKit.Internal;
using LiveKit.Rooms.Streaming.Audio;
using Livekit.Utils;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Audio
{
    public class OptimizedThreadedAudioRemixConveyor : IAudioRemixConveyor
    {
        private const short SILENCE_THRESHOLD = 32;

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
            ThreadPool.QueueUserWorkItem(_ => ProcessOnThreadPool(ownedAudioFrame, outputBuffer, numChannels, sampleRate));
        }

        private void ProcessOnThreadPool(
            OwnedAudioFrame ownedAudioFrame,
            Mutex<RingBuffer> outputBuffer,
            uint numChannels,
            uint sampleRate
        )
        {
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

                bool isEmptyFrame = IsFrameSilentOrEmpty(ownedAudioFrame.AsSpan());

                if (isEmptyFrame)
                {
                    var targetSamples = (int)(ownedAudioFrame.Length / sizeof(short) / ownedAudioFrame.numChannels * numChannels);
                    var silenceDataSize = targetSamples * sizeof(short);

                    WriteSilenceToBuffer(outputBuffer, silenceDataSize);
                    return;
                }

                if (ownedAudioFrame.numChannels == numChannels && ownedAudioFrame.sampleRate == sampleRate) { WriteToBuffer(outputBuffer, ownedAudioFrame.AsSpan()); }
                else
                {
                    using OwnedAudioFrame uFrame = resampler.RemixAndResample(ownedAudioFrame, numChannels, sampleRate);
                    Write(uFrame, outputBuffer);
                }
            }
            catch (Exception ex)
            {
                ReportHub.LogError(new ReportData(ReportCategory.VOICE_CHAT), $"ThreadedAudioRemixConveyor: ASYNC PROCESSING FAILED - Error: {ex.Message}");
                throw;
            }
        }

        private static bool IsFrameSilentOrEmpty(Span<byte> audioData)
        {
            if (audioData.Length == 0) return true;

            Span<short> samples = MemoryMarshal.Cast<byte, short>(audioData);

            for (var i = 0; i < samples.Length; i++)
            {
                if (Math.Abs(samples[i]) > SILENCE_THRESHOLD) { return false; }
            }

            return true;
        }

        private static void WriteToBuffer(Mutex<RingBuffer> outputBuffer, Span<byte> data)
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


        private static void WriteSilenceToBuffer(Mutex<RingBuffer> outputBuffer, int silenceDataSize)
        {
            using Mutex<RingBuffer>.Guard guard = outputBuffer.Lock();

            const int chunkSize = 1024; // 1KB chunks
            var chunk = Unsafe.stackalloc byte[chunkSize];

            int remainingBytes = silenceDataSize;
            while (remainingBytes > 0)
            {
                int bytesToWrite = Math.Min(chunkSize, remainingBytes);
                var chunkSpan = chunk.Slice(0, bytesToWrite);
                guard.Value.Write(chunkSpan);
                remainingBytes -= bytesToWrite;
            }
        }
    }
}
