using Arch.Core;
using CRDT;
using DCL.SDKComponents.MediaStream;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.Textures.Components;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility.Primitives;

namespace ECS.Unity.Materials.Tests
{
    public class CleanUpMaterialsSystemShould : UnitySystemTestBase<CleanUpMaterialsSystem>
    {
        private const int VIDEO_PLAYER_CRDT_ENTITY = 512;

        private DestroyMaterial destroyMaterial = null!;

        private Entity e;
        private Entity texPromise;

        [SetUp]
        public void SetUp()
        {
            system = new CleanUpMaterialsSystem(world, destroyMaterial = Substitute.For<DestroyMaterial>());

            e = world.Create(new MaterialComponent(new MaterialData()) { AlbedoTexPromise = AssetPromise<TextureData, GetTextureIntention>.Create(world, new GetTextureIntention { CommonArguments = new CommonLoadingArguments("url") }, PartitionComponent.TOP_PRIORITY) }, new DeleteEntityIntention());
        }

        [Test]
        public void AbortLoadingIntentions()
        {
            ref MaterialComponent component = ref world.Get<MaterialComponent>(e);
            component.Status = StreamableLoading.LifeCycle.LoadingInProgress;

            CancellationToken ct = component.AlbedoTexPromise.Value.LoadingIntention.CommonArguments.CancellationToken;

            system.Update(0);

            Assert.That(ct.IsCancellationRequested, Is.True);
            Assert.That(world.Get<MaterialComponent>(e).AlbedoTexPromise, Is.EqualTo(null));
        }

        [Test]
        public void StopTrackingRendererOfVideoConsumer()
        {
            // Arrange
            var videoTexture = new RenderTexture(1, 1, 0);
            IObjectPool<RenderTexture> videoTexturesPool = Substitute.For<IObjectPool<RenderTexture>>();
            videoTexturesPool.Get().Returns(videoTexture);
            var consumer = new VideoTextureConsumer(videoTexturesPool);

            var rendererGameObject = new GameObject(nameof(StopTrackingRendererOfVideoConsumer));
            MeshRenderer meshRenderer = rendererGameObject.AddComponent<MeshRenderer>();
            consumer.AddConsumer(meshRenderer);

            var textureData = new TextureData(AnyTexture.FromVideoTextureData(new VideoTextureData(consumer, default(MediaPlayerComponent))));
            textureData.AddReference();

            Entity videoConsumerEntity = world.Create(
                new MaterialComponent(new MaterialData())
                {
                    Status = StreamableLoading.LifeCycle.LoadingFinished,
                    Result = DefaultMaterial.New(),
                    AlbedoTexPromise = AssetPromise<TextureData, GetTextureIntention>.CreateFinalized(
                        new GetTextureIntention(new CRDTEntity(VIDEO_PLAYER_CRDT_ENTITY)),
                        new StreamableLoadingResult<TextureData>(textureData)),
                },
                new PrimitiveMeshRendererComponent { MeshRenderer = meshRenderer },
                new DeleteEntityIntention());

            // Act
            system.Update(0);

            // Assert
            (Vector3 boundsMin, Vector3 _) = consumer.GetBounds();
            Assert.That(boundsMin, Is.EqualTo(Vector3.one * float.MaxValue), "The released renderer must no longer contribute to the video bounds");
            Assert.That(world.Get<MaterialComponent>(videoConsumerEntity).AlbedoTexPromise, Is.EqualTo(null));

            Object.DestroyImmediate(rendererGameObject);
            Object.DestroyImmediate(videoTexture);
        }

        [Test]
        public void Dereference()
        {
            ref MaterialComponent component = ref world.Get<MaterialComponent>(e);
            component.Status = StreamableLoading.LifeCycle.LoadingFinished;
            component.Result = DefaultMaterial.New();

            system.Update(0);

            destroyMaterial.Received(1)(in component.Data, component.Result);
        }
    }
}
