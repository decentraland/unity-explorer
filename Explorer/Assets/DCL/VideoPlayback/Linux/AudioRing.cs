#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using System.Threading;

namespace DCL.VideoPlayback
{
    /// <summary>
    /// Single-producer / single-consumer ring of interleaved float frames. The
    /// decoder reader thread is the only writer of <c>head</c>; Unity's audio
    /// thread is the only writer of <c>tail</c>. Neither side ever moves the
    /// other's index, so a full ring simply refuses further writes and an empty
    /// ring hands back silence. Lock-free by construction: the audio thread must
    /// never block.
    /// </summary>
    internal sealed class AudioRing
    {
        // ffmpeg emits 7.1 as FL FR FC LFE BL BR SL SR; Unity's DSP graph orders
        // the same speakers FL FR FC LFE SL SR BL BR. Every other layout Unity
        // exposes (mono, stereo, quad, 5.0, 5.1) already matches ffmpeg's order.
        private static readonly int[] SEVEN_POINT_ONE_TO_UNITY_ORDER = { 0, 1, 2, 3, 6, 7, 4, 5 };

        private readonly float[] buffer;
        private readonly int capacityFrames;
        private readonly int[]? channelOrder;

        private volatile int headFrame;
        private volatile int tailFrame;
        private long consumedFrames;

        public int Channels { get; }

        public int SampleRate { get; }

        public int CapacityFrames => capacityFrames - 1;

        public int AvailableFrames
        {
            get
            {
                int available = headFrame - tailFrame;
                if (available < 0) available += capacityFrames;
                return available;
            }
        }

        public int FreeFrames => CapacityFrames - AvailableFrames;

        public bool IsEmpty => headFrame == tailFrame;

        /// <summary>Total frames handed to the consumer since construction; the presentation clock's audio anchor.</summary>
        public long ConsumedFrames => Volatile.Read(ref consumedFrames);

        public AudioRing(int channels, int sampleRate, double seconds)
        {
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));

            Channels = channels;
            SampleRate = sampleRate;
            capacityFrames = Math.Max(2, (int)Math.Ceiling(sampleRate * seconds)) + 1;
            buffer = new float[capacityFrames * channels];
            channelOrder = channels == SEVEN_POINT_ONE_TO_UNITY_ORDER.Length ? SEVEN_POINT_ONE_TO_UNITY_ORDER : null;
        }

        /// <summary>
        /// Producer side. Copies whole frames from <paramref name="src"/> until the
        /// ring is full and returns how many floats were taken; whatever did not
        /// fit is still owned by the producer and can be offered again later.
        /// </summary>
        public int Write(float[] src, int offset, int count)
        {
            int frames = Math.Min(count / Channels, FreeFrames);
            if (frames <= 0) return 0;

            int head = headFrame;
            int srcIndex = offset;

            for (int frame = 0; frame < frames; frame++)
            {
                int dstBase = head * Channels;

                if (channelOrder == null)
                    Array.Copy(src, srcIndex, buffer, dstBase, Channels);
                else
                    for (int channel = 0; channel < Channels; channel++)
                        buffer[dstBase + channel] = src[srcIndex + channelOrder[channel]];

                srcIndex += Channels;
                head++;
                if (head >= capacityFrames) head -= capacityFrames;
            }

            // Release-store: the slot writes above become visible before the index.
            Volatile.Write(ref headFrame, head);
            return frames * Channels;
        }

        /// <summary>
        /// Consumer side (Unity audio thread). Fills <paramref name="dst"/> with
        /// <paramref name="dstChannels"/>-interleaved frames, adapting the ring's
        /// layout to the requested one, and zero-fills whatever the ring cannot supply.
        /// Returns the number of frames consumed.
        /// </summary>
        public int Read(float[] dst, int dstChannels)
        {
            if (dstChannels <= 0)
            {
                Array.Clear(dst, 0, dst.Length);
                return 0;
            }

            int wanted = dst.Length / dstChannels;
            int head = Volatile.Read(ref headFrame);
            int tail = tailFrame;
            int available = head - tail;
            if (available < 0) available += capacityFrames;

            int frames = Math.Min(wanted, available);
            int dstIndex = 0;

            for (int frame = 0; frame < frames; frame++)
            {
                int srcBase = tail * Channels;

                if (dstChannels == Channels)
                {
                    Array.Copy(buffer, srcBase, dst, dstIndex, Channels);
                }
                else if (dstChannels == 1)
                {
                    dst[dstIndex] = Channels >= 2
                        ? (buffer[srcBase] + buffer[srcBase + 1]) * 0.5f
                        : buffer[srcBase];
                }
                else
                {
                    for (int channel = 0; channel < dstChannels; channel++)
                        dst[dstIndex + channel] = channel < Channels ? buffer[srcBase + channel] : 0f;
                }

                dstIndex += dstChannels;
                tail++;
                if (tail >= capacityFrames) tail -= capacityFrames;
            }

            if (dstIndex < dst.Length)
                Array.Clear(dst, dstIndex, dst.Length - dstIndex);

            Volatile.Write(ref tailFrame, tail);
            Volatile.Write(ref consumedFrames, consumedFrames + frames);
            return frames;
        }
    }
}
#endif
