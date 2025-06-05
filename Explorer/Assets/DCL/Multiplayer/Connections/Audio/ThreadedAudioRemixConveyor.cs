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
            Debug.LogError($"ThreadedAudioRemixConveyor: Process called - Input: {ownedAudioFrame.numChannels}ch@{ownedAudioFrame.sampleRate}Hz, Output: {numChannels}ch@{sampleRate}Hz");
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
                // Check if INPUT format has changed - reset resampler if needed
                // Output format (Unity's) should remain constant, only input (microphone) changes
                if (ownedAudioFrame.sampleRate != lastInputSampleRate || 
                    ownedAudioFrame.numChannels != lastInputChannels)
                {
                    Debug.LogError($"ThreadedAudioRemixConveyor: Input format change detected - resetting resampler " +
                                   $"(Input: {lastInputChannels}ch@{lastInputSampleRate}Hz -> {ownedAudioFrame.numChannels}ch@{ownedAudioFrame.sampleRate}Hz, " +
                                   $"Output remains: {numChannels}ch@{sampleRate}Hz)");
                    
                    // Dispose old resampler and create fresh one to clear any corrupted state
                    resampler?.Dispose();
                    resampler = new AudioResampler.ThreadSafe();
                    
                    // Update tracked INPUT formats only
                    lastInputSampleRate = ownedAudioFrame.sampleRate;
                    lastInputChannels = ownedAudioFrame.numChannels;
                }

                // Extract audio data before async operations to avoid Span in async context
                byte[] audioData = ownedAudioFrame.AsSpan().ToArray();
                bool isEmptyFrame = IsFrameSilentOrEmpty(audioData);
                
                if (isEmptyFrame)
                {
                    // For empty frames, just write silence at the target format without resampling
                    int targetSamples = (int)((audioData.Length / sizeof(short) / ownedAudioFrame.numChannels) * numChannels);
                    var silenceData = new byte[targetSamples * sizeof(short)];
                    
                    WriteToBuffer(outputBuffer, silenceData);
                    
                    Debug.LogError($"ThreadedAudioRemixConveyor: Skipped resampling for silent frame ({ownedAudioFrame.numChannels}ch@{ownedAudioFrame.sampleRate}Hz -> {numChannels}ch@{sampleRate}Hz)");
                    return;
                }

                // Optimization: Skip expensive resampling if formats already match
                if (ownedAudioFrame.numChannels == numChannels && ownedAudioFrame.sampleRate == sampleRate)
                {
                    // Direct copy - no resampling needed
                    Debug.LogError($"ThreadedAudioRemixConveyor: Direct copy {ownedAudioFrame.numChannels}ch@{ownedAudioFrame.sampleRate}Hz -> {numChannels}ch@{sampleRate}Hz");
                    WriteToBuffer(outputBuffer, audioData);
                    
                    // Frame will be disposed when method exits
                }
                else
                {
                    // Resampling required - use FFI resampler with timing
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    
                    Debug.LogError($"ThreadedAudioRemixConveyor: RESAMPLING STARTING {ownedAudioFrame.numChannels}ch@{ownedAudioFrame.sampleRate}Hz -> {numChannels}ch@{sampleRate}Hz");
                    
                    using var uFrame = resampler.RemixAndResample(ownedAudioFrame, numChannels, sampleRate);
                    stopwatch.Stop();
                    
                    Write(uFrame, outputBuffer);
                    
                    Debug.LogError($"ThreadedAudioRemixConveyor: RESAMPLING COMPLETED in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)");
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

            // Convert to int16 samples and check for silence
            var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, short>(audioData.AsSpan());
            
            // Quick check: if all samples are zero or very quiet, consider it silent
            const short SILENCE_THRESHOLD = 32; // Very quiet threshold
            
            for (int i = 0; i < samples.Length; i++)
            {
                if (Math.Abs(samples[i]) > SILENCE_THRESHOLD)
                {
                    return false; // Found non-silent audio
                }
            }
            
            return true; // All samples are silent/quiet
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
