// TRUST_WEBGL_THREAD_SAFETY_FLAG

using System;
using System.Threading;

namespace Plugins.NativeAudioAnalysis
{
    /// <summary>
    ///     Keeps the most recent audio buffer written from the audio thread until the main thread consumes it.
    ///     Frames written while the previous one is still unconsumed are dropped, so neither side ever blocks.
    /// </summary>
    public sealed class LastAudioFrameBuffer
    {
        private const int FREE = 0;
        private const int WRITING = 1;
        private const int READY = 2;

        private int state = FREE;
        private float[] buffer = Array.Empty<float>();
        private int channels;

        /// <summary>
        ///     Rate the published frames are sampled at. Written on the main thread only.
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        ///     Main thread tries to consume the latest audio frame. Output is not owned and must be used in place.
        /// </summary>
        public bool TryConsume(out float[]? output, out int outChannels, out int outSampleRate)
        {
            outSampleRate = SampleRate;

            bool result = Interlocked.CompareExchange(ref state, FREE, READY) == READY;
            outChannels = channels;
            output = result ? buffer : null;
            return result;
        }

        /// <summary>
        ///     Audio thread publishes a frame. Dropped when the previous frame has not been consumed yet.
        /// </summary>
        public void Write(float[] data, int dataChannels)
        {
            if (Interlocked.CompareExchange(ref state, WRITING, FREE) != FREE)
                return;

            // One-time resize only: the DSP buffer size is stable for the lifetime of the audio configuration.
            if (buffer.Length != data.Length)
                buffer = new float[data.Length];

            data.CopyTo(buffer, 0);
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
    }
}
