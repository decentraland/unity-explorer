using Arch.Core;
using CRDT;
using DCL.Audio;
using DCL.AvProSwitch;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Prefs;
using DCL.WebRequests;
using ECS.StreamableLoading.Textures;
using ECS.Unity.AssetLoad.Cache;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.Textures.Components;
using LiveKit.Rooms;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class MediaFactoryShould
    {
        private const string POOL_CONTAINER_NAME = "POOL_CONTAINER_MEDIA_PLAYER";

        private static readonly CRDTEntity VIDEO_PLAYER_CRDT_ENTITY = new (512);

        // MediaVolume reads DCLPlayerPrefs on construction, whose backing store is only populated by a
        // [RuntimeInitializeOnLoadMethod]; EditMode tests swap in an in-memory store for their duration instead.
        private static readonly FieldInfo? DCL_PREFS_BACKING_FIELD =
            typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static);

        private readonly List<Object> createdObjects = new ();

        private IDCLPrefs? originalPrefs;
        private World world = null!;
        private Dictionary<CRDTEntity, Entity> entitiesMap = null!;
        private IObjectPool<RenderTexture> videoTexturesPool = null!;
        private MediaFactory factory = null!;

        [SetUp]
        public void SetUp()
        {
            Assert.That(DCL_PREFS_BACKING_FIELD, Is.Not.Null, "DCLPlayerPrefs no longer has a 'dclPrefs' backing field, update this test's reflection target");
            originalPrefs = (IDCLPrefs?)DCL_PREFS_BACKING_FIELD!.GetValue(null);
            DCL_PREFS_BACKING_FIELD.SetValue(null, new InMemoryDCLPlayerPrefs());

            world = World.Create();
            entitiesMap = new Dictionary<CRDTEntity, Entity>();

            videoTexturesPool = Substitute.For<IObjectPool<RenderTexture>>();

            videoTexturesPool.Get().Returns(_ =>
            {
                var renderTexture = new RenderTexture(2, 2, 0);
                createdObjects.Add(renderTexture);
                return renderTexture;
            });

            var mediaPlayerPrefab = new GameObject("MediaPlayerPrefab").AddComponent<MediaPlayer>();
            createdObjects.Add(mediaPlayerPrefab.gameObject);

            factory = new MediaFactory(
                Substitute.For<ISceneData>(),
                Substitute.For<IRoom>(),
                static () => false,
                new MediaPlayerCustomPool(mediaPlayerPrefab),
                Substitute.For<ISceneStateProvider>(),
                new MediaVolume(new VolumeBus()),
                videoTexturesPool,
                entitiesMap,
                world,
                Substitute.For<IWebRequestController>(),
                Substitute.For<IPerformanceBudget>(),
                new AssetPreLoadCache(Substitute.For<IGltfContainerAssetsCache>()),
                Substitute.For<IAnalyticsController>(),
                placeholderSource: null);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();

            GameObject? poolContainer = GameObject.Find(POOL_CONTAINER_NAME);

            if (poolContainer != null)
                createdObjects.Add(poolContainer);

            foreach (Object createdObject in createdObjects)
                UnityObjectUtils.SafeDestroy(createdObject);

            createdObjects.Clear();

            DCL_PREFS_BACKING_FIELD!.SetValue(null, originalPrefs);
        }

        [Test]
        public void NotAddScreenSpaceConsumerWhileVideoPlayerEntityIsUnknown()
        {
            // Act
            bool added = factory.TryAddScreenSpaceConsumer(VIDEO_PLAYER_CRDT_ENTITY, out TextureData? textureData);

            // Assert
            Assert.That(added, Is.False);
            Assert.That(textureData, Is.Null);
        }

        [Test]
        public void NotAddScreenSpaceConsumerWhileVideoPlayerIsNotCreated()
        {
            // Arrange
            Entity videoPlayerEntity = world.Create(new VideoTextureConsumer(videoTexturesPool));
            entitiesMap[VIDEO_PLAYER_CRDT_ENTITY] = videoPlayerEntity;

            // Act
            bool added = factory.TryAddScreenSpaceConsumer(VIDEO_PLAYER_CRDT_ENTITY, out TextureData? textureData);

            // Assert
            Assert.That(added, Is.False);
            Assert.That(textureData, Is.Null);
            Assert.That(world.Has<TextureData>(videoPlayerEntity), Is.False);
            Assert.That(world.Get<VideoTextureConsumer>(videoPlayerEntity).ScreenSpaceConsumers, Is.Zero);
        }

        [Test]
        public void AddScreenSpaceConsumerAndReferenceTextureData()
        {
            // Arrange
            Entity videoPlayerEntity = CreateReadyVideoPlayer();

            // Act
            bool added = factory.TryAddScreenSpaceConsumer(VIDEO_PLAYER_CRDT_ENTITY, out TextureData? textureData);

            // Assert
            Assert.That(added, Is.True);
            Assert.That(textureData, Is.Not.Null);
            Assert.That(textureData!.referenceCount, Is.EqualTo(1));
            Assert.That(world.Get<TextureData>(videoPlayerEntity), Is.SameAs(textureData));
            Assert.That(textureData.Asset.Texture, Is.SameAs(world.Get<VideoTextureConsumer>(videoPlayerEntity).Texture));
            Assert.That(world.Get<VideoTextureConsumer>(videoPlayerEntity).ScreenSpaceConsumers, Is.EqualTo(1));
        }

        [Test]
        public void ShareTextureDataBetweenConsumers()
        {
            // Arrange
            Entity videoPlayerEntity = CreateReadyVideoPlayer();
            Entity rendererConsumerEntity = world.Create();

            // Act
            factory.TryAddConsumer(rendererConsumerEntity, VIDEO_PLAYER_CRDT_ENTITY, out TextureData? rendererTextureData);
            factory.TryAddScreenSpaceConsumer(VIDEO_PLAYER_CRDT_ENTITY, out TextureData? firstScreenSpaceTextureData);
            factory.TryAddScreenSpaceConsumer(VIDEO_PLAYER_CRDT_ENTITY, out TextureData? secondScreenSpaceTextureData);

            // Assert
            Assert.That(firstScreenSpaceTextureData, Is.SameAs(rendererTextureData));
            Assert.That(secondScreenSpaceTextureData, Is.SameAs(rendererTextureData));
            Assert.That(rendererTextureData!.referenceCount, Is.EqualTo(3));
            Assert.That(world.Get<VideoTextureConsumer>(videoPlayerEntity).ScreenSpaceConsumers, Is.EqualTo(2));
        }

        [Test]
        public void RemoveScreenSpaceConsumerWithoutDereferencingTextureData()
        {
            // Arrange
            Entity videoPlayerEntity = CreateReadyVideoPlayer();
            factory.TryAddScreenSpaceConsumer(VIDEO_PLAYER_CRDT_ENTITY, out TextureData? textureData);

            // Act
            factory.RemoveScreenSpaceConsumer(VIDEO_PLAYER_CRDT_ENTITY);

            // Assert
            Assert.That(world.Get<VideoTextureConsumer>(videoPlayerEntity).ScreenSpaceConsumers, Is.Zero);
            Assert.That(textureData!.referenceCount, Is.EqualTo(1));
        }

        [Test]
        public void IgnoreScreenSpaceConsumerRemovalWhenVideoPlayerIsGone()
        {
            // Arrange
            Entity videoPlayerEntity = CreateReadyVideoPlayer();
            factory.TryAddScreenSpaceConsumer(VIDEO_PLAYER_CRDT_ENTITY, out TextureData? _);
            world.Destroy(videoPlayerEntity);

            // Act & Assert
            Assert.DoesNotThrow(() => factory.RemoveScreenSpaceConsumer(VIDEO_PLAYER_CRDT_ENTITY));
            Assert.DoesNotThrow(() => factory.RemoveScreenSpaceConsumer(new CRDTEntity(1024)));
        }

        private Entity CreateReadyVideoPlayer()
        {
            Entity videoPlayerEntity = world.Create(new VideoTextureConsumer(videoTexturesPool), new MediaPlayerComponent());
            entitiesMap[VIDEO_PLAYER_CRDT_ENTITY] = videoPlayerEntity;
            return videoPlayerEntity;
        }
    }
}
