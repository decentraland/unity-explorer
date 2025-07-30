using LiveKit;
using System;
using System.Collections.Generic;
using UnityEngine;
using LiveKit.Rooms.Streaming.Audio;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Handles audio mixing and processing for multiple voice chat streams.
    ///     Implements IAudioFilter for integration with LiveKit's audio processing pipeline.
    ///     Always processes stereo audio output.
    /// </summary>
    public class VoiceChatCombinedStreamsAudioFilter : MonoBehaviour, IAudioFilter, IDisposable
    {
        private const int STEREO_CHANNELS = 2;

        private readonly HashSet<WeakReference<IAudioStream>> streams;
        private int sampleRate = 48000;
        private float[] tempBuffer;

        public VoiceChatCombinedStreamsAudioFilter()
        {
            streams = new HashSet<WeakReference<IAudioStream>>();
        }

        public void Reset()
        {
            streams.Clear();

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);
        }

        public bool IsValid { get; private set; } = true;

        public event IAudioFilter.OnAudioDelegate AudioRead;

        public void Dispose()
        {
            IsValid = false;
            Reset();
            AudioRead = null;
        }

        /// <summary>
        ///     Processes audio data by mixing all active streams into stereo output buffer.
        /// </summary>
        /// <param name="data">Output stereo audio buffer to fill</param>
        /// <param name="isPlaying">Whether audio should be processed</param>
        private void ProcessAudio(float[] data, bool isPlaying)
        {
            if (!isPlaying || streams.Count == 0)
            {
                data.AsSpan().Clear();
                return;
            }

            EnsureTempBufferSize(data.Length);

            Span<float> dataSpan = data.AsSpan();
            dataSpan.Clear();
            var activeStreams = 0;

            foreach (WeakReference<IAudioStream> weakStream in streams)
            {
                if (weakStream.TryGetTarget(out IAudioStream stream))
                {
                    Array.Clear(tempBuffer, 0, tempBuffer.Length);

                    // Read data from stream
                    stream.ReadAudio(tempBuffer, STEREO_CHANNELS, sampleRate);

                    // Mix into stereo output buffer
                    for (var i = 0; i < data.Length; i++)
                        data[i] += tempBuffer[i];

                    activeStreams++;
                }
            }

            // Normalize only if multiple streams
            if (activeStreams > 1) { NormalizeOutput(data, activeStreams); }
        }

        /// <summary>
        ///     Processes audio data and raises the AudioRead event for LiveKit integration.
        ///     Always processes stereo audio.
        /// </summary>
        /// <param name="data">Stereo audio buffer to process</param>
        /// <param name="sampleRate">Sample rate of the audio</param>
        public void ProcessAudioForLiveKit(Span<float> data, int sampleRate)
        {
            if (!IsValid || streams.Count == 0)
            {
                data.Clear();
                return;
            }

            // Convert Span to array for processing
            float[] dataArray = data.ToArray();
            ProcessAudio(dataArray, true);

            // Copy processed data back to span
            dataArray.AsSpan().CopyTo(data);

            // Raise event for LiveKit integration with stereo channels
            AudioRead?.Invoke(data, STEREO_CHANNELS, sampleRate);
        }

        private void EnsureTempBufferSize(int dataLength)
        {
            if (tempBuffer == null || tempBuffer.Length != dataLength) 
            { 
                tempBuffer = new float[dataLength]; 
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
