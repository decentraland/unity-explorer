using Arch.Core;
using DCL.ECSComponents;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class GatherMediaStreamDebugSystemShould : UnitySystemTestBase<GatherMediaStreamDebugSystem>
    {
        private MediaPlayerDebugRegistry registry;

        [SetUp]
        public void SetUp()
        {
            registry = new MediaPlayerDebugRegistry();

            ISceneStateProvider sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            system = new GatherMediaStreamDebugSystem(world, registry, sceneStateProvider, new ISceneData.Fake());
        }

        private Entity CreateMediaEntity()
        {
            // a pool-evicted backend (null MediaPlayer) exercises the IsValid guard,
            // so rows stop at the "(backend destroyed)" line
            var component = new MediaPlayerComponent(MultiMediaPlayer.FromAvProPlayer(new AvProPlayer(null!, null!)), isFromContentServer: false);
            return world.Create(new PBVideoPlayer { Src = "https://example.com/video.mp4" }, component);
        }

        [Test]
        public void StayPassiveWithoutARequest()
        {
            CreateMediaEntity();

            system!.Update(0);

            Assert.That(registry.LastCollectedFrame, Is.EqualTo(-1));
            Assert.That(registry.Rows, Is.Empty);
        }

        [Test]
        public void CollectOnRequestAndClearTheFlag()
        {
            CreateMediaEntity();
            registry.RequestCollect();

            system!.Update(0);

            Assert.That(registry.CollectRequested, Is.False);
            Assert.That(registry.LastCollectedFrame, Is.GreaterThanOrEqualTo(0));
            Assert.That(registry.VideoPlayerCount, Is.EqualTo(1));
            Assert.That(registry.AudioStreamCount, Is.EqualTo(0));
            Assert.That(registry.Rows[0].value, Is.EqualTo("https://example.com/video.mp4"));
        }

        [Test]
        public void CountAudioStreams()
        {
            var component = new MediaPlayerComponent(MultiMediaPlayer.FromAvProPlayer(new AvProPlayer(null!, null!)), isFromContentServer: false);
            world.Create(new PBAudioStream { Url = "https://example.com/audio.mp3" }, component);
            registry.RequestCollect();

            system!.Update(0);

            Assert.That(registry.AudioStreamCount, Is.EqualTo(1));
            Assert.That(registry.VideoPlayerCount, Is.EqualTo(0));
        }
    }
}
