using System;
using System.Threading;

namespace UUAV
{
    /// <summary>
    /// Single-slot hand-off of the latest DSP buffer from the audio mixer
    /// thread to the main thread. A frame produced while the previous one is
    /// still pending or being read is dropped, so neither side ever blocks.
    /// </summary>
    internal sealed class AudioFrameSnapshot
    {
        private const int FREE = 0;
        private const int WRITING = 1;
        private const int READY = 2;
        private const int READING = 3;

        private int state = FREE;
        private float[] samples = Array.Empty<float>();
        private int sampleCount;
        private int channels;

        // main thread. Sizes the slot ahead of the first write so the audio
        // thread never allocates. A pending frame is dropped; when the audio
        // thread holds the slot right now, Write resizes once on its own.
        public void EnsureCapacity(int capacity)
        {
            if (samples.Length >= capacity)
            {
                return;
            }

            Reset();

            if (Interlocked.CompareExchange(ref state, WRITING, FREE) != FREE)
            {
                return;
            }

            samples = new float[capacity];
            Volatile.Write(ref state, FREE);
        }

        // audio mixer thread
        public void Write(float[] data, int dataChannels)
        {
            if (Interlocked.CompareExchange(ref state, WRITING, FREE) != FREE)
            {
                return;
            }

            if (samples.Length < data.Length)
            {
                samples = new float[data.Length];
            }

            data.CopyTo(samples, 0);
            sampleCount = data.Length;
            channels = dataChannels;
            Volatile.Write(ref state, READY);
        }

        // main thread. The slot stays owned by the reader until the copy is
        // done, so the audio thread cannot overwrite a frame mid-read.
        public bool TryRead(Span<float> destination, out int outChannels, out int outSampleCount)
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

        // drops a pending frame; a frame being written right now is
        // published as usual
        public void Reset()
        {
            Interlocked.CompareExchange(ref state, FREE, READY);
        }
    }
}
