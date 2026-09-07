#if UNITY_EDITOR_LINUX
using NUnit.Framework;

namespace DCL.VideoPlayback.Tests
{
    public class AudioRingShould
    {
        private static float[] Frames(int frames, int channels, float start = 0f)
        {
            var data = new float[frames * channels];
            for (var i = 0; i < data.Length; i++) data[i] = start + i;
            return data;
        }

        [Test]
        public void HandBackWrittenFramesInOrderAndCountThem()
        {
            // Arrange
            var ring = new AudioRing(2, 48000, 0.01);
            float[] input = Frames(10, 2);
            var output = new float[8];

            // Act
            int written = ring.Write(input, 0, input.Length);
            int consumed = ring.Read(output, 2);

            // Assert
            Assert.AreEqual(20, written);
            Assert.AreEqual(4, consumed);
            CollectionAssert.AreEqual(new float[] { 0, 1, 2, 3, 4, 5, 6, 7 }, output);
            Assert.AreEqual(4, ring.ConsumedFrames);
            Assert.AreEqual(6, ring.AvailableFrames);
        }

        [Test]
        public void ZeroFillWhatItCannotSupply()
        {
            // Arrange
            var ring = new AudioRing(2, 48000, 0.01);
            ring.Write(Frames(2, 2, 10f), 0, 4);
            float[] output = { 9, 9, 9, 9, 9, 9 };

            // Act
            int consumed = ring.Read(output, 2);

            // Assert
            Assert.AreEqual(2, consumed);
            CollectionAssert.AreEqual(new float[] { 10, 11, 12, 13, 0, 0 }, output);
        }

        [Test]
        public void RefuseWritesBeyondCapacityInsteadOfOverwriting()
        {
            // Arrange: capacity of 480 frames at 48 kHz / 10 ms.
            var ring = new AudioRing(2, 48000, 0.01);
            float[] input = Frames(600, 2);

            // Act
            int first = ring.Write(input, 0, input.Length);
            int second = ring.Write(input, first, input.Length - first);

            // Assert
            Assert.AreEqual(ring.CapacityFrames * 2, first);
            Assert.AreEqual(0, second);
            Assert.AreEqual(0, ring.FreeFrames);
        }

        [Test]
        public void WrapAroundTheEndOfTheBuffer()
        {
            // Arrange
            var ring = new AudioRing(1, 48000, 0.0001);
            int capacity = ring.CapacityFrames;
            var scratch = new float[capacity];

            for (var round = 0; round < 3; round++)
            {
                float[] input = Frames(capacity, 1, round * 100f);

                // Act
                int written = ring.Write(input, 0, input.Length);
                int consumed = ring.Read(scratch, 1);

                // Assert
                Assert.AreEqual(capacity, written);
                Assert.AreEqual(capacity, consumed);
                Assert.AreEqual(round * 100f, scratch[0]);
                Assert.AreEqual(round * 100f + capacity - 1, scratch[capacity - 1]);
            }
        }

        [Test]
        public void DownmixStereoToMono()
        {
            // Arrange
            var ring = new AudioRing(2, 48000, 0.01);
            ring.Write(new float[] { 0.2f, 0.4f, -1f, 1f }, 0, 4);
            var output = new float[2];

            // Act
            ring.Read(output, 1);

            // Assert
            Assert.AreEqual(0.3f, output[0], 1e-6f);
            Assert.AreEqual(0f, output[1], 1e-6f);
        }

        [Test]
        public void SpreadStereoIntoWiderLayoutsWithSilentExtras()
        {
            // Arrange
            var ring = new AudioRing(2, 48000, 0.01);
            ring.Write(new float[] { 1f, 2f }, 0, 2);
            var output = new float[6];

            // Act
            ring.Read(output, 6);

            // Assert
            CollectionAssert.AreEqual(new float[] { 1, 2, 0, 0, 0, 0 }, output);
        }

        [Test]
        public void KeepTheFrontPairWhenNarrowingSurround()
        {
            // Arrange
            var ring = new AudioRing(6, 48000, 0.01);
            ring.Write(new float[] { 1, 2, 3, 4, 5, 6 }, 0, 6);
            var output = new float[2];

            // Act
            ring.Read(output, 2);

            // Assert
            CollectionAssert.AreEqual(new float[] { 1, 2 }, output);
        }

        [Test]
        public void ReorderSevenPointOneIntoUnitySpeakerOrder()
        {
            // Arrange: ffmpeg order FL FR FC LFE BL BR SL SR.
            var ring = new AudioRing(8, 48000, 0.01);
            ring.Write(new float[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 0, 8);
            var output = new float[8];

            // Act
            ring.Read(output, 8);

            // Assert: Unity order FL FR FC LFE SL SR BL BR.
            CollectionAssert.AreEqual(new float[] { 1, 2, 3, 4, 7, 8, 5, 6 }, output);
        }
    }
}
#endif
