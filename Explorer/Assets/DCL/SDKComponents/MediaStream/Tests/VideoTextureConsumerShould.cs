using ECS.Unity.Textures.Components;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class VideoTextureConsumerShould
    {
        private IObjectPool<RenderTexture> videoTexturesPool = null!;
        private RenderTexture renderTexture = null!;

        [SetUp]
        public void SetUp()
        {
            renderTexture = new RenderTexture(2, 2, 0);
            videoTexturesPool = Substitute.For<IObjectPool<RenderTexture>>();
            videoTexturesPool.Get().Returns(renderTexture);
        }

        [TearDown]
        public void TearDown()
        {
            UnityObjectUtils.SafeDestroy(renderTexture);
        }

        [Test]
        public void StartWithoutScreenSpaceConsumers()
        {
            // Act
            var consumer = new VideoTextureConsumer(videoTexturesPool);

            // Assert
            Assert.That(consumer.ScreenSpaceConsumers, Is.Zero);
        }

        [Test]
        public void CountScreenSpaceConsumers()
        {
            // Arrange
            var consumer = new VideoTextureConsumer(videoTexturesPool);

            // Act
            consumer.AddScreenSpaceConsumer();
            consumer.AddScreenSpaceConsumer();
            consumer.RemoveScreenSpaceConsumer();

            // Assert
            Assert.That(consumer.ScreenSpaceConsumers, Is.EqualTo(1));
        }

        [Test]
        public void NotCountScreenSpaceConsumersBelowZero()
        {
            // Arrange
            var consumer = new VideoTextureConsumer(videoTexturesPool);

            // Act
            consumer.RemoveScreenSpaceConsumer();

            // Assert
            Assert.That(consumer.ScreenSpaceConsumers, Is.Zero);
        }

        [Test]
        public void ResetScreenSpaceConsumersOnDispose()
        {
            // Arrange
            var consumer = new VideoTextureConsumer(videoTexturesPool);
            consumer.AddScreenSpaceConsumer();

            // Act
            consumer.Dispose();

            // Assert
            Assert.That(consumer.ScreenSpaceConsumers, Is.Zero);
            videoTexturesPool.Received(1).Release(renderTexture);
        }
    }
}
