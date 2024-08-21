using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.TestSuite;
using ECS.Unity.Textures.Components;
using NSubstitute;
using NUnit.Framework;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class CleanUpMediaPlayerShould : UnitySystemTestBase<CleanUpMediaPlayerSystem>
    {
        private MediaPlayer mediaPlayerGameObject;

        private IComponentPool<MediaPlayer> mediaPlayerPool;
        private IComponentPool<Texture2D> videoTexturePool;

        [SetUp]
        public void SetUp()
        {
            ISceneStateProvider sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            mediaPlayerGameObject = new GameObject().AddComponent<MediaPlayer>();

            mediaPlayerPool = Substitute.For<IComponentPool<MediaPlayer>>();
            videoTexturePool = Substitute.For<IComponentPool<Texture2D>>();

            system = new CleanUpMediaPlayerSystem(world, mediaPlayerPool, videoTexturePool);
        }

        [Test]
        public void CleanAbandonedMediaPlayer()
        {
            var videoPlayer = new PBVideoPlayer();
            var videoTexConsumer = new VideoTextureConsumer(Texture2D.blackTexture);
            var mediaPlayer = new MediaPlayerComponent { MediaPlayer = mediaPlayerGameObject };

            Entity entity = world.Create(videoPlayer, videoTexConsumer, mediaPlayer);

            system.Update(0);

            mediaPlayerPool.Received(1).Release(mediaPlayer.MediaPlayer);
            videoTexturePool.Received(1).Release(videoTexConsumer.Texture);
            Assert.That(world.Has<MediaPlayerComponent>(entity), Is.False);
            Assert.That(world.Has<VideoTextureConsumer>(entity), Is.False);
        }

        [TearDown]
        public void DisposeMediaPlayer()
        {
            UnityObjectUtils.SafeDestroy(mediaPlayerGameObject.gameObject);
        }
    }
}
