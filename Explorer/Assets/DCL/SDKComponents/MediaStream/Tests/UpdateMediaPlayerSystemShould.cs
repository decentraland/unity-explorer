using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class UpdateMediaPlayerSystemShould : UnitySystemTestBase<UpdateMediaPlayerSystem>
    {
        private Entity entity;
        private MediaPlayerComponent mediaPlayerComponent;

        private MediaPlayer mediaPlayerGameObject;

        [SetUp]
        public void SetUp()
        {
            // System
            system = CreateSystem(world);

            // Components
            var pbAudioStream = new PBAudioStream { Url = "http://ice3.somafm.com/dronezone-128-mp3"};
            mediaPlayerGameObject = new GameObject().AddComponent<MediaPlayer>();
            var mediaPlayerComponent = new MediaPlayerComponent { MediaPlayer = mediaPlayerGameObject };

            // Entity
            entity = world.Create(pbAudioStream, mediaPlayerComponent, PartitionComponent.TOP_PRIORITY); // Create entity
            AddTransformToEntity(entity);
            return;

            UpdateMediaPlayerSystem CreateSystem(World world)
            {
                ISceneStateProvider sceneStateProvider = Substitute.For<ISceneStateProvider>();
                sceneStateProvider.IsCurrent.Returns(true);

                IPerformanceBudget budgetProvider = Substitute.For<IPerformanceBudget>();
                budgetProvider.TrySpendBudget().Returns(true);

                return new UpdateMediaPlayerSystem(world, sceneStateProvider, budgetProvider);
            }
        }

        // [Test]
        public void UpdateMediaStream_WhenPBAudioSourceIsDirty()
        {
            // Arrange

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(world.TryGet<MediaPlayerComponent>(entity, out _));
        }
    }
}
