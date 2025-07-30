using LiveKit;
using System;
using System.Collections.Generic;
using UnityEngine;
using LiveKit.Rooms.Streaming.Audio;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Handles audio mixing and processing for multiple voice chat streams.
    /// Combines mono audio streams into stereo output with proper normalization.
    /// Implements IAudioFilter for integration with LiveKit's audio processing pipeline.
    /// </summary>
    public class VoiceChatCombinedStreamsAudioFilter : MonoBehaviour, IAudioFilter, IDisposable
    {
        private const int DEFAULT_LIVEKIT_CHANNELS = 2;

        private readonly HashSet<WeakReference<IAudioStream>> streams;
        private float[] tempBuffer;
        private int sampleRate = 48000;
        private bool isValid = true;

        public VoiceChatCombinedStreamsAudioFilter()
        {
            streams = new HashSet<WeakReference<IAudioStream>>();
        }

        public bool IsValid => isValid;

        public event IAudioFilter.OnAudioDelegate AudioRead;

        /// <summary>
        /// Processes audio data by mixing all active streams into the output buffer.
        /// </summary>
        /// <param name="data">Output audio buffer to fill</param>
        /// <param name="channels">Number of output channels (1=mono, 2=stereo)</param>
        /// <param name="isPlaying">Whether audio should be processed</param>
        public void ProcessAudio(float[] data, int channels, bool isPlaying)
        {
            if (!isPlaying || streams.Count == 0)
            {
                data.AsSpan().Clear();
                return;
            }

            EnsureTempBufferSize(channels, data.Length);

            Span<float> dataSpan = data.AsSpan();
            dataSpan.Clear();
            var activeStreams = 0;

            foreach (WeakReference<IAudioStream> weakStream in streams)
            {
                if (weakStream.TryGetTarget(out IAudioStream stream))
                {
                    Array.Clear(tempBuffer, 0, tempBuffer.Length);

                    // Read data from stream
                    stream.ReadAudio(tempBuffer, DEFAULT_LIVEKIT_CHANNELS, sampleRate);

                    // Mix into output buffer
                    if (channels != DEFAULT_LIVEKIT_CHANNELS)
                        MixMonoStreamIntoOutput(data, tempBuffer, channels, data.Length);
                    else
                    {
                        for (var i = 0; i < data.Length; i++)
                            data[i] += tempBuffer[i];
                    }

                    activeStreams++;
                }
            }

            // Normalize only if multiple streams
            if (activeStreams > 1)
            {
                NormalizeOutput(data, activeStreams);
            }
        }

        /// <summary>
        /// Processes audio data and raises the AudioRead event for LiveKit integration.
        /// </summary>
        /// <param name="data">Audio buffer to process</param>
        /// <param name="channels">Number of audio channels</param>
        /// <param name="sampleRate">Sample rate of the audio</param>
        public void ProcessAudioForLiveKit(Span<float> data, int channels, int sampleRate)
        {
            if (!isValid || streams.Count == 0)
            {
                data.Clear();
                return;
            }

            // Convert Span to array for processing
            var dataArray = data.ToArray();
            ProcessAudio(dataArray, channels, true);

            // Copy processed data back to span
            dataArray.AsSpan().CopyTo(data);

            // Raise event for LiveKit integration
            AudioRead?.Invoke(data, channels, sampleRate);
        }

        private void EnsureTempBufferSize(int channels, int dataLength)
        {
            int requiredSize = dataLength;

            if (tempBuffer == null || tempBuffer.Length != requiredSize)
            {
                tempBuffer = new float[requiredSize];
            }
        }

        private void MixMonoStreamIntoOutput(float[] output, float[] monoInput, int channels, int outputLength)
        {
            if (channels == 2)
            {
                // Upmix mono to stereo
                for (int i = 0, j = 0; i < outputLength; i += 2, j++)
                {
                    output[i] += monoInput[j];     // Left
                    output[i + 1] += monoInput[j]; // Right
                }
            }
            else
            {
                // Mono output
                for (var i = 0; i < outputLength; i++)
                    output[i] += monoInput[i];
            }
        }

        private void NormalizeOutput(float[] data, int activeStreams)
        {
            float norm = 1f / activeStreams;
            for (var i = 0; i < data.Length; i++)
                data[i] *= norm;
        }

        public void AddStream(WeakReference<IAudioStream> weakStream)
        {
            streams.Add(weakStream);
        }

        public void RemoveStream(WeakReference<IAudioStream> stream)
        {
            streams.Remove(stream);
        }

        public void Reset()
        {
            streams.Clear();

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);
        }

        public void Dispose()
        {
            isValid = false;
            Reset();
            AudioRead = null;
        }

        public void SetSampleRate(int newSampleRate)
        {
            sampleRate = newSampleRate;
        }

        public void Clear()
        {
            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);
        }
    }
}
