using Arch.Core;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using ECS.Unity.Textures.Components;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class CleanUpMediaPlayerShould : UnitySystemTestBase<CleanUpMediaPlayerSystem>
    {
        private readonly List<Object> createdObjects = new ();

        private IObjectPool<RenderTexture> videoTexturesPool = null!;

        [SetUp]
        public void SetUp()
        {
            videoTexturesPool = Substitute.For<IObjectPool<RenderTexture>>();

            videoTexturesPool.Get().Returns(_ =>
            {
                var renderTexture = new RenderTexture(2, 2, 0);
                createdObjects.Add(renderTexture);
                return renderTexture;
            });

            system = new CleanUpMediaPlayerSystem(world);
        }

        protected override void OnTearDown()
        {
            foreach (Object createdObject in createdObjects)
                UnityObjectUtils.SafeDestroy(createdObject);

            createdObjects.Clear();
        }

        [Test]
        public void ReleaseConsumerAndItsTextureDataWhenNothingReferencesIt()
        {
            // Arrange
            var consumer = new VideoTextureConsumer(videoTexturesPool);
            var textureData = new TextureData(AnyTexture.FromVideoTextureData(new VideoTextureData(consumer, default(MediaPlayerComponent))));
            Entity entity = world.Create(consumer, textureData);

            // Act
            system.Update(0);

            // Assert
            videoTexturesPool.Received(1).Release(consumer.Texture);
            Assert.That(world.Has<VideoTextureConsumer>(entity), Is.False);
            Assert.That(world.Has<TextureData>(entity), Is.False, "a stale TextureData would hand the released render texture to the next consumer");
        }

        [Test]
        public void KeepConsumerAndTextureDataWhileReferenced()
        {
            // Arrange
            var consumer = new VideoTextureConsumer(videoTexturesPool);
            var textureData = new TextureData(AnyTexture.FromVideoTextureData(new VideoTextureData(consumer, default(MediaPlayerComponent))));
            textureData.AddReference();
            Entity entity = world.Create(consumer, textureData);

            // Act
            system.Update(0);

            // Assert
            videoTexturesPool.DidNotReceive().Release(Arg.Any<RenderTexture>());
            Assert.That(world.Has<VideoTextureConsumer>(entity), Is.True);
            Assert.That(world.Get<TextureData>(entity), Is.SameAs(textureData));
        }
    }
}
