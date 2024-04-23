using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
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
    public class CreateMediaPlayerSystemShould : UnitySystemTestBase<CreateMediaPlayerSystem>
    {
        private MediaPlayerComponent component;
        private Entity entity;
        private MediaPlayer mediaPlayerGameObject;

        [SetUp]
        public void SetUp()
        {
            system = CreateSystem(world);

            CreateMediaPlayerSystem CreateSystem(World world)
            {
                IComponentPoolsRegistry poolsRegistry = Substitute.For<IComponentPoolsRegistry>();
                IComponentPool<MediaPlayer> mediaPlayersPool = Substitute.For<IComponentPool<MediaPlayer>>();

                poolsRegistry.GetReferenceTypePool<MediaPlayer>().Returns(mediaPlayersPool);

                mediaPlayerGameObject = new GameObject().AddComponent<MediaPlayer>();
                mediaPlayersPool.Get().Returns(mediaPlayerGameObject);

                ISceneStateProvider sceneStateProvider = Substitute.For<ISceneStateProvider>();
                sceneStateProvider.IsCurrent.Returns(true);

                IPerformanceBudget budgetProvider = Substitute.For<IPerformanceBudget>();
                budgetProvider.TrySpendBudget().Returns(true);

                return new CreateMediaPlayerSystem(world, null, mediaPlayersPool, sceneStateProvider, budgetProvider);
            }
        }

        [TearDown]
        public void DisposeMediaPlayer()
        {
            UnityObjectUtils.SafeDestroy(mediaPlayerGameObject.gameObject);
        }

        [TestCase("http://ice3.somafm.com/dronezone-128-mp3", 0.5f)]
        [TestCase("http://ice3.somafm.com/defcon-128-mp3", 1f)]
        public void AttachMediaPlayerComponent_ForPBAudioStream(string url, float volume)
        {
            // Arrange
            var pbAudioStream = new PBAudioStream { Url = url, Volume = volume };

            entity = world.Create(pbAudioStream, PartitionComponent.TOP_PRIORITY); // Create entity
            AddTransformToEntity(entity);

            // Act
            system.Update(0);

            // Assert
            MediaPlayerComponent mediaPlayerComponent = world.Get<MediaPlayerComponent>(entity);
            Assert.That(mediaPlayerComponent.State, Is.EqualTo(VideoState.VsNone));
            Assert.That(mediaPlayerComponent.URL, Is.EqualTo(pbAudioStream.Url));

            MediaPlayer mediaPlayer = mediaPlayerComponent.MediaPlayer;
            Assert.That(mediaPlayer, Is.Not.Null);
            Assert.That(mediaPlayer.AudioVolume, Is.EqualTo(pbAudioStream.Volume));
        }

        [TestCase("http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4", 0.5f, true)]
        [TestCase("https://player.vimeo.com/external/552481870.m3u8?s=c312c8533f97e808fccc92b0510b085c8122a875", 1f, false)]
        [TestCase("https://player.vimeo.com/external/878776548.m3u8?s=e6e54ac3862fe71ac3ecbdb2abbfdd7ca7daafaf&logging=false", 0.1f, true)]
        public void AttachMediaPlayerComponent_ForPBVideoPlayer_WhenVideoTextureComponentIsPresent(string src, float volume, bool loop)
        {
            // Arrange
            var pbVideoPlayer = new PBVideoPlayer
            {
                Src = src,
                Volume = volume,
                Playing = true,
                Loop = loop
            };

            entity = world.Create(pbVideoPlayer, new VideoTextureComponent( new Texture2D(1, 1) ), PartitionComponent.TOP_PRIORITY); // Create entity
            AddTransformToEntity(entity);

            // Act
            system.Update(0);

            // Assert
            MediaPlayerComponent mediaPlayerComponent = world.Get<MediaPlayerComponent>(entity);
            Assert.That(mediaPlayerComponent.State, Is.EqualTo(VideoState.VsNone));
            Assert.That(mediaPlayerComponent.URL, Is.EqualTo(pbVideoPlayer.Src));

            MediaPlayer mediaPlayer = mediaPlayerComponent.MediaPlayer;
            Assert.That(mediaPlayer, Is.Not.Null);
            Assert.That(mediaPlayer.AudioVolume, Is.EqualTo(pbVideoPlayer.Volume));
            Assert.That(mediaPlayer.Control.IsLooping(), Is.EqualTo(pbVideoPlayer.Loop));
        }

        [Test]
        public void DontAttachMediaPlayerComponent_ForPBVideoPlayer_WhenVideoTextureComponentIsNotPresent()
        {
            // Arrange
            var pbVideoPlayer = new PBVideoPlayer { Src = "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4" };

            entity = world.Create(pbVideoPlayer, PartitionComponent.TOP_PRIORITY); // Create entity
            AddTransformToEntity(entity);

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(world.TryGet<MediaPlayerComponent>(entity, out _));
        }
    }
}
