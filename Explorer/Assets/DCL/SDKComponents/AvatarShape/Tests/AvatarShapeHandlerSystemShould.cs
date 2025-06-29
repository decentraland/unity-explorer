using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.AvatarShape.Components;
using ECS.Unity.AvatarShape.Systems;
using NUnit.Framework;
using DCL.Character.Components;
using DCL.Optimization.Pools;
using NSubstitute;
using SceneRunner.Scene;
using UnityEngine;
using DCL.AvatarRendering.Emotes;
using DCL.Ipfs;
using System;

namespace ECS.Unity.AvatarShape.Tests
{
    [TestFixture]
    public class AvatarShapeHandlerSystemShould : UnitySystemTestBase<AvatarShapeHandlerSystem>
    {
        [SetUp]
        public void SetUp()
        {
            globalWorld = World.Create();
            IComponentPool<Transform> pool = Substitute.For<IComponentPool<Transform>>();
            pool.Get().Returns(new GameObject().transform);
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneLoadingConcluded.Returns(true);
            system = new AvatarShapeHandlerSystem(world, globalWorld, pool, sceneData, false);

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
        }

        private Entity entity;
        private World globalWorld;

        [Test]
        public void ForwardSDKAvatarShapeInstantiationToGlobalWorldSystems()
        {
            Assert.AreEqual(0, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>()));

            var pbAvatarShapeComponent = new PBAvatarShape
                { Name = "Cthulhu" };

            world.Add(entity, pbAvatarShapeComponent);

