using NUnit.Framework;

namespace Plugins.NativeAudioAnalysis.Tests
{
    public class LastAudioFrameBufferShould
    {
        private const int SAMPLE_RATE = 48000;
        private const int CHANNELS = 2;

        [Test]
        public void ReturnNothingBeforeFirstWrite()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer(SAMPLE_RATE);

            // Act
            bool consumed = buffer.TryConsume(out float[]? output, out _, out int sampleRate);

            // Assert
            Assert.That(consumed, Is.False);
            Assert.That(output, Is.Null);
            Assert.That(sampleRate, Is.EqualTo(SAMPLE_RATE));
        }

        [Test]
        public void HandOverTheWrittenFrameOnce()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer(SAMPLE_RATE);
            float[] frame = { 0.1f, 0.2f, 0.3f, 0.4f };

            // Act
            buffer.Write(frame, CHANNELS);
            bool firstConsume = buffer.TryConsume(out float[]? output, out int channels, out _);
            bool secondConsume = buffer.TryConsume(out float[]? secondOutput, out _, out _);

            // Assert
            Assert.That(firstConsume, Is.True);
            Assert.That(output, Is.EqualTo(frame));
            Assert.That(channels, Is.EqualTo(CHANNELS));
            Assert.That(secondConsume, Is.False);
            Assert.That(secondOutput, Is.Null);
        }

        [Test]
        public void DropWritesUntilThePendingFrameIsConsumed()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer(SAMPLE_RATE);
            float[] first = { 1f, 1f };
            float[] second = { 2f, 2f };

            // Act
            buffer.Write(first, CHANNELS);
            buffer.Write(second, CHANNELS);
            buffer.TryConsume(out float[]? output, out _, out _);

            // Assert
            Assert.That(output, Is.EqualTo(first));
        }

        [Test]
        public void CopyTheFrameInsteadOfKeepingTheCallerArray()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer(SAMPLE_RATE);
            float[] frame = { 0.5f, 0.5f };

            // Act
            buffer.Write(frame, CHANNELS);
            frame[0] = 9f;
            buffer.TryConsume(out float[]? output, out _, out _);

            // Assert
            Assert.That(output, Is.Not.SameAs(frame));
            Assert.That(output![0], Is.EqualTo(0.5f));
        }

        [Test]
        public void DiscardThePendingFrameOnReset()
        {
            // Arrange
            var buffer = new LastAudioFrameBuffer(SAMPLE_RATE);
            buffer.Write(new[] { 0.5f, 0.5f }, CHANNELS);

            // Act
            buffer.Reset();
            bool consumed = buffer.TryConsume(out _, out _, out _);

            // Assert
            Assert.That(consumed, Is.False);
        }
    }
}
