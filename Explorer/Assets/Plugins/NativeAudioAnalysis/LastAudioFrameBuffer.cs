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
        // 0 = nothing written or already consumed, 1 = new frame available.
        private int readyFlag;
        private float[] buffer = Array.Empty<float>();
        private int channels;

        public int SampleRate { get; }

        public LastAudioFrameBuffer(int sampleRate)
        {
            SampleRate = sampleRate;
        }

        /// <summary>
        ///     Main thread tries to consume the latest audio frame. Output is not owned and must be used in place.
        /// </summary>
        public bool TryConsume(out float[]? output, out int outChannels, out int outSampleRate)
        {
            outChannels = channels;
            outSampleRate = SampleRate;

            // Atomically check and clear readyFlag.
            bool result = Interlocked.Exchange(ref readyFlag, 0) == 1;
            output = result ? buffer : null;
            return result;
        }

        /// <summary>
        ///     Audio thread publishes a frame. Dropped when the previous frame has not been consumed yet.
        /// </summary>
        public void Write(float[] data, int dataChannels)
        {
            if (Interlocked.CompareExchange(ref readyFlag, 1, 0) != 0)
                return;

            // One-time resize only: the DSP buffer size is stable for the lifetime of the audio configuration.
            if (buffer.Length != data.Length)
                buffer = new float[data.Length];

            data.CopyTo(buffer, 0);
            channels = dataChannels;
        }

        /// <summary>
        ///     Discards a pending frame so a consumer cannot read audio produced before a pause or re-enable.
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref readyFlag, 0);
        }
    }
}