            system.Update(0);

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<PBAvatarShape, SDKAvatarShapeComponent>()));
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>()));
            globalWorld.Query(new QueryDescription().WithAll<PBAvatarShape, CharacterTransform>(), (ref PBAvatarShape comp) => Assert.AreEqual(pbAvatarShapeComponent.Name, comp.Name));
        }

        [Test]
        public void ForwardSDKAvatarShapeUpdateToGlobalWorldSystems()
        {
            // Creation
            var pbAvatarShapeComponent = new PBAvatarShape
                { Name = "Cthulhu" };

            world.Add(entity, pbAvatarShapeComponent);

            system.Update(0);

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<PBAvatarShape, SDKAvatarShapeComponent>()));
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>()));

            // Update
            pbAvatarShapeComponent.Name = "Dagon";
            world.Set(entity, pbAvatarShapeComponent);

            system.Update(0);

            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>()));
            globalWorld.Query(new QueryDescription().WithAll<PBAvatarShape>(), (ref PBAvatarShape comp) => Assert.AreEqual(pbAvatarShapeComponent.Name, comp.Name));
        }

        [Test]
        public void RemoveEntityFromGlobalWorldOnComponentRemove()
        {
            // Create
            var pbAvatarShapeComponent = new PBAvatarShape
                { Name = "Cthulhu" };

            world.Add(entity, pbAvatarShapeComponent);

            system.Update(0);

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<PBAvatarShape, SDKAvatarShapeComponent>()));
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>().WithNone<DeleteEntityIntention>()));
            Assert.AreEqual(0, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape, DeleteEntityIntention>()));

            // Remove
            world.Remove<PBAvatarShape>(entity);

            system.Update(0);

            Assert.AreEqual(0, world.CountEntities(new QueryDescription().WithAll<PBAvatarShape, SDKAvatarShapeComponent>()));
            Assert.AreEqual(0, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>().WithNone<DeleteEntityIntention>()));
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape, DeleteEntityIntention>()));
        }

        [Test]
        public void RemoveEntityFromGlobalWorldOnSceneEntityDestruction()
        {
            // Create
            var pbAvatarShapeComponent = new PBAvatarShape
                { Name = "Cthulhu" };

            world.Add(entity, pbAvatarShapeComponent);

            system.Update(0);

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<PBAvatarShape, SDKAvatarShapeComponent>()));
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>().WithNone<DeleteEntityIntention>()));
            Assert.AreEqual(0, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape, DeleteEntityIntention>()));

            // Remove
            world.Add<DeleteEntityIntention>(entity);

            system.Update(0);

            Assert.AreEqual(0, world.CountEntities(new QueryDescription().WithAll<PBAvatarShape, SDKAvatarShapeComponent>()));
            Assert.AreEqual(0, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>().WithNone<DeleteEntityIntention>()));
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape, DeleteEntityIntention>()));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TriggerSceneEmote(bool isLocal)
        {
            // Arrange
            var pool = Substitute.For<IComponentPool<Transform>>();
            pool.Get().Returns(new GameObject().transform);

            var sceneData = Substitute.For<ISceneData>();
            sceneData.SceneLoadingConcluded.Returns(true);

            var sceneContent = Substitute.For<ISceneContent>();
            const string EMOTE_ID = "emote.glb";
            const string EMOTE_HASH = "someHash";

            sceneContent.TryGetHash(EMOTE_ID, out Arg.Any<string>())
                        .Returns(x =>
                         {
                             x[1] = EMOTE_HASH;
                             return true;
                         });

            sceneData.SceneContent.Returns(sceneContent);

            const string SCENE_ID = "aSceneId";
            sceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition { id = SCENE_ID });
            sceneData.AssetBundleManifest.Returns(new SceneAssetBundleManifest(URLDomain.EMPTY, "v1", Array.Empty<string>(), "hash", "date"));

            system = new AvatarShapeHandlerSystem(world, globalWorld, pool, sceneData, isLocal);

            var pbAvatarShapeComponent = new PBAvatarShape { ExpressionTriggerId = EMOTE_ID };
            world.Add(entity, pbAvatarShapeComponent);

            // Act
            system.Update(0);

            // Assert
            SDKAvatarShapeComponent sdkAvatarShape = world.Get<SDKAvatarShapeComponent>(entity);
            Entity globalEntity = sdkAvatarShape.globalWorldEntity;
            Assert.IsTrue(globalWorld.Has<SDKAvatarShapeEmotePromiseCancellationToken>(globalEntity));

            if (isLocal)
            {
                Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<GetSceneEmoteFromLocalSceneIntention>()));

                globalWorld.Query(new QueryDescription().WithAll<GetSceneEmoteFromLocalSceneIntention>(),
                    (ref GetSceneEmoteFromLocalSceneIntention intention) =>
                    {
                        Assert.AreEqual(EMOTE_ID, intention.EmotePath);
                        Assert.AreEqual(EMOTE_HASH, intention.EmoteHash);
                    });
            }
            else
            {
                Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<GetSceneEmoteFromRealmIntention>()));

                globalWorld.Query(new QueryDescription().WithAll<GetSceneEmoteFromRealmIntention>(),
                    (ref GetSceneEmoteFromRealmIntention intention) =>
                    {
                        Assert.AreEqual(SCENE_ID, intention.SceneId);
                        Assert.AreEqual(EMOTE_HASH, intention.EmoteHash);
                    });
            }
        }

        [Test]
        public void CancelPreviousPromiseWhenTriggeringNewEmote()
        {
            // Arrange
            var pool = Substitute.For<IComponentPool<Transform>>();
            pool.Get().Returns(new GameObject().transform);
            var sceneData = Substitute.For<ISceneData>();
            sceneData.SceneLoadingConcluded.Returns(true);
            var sceneContent = Substitute.For<ISceneContent>();

            const string EMOTE_ID_1 = "emote1.glb";
            const string EMOTE_HASH_1 = "hash1";
            sceneContent.TryGetHash(EMOTE_ID_1, out Arg.Any<string>()).Returns(x => { x[1] = EMOTE_HASH_1; return true; });

            const string EMOTE_ID_2 = "emote2.glb";
            const string EMOTE_HASH_2 = "hash2";
            sceneContent.TryGetHash(EMOTE_ID_2, out Arg.Any<string>()).Returns(x => { x[1] = EMOTE_HASH_2; return true; });

            sceneData.SceneContent.Returns(sceneContent);
            system = new AvatarShapeHandlerSystem(world, globalWorld, pool, sceneData, true);

            // Act 1: Trigger first emote
            var pbAvatarShapeComponent = new PBAvatarShape { ExpressionTriggerId = EMOTE_ID_1 };
            world.Add(entity, pbAvatarShapeComponent);
            system.Update(0);

            // Assert 1
            SDKAvatarShapeComponent sdkAvatarShape = world.Get<SDKAvatarShapeComponent>(entity);
            Entity globalEntity = sdkAvatarShape.globalWorldEntity;
            var promiseComponent1 = globalWorld.Get<SDKAvatarShapeEmotePromiseCancellationToken>(globalEntity);
            Assert.IsFalse(promiseComponent1.Cts.IsCancellationRequested);

            // Act 2: Trigger second emote
            pbAvatarShapeComponent.ExpressionTriggerId = EMOTE_ID_2;
            pbAvatarShapeComponent.IsDirty = true;
            world.Set(entity, pbAvatarShapeComponent);
            system.Update(0);

            // Assert 2
            var promiseComponent2 = globalWorld.Get<SDKAvatarShapeEmotePromiseCancellationToken>(globalEntity);
            Assert.IsTrue(promiseComponent1.Cts.IsCancellationRequested);
            Assert.AreNotSame(promiseComponent1, promiseComponent2);
            Assert.IsFalse(promiseComponent2.Cts.IsCancellationRequested);
        }
    }
}
