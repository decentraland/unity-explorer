using Cysharp.Threading.Tasks;
using LiveKit.Internal;
using LiveKit.Rooms.Streaming.Audio;
using Livekit.Utils;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Audio
{
    public class ThreadedAudioRemixConveyor : IAudioRemixConveyor
    {
        private AudioResampler.ThreadSafe resampler = new();
        private uint lastInputSampleRate = 0;
        private uint lastInputChannels = 0;

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
                    int targetSamples = (int)((audioData.Length / sizeof(short) / ownedAudioFrame.numChannels) * numChannels);
                    var silenceData = new byte[targetSamples * sizeof(short)];
                    
                    WriteToBuffer(outputBuffer, silenceData);
                    return;
                }

                if (ownedAudioFrame.numChannels == numChannels && ownedAudioFrame.sampleRate == sampleRate)
                {
                    WriteToBuffer(outputBuffer, audioData);
                }
                else
                {
                    using var uFrame = resampler.RemixAndResample(ownedAudioFrame, numChannels, sampleRate);
                    Write(uFrame, outputBuffer);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"ThreadedAudioRemixConveyor: ASYNC PROCESSING FAILED - Error: {ex.Message}");
                throw;
            }
        }

        private static bool IsFrameSilentOrEmpty(byte[] audioData)
        {
            if (audioData.Length == 0) return true;

            var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, short>(audioData.AsSpan());
            
            const short SILENCE_THRESHOLD = 32;
            
            for (int i = 0; i < samples.Length; i++)
            {
                if (Math.Abs(samples[i]) > SILENCE_THRESHOLD)
                {
                    return false;
                }
            }
            
            return true;
        }

        private static void WriteToBuffer(Mutex<RingBuffer> outputBuffer, byte[] data)
        {
            using var guard = outputBuffer.Lock();
            guard.Value.Write(data);
        }

        private static void Write(OwnedAudioFrame frame, Mutex<RingBuffer> buffer)
        {
            var data = frame.AsSpan();
            using var guard = buffer.Lock();
            guard.Value.Write(data);
        }

        public void Dispose()
        {
            resampler?.Dispose();
        }
    }
} 
