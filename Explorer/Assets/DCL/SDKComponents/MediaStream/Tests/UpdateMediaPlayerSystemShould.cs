using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class UpdateMediaPlayerSystemShould : UnitySystemTestBase<UpdateMediaPlayerSystem>
    {
        private MediaPlayer mediaPlayerGameObject;

        [SetUp]
        public void SetUp()
        {
            ISceneStateProvider sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            IPerformanceBudget budgetProvider = Substitute.For<IPerformanceBudget>();
            budgetProvider.TrySpendBudget().Returns(true);

            mediaPlayerGameObject = new GameObject().AddComponent<MediaPlayer>();

            system = new UpdateMediaPlayerSystem(world,
                Substitute.For<IWebRequestController>(),
                Substitute.For<ISceneData>(),
                sceneStateProvider,
                budgetProvider);
        }

        [TearDown]
        public void DisposeMediaPlayer()
        {
            UnityObjectUtils.SafeDestroy(mediaPlayerGameObject.gameObject);
        }

        [Test]
        [TestCase("http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4", 0.5f, true)]
        [TestCase("https://player.vimeo.com/external/552481870.m3u8?s=c312c8533f97e808fccc92b0510b085c8122a875", 1f, false)]
        [TestCase("https://player.vimeo.com/external/878776548.m3u8?s=e6e54ac3862fe71ac3ecbdb2abbfdd7ca7daafaf&logging=false", 0.1f, true)]
        public void OpenMediaWhenReachabilityPromiseIsResolved(string src, float volume, bool loop)
        {
            var pbVideoPlayer = new PBVideoPlayer
            {
                Src = src,
                Volume = volume,
                Playing = true,
                Loop = loop,
            };

            Entity entity = world.Create(pbVideoPlayer,
                new MediaPlayerComponent
                {
                    MediaPlayer = mediaPlayerGameObject,
                    URL = src,
                    Cts = new CancellationTokenSource(),
                    OpenMediaPromise = new OpenMediaPromise { url = src, status = OpenMediaPromise.Status.Resolved, isReachable = true },
                },
                PartitionComponent.TOP_PRIORITY); // Create entity

            AddTransformToEntity(entity);

            system.Update(0);

            Assert.That(mediaPlayerGameObject.Control, Is.Not.Null);
            Assert.That(mediaPlayerGameObject.TextureProducer, Is.Not.Null);
        }
    }
}
