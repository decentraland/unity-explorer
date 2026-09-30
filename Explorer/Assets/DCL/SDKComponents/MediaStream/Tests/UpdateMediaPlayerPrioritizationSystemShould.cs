using Arch.Core;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.SDKComponents.MediaStream.Settings;
using ECS.TestSuite;
using ECS.Unity.Textures.Components;
using LiveKit.Rooms;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class UpdateMediaPlayerPrioritizationSystemShould : UnitySystemTestBase<UpdateMediaPlayerPrioritizationSystem>
    {
        private readonly List<Object> createdObjects = new ();
        private readonly List<LivekitPlayer> createdPlayers = new ();

        private VideoPrioritizationSettings settings = null!;
        private IObjectPool<RenderTexture> videoTexturesPool = null!;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<VideoPrioritizationSettings>();
            settings.MaximumSimultaneousVideos = 1;
            createdObjects.Add(settings);

            // The camera sits at the origin; the system is not initialized on purpose so no FOV is cached:
            // only a video whose bounds contain the camera can then be scored by its renderers
            IExposedCameraData cameraData = Substitute.For<IExposedCameraData>();
            cameraData.WorldPosition.Returns(new CanBeDirty<Vector3>(Vector3.zero));
            cameraData.WorldRotation.Returns(new CanBeDirty<Quaternion>(Quaternion.identity));

            videoTexturesPool = Substitute.For<IObjectPool<RenderTexture>>();

            videoTexturesPool.Get().Returns(_ =>
            {
                var renderTexture = new RenderTexture(2, 2, 0);
                createdObjects.Add(renderTexture);
                return renderTexture;
            });

            system = new UpdateMediaPlayerPrioritizationSystem(world, cameraData, settings);
        }

        protected override void OnTearDown()
        {
            foreach (LivekitPlayer player in createdPlayers)
                player.Dispose();

            foreach (Object createdObject in createdObjects)
                UnityObjectUtils.SafeDestroy(createdObject);

            createdPlayers.Clear();
            createdObjects.Clear();
        }

        [Test]
        [TestCase(0f)]
        [TestCase(-1f)]
        public void NotResumeSeekWhileDurationIsUnknown(float duration)
        {
            // Act
            bool hasResumeTime = UpdateMediaPlayerPrioritizationSystem.TryGetResumeTime(12.5d, duration, out double resumeTime);

            // Assert
            Assert.That(hasResumeTime, Is.False);
            Assert.That(resumeTime, Is.Zero);
        }

        [Test]
        public void WrapResumeTimeAroundDuration()
        {
            // Act
            bool hasResumeTime = UpdateMediaPlayerPrioritizationSystem.TryGetResumeTime(25d, 10f, out double resumeTime);

            // Assert
            Assert.That(hasResumeTime, Is.True);
            Assert.That(resumeTime, Is.EqualTo(5d).Within(1e-6));
        }

        [Test]
        public void NotResumeSeekWhenPauseDurationIsNotFinite()
        {
            // Act
            bool hasResumeTime = UpdateMediaPlayerPrioritizationSystem.TryGetResumeTime(double.NaN, 10f, out double _);

            // Assert
            Assert.That(hasResumeTime, Is.False);
        }

        [Test]
        public void GiveMaximumScoreToVideoWithScreenSpaceConsumer()
        {
            // Arrange
            Entity screenSpaceVideo = CreatePlayingVideo(renderer: null, screenSpaceConsumers: 1, out LivekitPlayer _);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Get<VideoStateByPriorityComponent>(screenSpaceVideo).Score, Is.EqualTo(float.MaxValue));
        }

        [Test]
        public void KeepScreenSpaceVideoPlayingOverVideoInFrontOfCamera()
        {
            // Arrange
            CreatePlayingVideo(renderer: null, screenSpaceConsumers: 1, out LivekitPlayer screenSpacePlayer);
            CreatePlayingVideo(CreateRendererAroundCamera(), screenSpaceConsumers: 0, out LivekitPlayer rendererPlayer);

            // Act
            system.Update(0);

            // Assert
            Assert.That(screenSpacePlayer.State, Is.EqualTo(PlayerState.Playing));
            Assert.That(rendererPlayer.State, Is.EqualTo(PlayerState.Paused));
        }

        [Test]
        public void CullVideoWithoutRenderersNorScreenSpaceConsumers()
        {
            // Arrange
            CreatePlayingVideo(renderer: null, screenSpaceConsumers: 0, out LivekitPlayer invisiblePlayer);
            CreatePlayingVideo(CreateRendererAroundCamera(), screenSpaceConsumers: 0, out LivekitPlayer rendererPlayer);

            // Act
            system.Update(0);

            // Assert
            Assert.That(invisiblePlayer.State, Is.EqualTo(PlayerState.Paused));
            Assert.That(rendererPlayer.State, Is.EqualTo(PlayerState.Playing));
        }

        private Entity CreatePlayingVideo(Renderer? renderer, int screenSpaceConsumers, out LivekitPlayer player)
        {
            player = new LivekitPlayer(Substitute.For<IRoom>(), static () => false, placeholderSource: null);
            player.Play();
            createdPlayers.Add(player);

            var consumer = new VideoTextureConsumer(videoTexturesPool);

            if (renderer != null)
                consumer.AddConsumer(renderer);

            for (var i = 0; i < screenSpaceConsumers; i++)
                consumer.AddScreenSpaceConsumer();

            var mediaPlayer = new MediaPlayerComponent { MediaPlayer = MultiMediaPlayer.FromLivekitPlayer(player) };

            return world.Create(new PBVideoPlayer(), mediaPlayer, consumer);
        }

        private Renderer CreateRendererAroundCamera()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = Vector3.zero;
            createdObjects.Add(cube);
            return cube.GetComponent<Renderer>();
        }
    }
}
