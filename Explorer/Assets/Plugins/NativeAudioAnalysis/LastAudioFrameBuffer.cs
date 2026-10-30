// TRUST_WEBGL_THREAD_SAFETY_FLAG

using System;
using System.Threading;
using UnityEngine;

namespace Plugins.NativeAudioAnalysis
{
    /// <summary>
    ///     Keeps the most recent audio buffer written from the audio thread until the main thread copies it out.
    ///     Frames written while the previous one is still pending or being read are dropped, so neither side ever blocks.
    /// </summary>
    public sealed class LastAudioFrameBuffer
    {
        private const int FREE = 0;
        private const int WRITING = 1;
        private const int READY = 2;
        private const int READING = 3;

        private int state = FREE;
        private float[] samples;
        private int sampleCount;
        private int channels;

        public LastAudioFrameBuffer(int capacity = 0)
        {
            samples = capacity > 0 ? new float[capacity] : Array.Empty<float>();
        }

        /// <summary>
        ///     Number of samples the engine hands to one DSP callback: the DSP buffer length times the output channel count.
        ///     Main thread only.
        /// </summary>
        public static int OutputFrameLength()
        {
            AudioSettings.GetDSPBufferSize(out int bufferLength, out _);
            return bufferLength * ChannelCount(AudioSettings.speakerMode);
        }

        /// <summary>
        ///     Main thread sizes the slot ahead of the first write so the audio thread never allocates.
        ///     A pending frame is dropped; when the audio thread holds the slot right now, <see cref="Write" /> resizes once on its own.
        /// </summary>
        public void EnsureCapacity(int capacity)
        {
            if (samples.Length >= capacity)
                return;

            Reset();

            if (Interlocked.CompareExchange(ref state, WRITING, FREE) != FREE)
                return;

            samples = new float[capacity];
            Volatile.Write(ref state, FREE);
        }

        /// <summary>
        ///     Main thread copies the latest frame out. The slot stays owned by the reader until the copy is done,
        ///     so the audio thread cannot overwrite a frame mid-read.
        /// </summary>
        public bool TryConsume(Span<float> destination, out int outChannels, out int outSampleCount)
        {
            if (Interlocked.CompareExchange(ref state, READING, READY) != READY)
            {
                outChannels = 0;
                outSampleCount = 0;
                return false;
            }

            outSampleCount = Math.Min(sampleCount, destination.Length);
            samples.AsSpan(0, outSampleCount).CopyTo(destination);
            outChannels = channels;
            Volatile.Write(ref state, FREE);
            return true;
        }

        /// <summary>
        ///     Audio thread publishes a frame. Dropped when the previous frame has not been consumed yet.
        /// </summary>
        public void Write(float[] data, int dataChannels)
        {
            if (Interlocked.CompareExchange(ref state, WRITING, FREE) != FREE)
                return;

            if (samples.Length < data.Length)
                samples = new float[data.Length];

            data.CopyTo(samples, 0);
            sampleCount = data.Length;
            channels = dataChannels;
            Volatile.Write(ref state, READY);
        }

        /// <summary>
        ///     Discards a pending frame so a consumer cannot read audio produced before a pause or re-enable.
        ///     A frame being written at this moment is published as usual.
        /// </summary>
        public void Reset()
        {
            Interlocked.CompareExchange(ref state, FREE, READY);
        }

        private static int ChannelCount(AudioSpeakerMode mode) =>
            mode switch
            {
                AudioSpeakerMode.Mono => 1,
                AudioSpeakerMode.Stereo => 2,
                AudioSpeakerMode.Quad => 4,
                AudioSpeakerMode.Surround => 5,
                AudioSpeakerMode.Mode5point1 => 6,
                AudioSpeakerMode.Mode7point1 => 8,
                _ => 2,
            };
    }
}
