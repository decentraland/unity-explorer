using NUnit.Framework;

namespace Plugins.NativeAudioAnalysis.Tests
{
    public class LastAudioFrameBufferShould
    {
        private const int CHANNELS = 2;

        [Test]
        public void ReturnNothingBeforeFirstWrite()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer();
            float[] destination = { 7f, 7f };

            // Act
            bool consumed = buffer.TryConsume(destination, out _, out int sampleCount);

            // Assert
            Assert.That(consumed, Is.False);
            Assert.That(sampleCount, Is.EqualTo(0));
            Assert.That(destination, Is.EqualTo(new[] { 7f, 7f }));
        }

        [Test]
        public void HandOverTheWrittenFrameOnce()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer();
            float[] frame = { 0.1f, 0.2f, 0.3f, 0.4f };
            float[] destination = new float[4];

            // Act
            buffer.Write(frame, CHANNELS);
            bool firstConsume = buffer.TryConsume(destination, out int channels, out int sampleCount);
            bool secondConsume = buffer.TryConsume(destination, out _, out _);

            // Assert
            Assert.That(firstConsume, Is.True);
            Assert.That(destination, Is.EqualTo(frame));
            Assert.That(channels, Is.EqualTo(CHANNELS));
            Assert.That(sampleCount, Is.EqualTo(frame.Length));
            Assert.That(secondConsume, Is.False);
        }

        [Test]
        public void DropWritesUntilThePendingFrameIsConsumed()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer();
            float[] first = { 1f, 1f };
            float[] second = { 2f, 2f };
            float[] destination = new float[2];

            // Act
            buffer.Write(first, CHANNELS);
            buffer.Write(second, CHANNELS);
            buffer.TryConsume(destination, out _, out _);

            // Assert
            Assert.That(destination, Is.EqualTo(first));
        }

        [Test]
        public void CopyTheFrameOnWrite()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer();
            float[] frame = { 0.5f, 0.5f };
            float[] destination = new float[2];

            // Act
            buffer.Write(frame, CHANNELS);
            frame[0] = 9f;
            buffer.TryConsume(destination, out _, out _);

            // Assert
            Assert.That(destination[0], Is.EqualTo(0.5f));
        }

        [Test]
        public void CopyOnlyAsManySamplesAsTheDestinationHolds()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer();
            float[] destination = new float[2];

            // Act
            buffer.Write(new[] { 1f, 2f, 3f, 4f }, CHANNELS);
            bool consumed = buffer.TryConsume(destination, out _, out int sampleCount);

            // Assert
            Assert.That(consumed, Is.True);
            Assert.That(sampleCount, Is.EqualTo(2));
            Assert.That(destination, Is.EqualTo(new[] { 1f, 2f }));
        }

        [Test]
        public void ReportTheFrameLengthWhenTheDestinationIsLarger()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer();
            float[] destination = { 7f, 7f, 7f, 7f };

            // Act
            buffer.Write(new[] { 1f, 2f }, CHANNELS);
            buffer.TryConsume(destination, out _, out int sampleCount);

            // Assert
            Assert.That(sampleCount, Is.EqualTo(2));
            Assert.That(destination, Is.EqualTo(new[] { 1f, 2f, 7f, 7f }));
        }

        [Test]
        public void ReportTheChannelCountOfTheConsumedFrame()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer();
            float[] destination = new float[2];
            buffer.Write(new[] { 0.5f, 0.5f }, CHANNELS);
            buffer.TryConsume(destination, out _, out _);

            // Act
            buffer.Write(new[] { 0.5f }, 1);
            buffer.TryConsume(destination, out int channels, out _);

            // Assert
            Assert.That(channels, Is.EqualTo(1));
        }

        [Test]
        public void DiscardThePendingFrameOnReset()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer();
            buffer.Write(new[] { 0.5f, 0.5f }, CHANNELS);

            // Act
            buffer.Reset();
            bool consumed = buffer.TryConsume(new float[2], out _, out _);

            // Assert
            Assert.That(consumed, Is.False);
        }

        [Test]
        public void AcceptAFrameWrittenAfterReset()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer();
            float[] destination = new float[2];
            buffer.Write(new[] { 0.5f, 0.5f }, CHANNELS);
            buffer.Reset();

            // Act
            buffer.Write(new[] { 0.25f, 0.25f }, CHANNELS);
            bool consumed = buffer.TryConsume(destination, out _, out _);

            // Assert
            Assert.That(consumed, Is.True);
            Assert.That(destination[0], Is.EqualTo(0.25f));
        }

        [Test]
        public void KeepAcceptingFramesAfterEnsureCapacity()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer(2);
            float[] destination = new float[4];

            // Act
            buffer.EnsureCapacity(4);
            buffer.Write(new[] { 1f, 2f, 3f, 4f }, CHANNELS);
            bool consumed = buffer.TryConsume(destination, out _, out int sampleCount);

            // Assert
            Assert.That(consumed, Is.True);
            Assert.That(sampleCount, Is.EqualTo(4));
            Assert.That(destination, Is.EqualTo(new[] { 1f, 2f, 3f, 4f }));
        }
    }
}
