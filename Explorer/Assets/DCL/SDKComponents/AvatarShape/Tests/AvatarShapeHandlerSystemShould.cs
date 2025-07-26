using Arch.Core;
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
using DCL.AvatarRendering.Loading.Components;
using ECS.StreamableLoading.Common.Components;
using CommunicationData.URLHelpers;
using DCL.Ipfs;
using System;
using DCL.Diagnostics;
using UnityEngine.TestTools;

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

        [Test]
        public void TriggersLocalSceneEmote()
        {
            // ARRANGE
            // System
            IComponentPool<Transform> pool = Substitute.For<IComponentPool<Transform>>();
            pool.Get().Returns(new GameObject().transform);
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneLoadingConcluded.Returns(true);
            var sceneContent = Substitute.For<ISceneContent>();
            const string emoteId = "emote.glb";
            const string hash = "emote_hash";

            sceneContent.TryGetHash(emoteId, out Arg.Any<string>())
                        .Returns(x =>
                         {
                             x[1] = hash;
                             return true;
                         });

            sceneData.SceneContent.Returns(sceneContent);
            system = new AvatarShapeHandlerSystem(world, globalWorld, pool, sceneData, true);

            // Avatar Shape
            var pbAvatarShapeComponent = new PBAvatarShape { Name = "Cthulhu", BodyShape = BodyShape.MALE.ToString() };
            world.Add(entity, pbAvatarShapeComponent);
            system.Update(0);

            var sdkAvatarShapeComponent = world.Get<SDKAvatarShapeComponent>(entity);
            Assert.IsFalse(sdkAvatarShapeComponent.LocalSceneEmotePromise.HasValue);

            // ACT
            // Update component to trigger emote
            var shape = world.Get<PBAvatarShape>(entity);
            shape.ExpressionTriggerId = emoteId;
            shape.IsDirty = true;
            world.Set(entity, shape);
            system.Update(0);

            // Simulate ResetDirtyFlagSystem effect
            shape.IsDirty = false;
            world.Set(entity, shape);

            // ASSERT
            // Promise is created
            sdkAvatarShapeComponent = world.Get<SDKAvatarShapeComponent>(entity);
            Assert.IsTrue(sdkAvatarShapeComponent.LocalSceneEmotePromise.HasValue);

            var promise = sdkAvatarShapeComponent.LocalSceneEmotePromise.Value;
            var promiseQuery = new QueryDescription().WithAll<GetSceneEmoteFromLocalSceneIntention>();
            Assert.AreEqual(1, globalWorld.CountEntities(promiseQuery));

            globalWorld.Query(promiseQuery, (ref GetSceneEmoteFromLocalSceneIntention intent) =>
            {
                Assert.AreEqual(emoteId, intent.EmotePath);
                Assert.AreEqual(hash, intent.EmoteHash);
                Assert.AreEqual(BodyShape.MALE, intent.BodyShape);
            });

            // ARRANGE 2
            // Resolve promise
            var emoteUrn = new URN("urn:decentraland:off-chain:scene-emote-1");
            var emote = Substitute.For<IEmote>();
            var dto = new EmoteDTO();
            dto.metadata = new EmoteDTO.EmoteMetadataDto();
            dto.metadata.id = emoteUrn;
            emote.DTO.Returns(dto);
            var emotes = RepoolableList<IEmote>.NewListWithContentOf(emote);
            var resolution = new EmotesResolution(emotes, BodyShape.MALE);
            var result = new StreamableLoadingResult<EmotesResolution>(resolution);
            globalWorld.Add(promise.Entity, result);

            // ACT 2
            system.Update(0);

            // ASSERT 2
            // Emote is triggered
            sdkAvatarShapeComponent = world.Get<SDKAvatarShapeComponent>(entity);
            Assert.IsFalse(sdkAvatarShapeComponent.LocalSceneEmotePromise.HasValue);

            var globalEntity = sdkAvatarShapeComponent.GlobalWorldEntity;
            Assert.IsTrue(globalWorld.Has<CharacterEmoteIntent>(globalEntity));

            var characterEmoteIntent = globalWorld.Get<CharacterEmoteIntent>(globalEntity);
            Assert.AreEqual(emoteUrn, characterEmoteIntent.EmoteId);
            Assert.IsTrue(characterEmoteIntent.Spatial);
            Assert.AreEqual(TriggerSource.SCENE, characterEmoteIntent.TriggerSource);
        }

        [Test]
        public void TriggersRealmSceneEmote()
        {
            // ARRANGE
            // System
            IComponentPool<Transform> pool = Substitute.For<IComponentPool<Transform>>();
            pool.Get().Returns(new GameObject().transform);
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneLoadingConcluded.Returns(true);
            var sceneContent = Substitute.For<ISceneContent>();
            const string emoteId = "emote.glb";
            const string hash = "emote_hash";

            sceneContent.TryGetHash(emoteId, out Arg.Any<string>())
                        .Returns(x =>
                         {
                             x[1] = hash;
                             return true;
                         });

            sceneData.SceneContent.Returns(sceneContent);
            sceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition { id = "sceneId" , AssetBundleManifest = new SceneAssetBundleManifest(URLDomain.EMPTY, "v1", Array.Empty<string>(), "hash", "date")});

            system = new AvatarShapeHandlerSystem(world, globalWorld, pool, sceneData, false);

            // Avatar Shape
            var pbAvatarShapeComponent = new PBAvatarShape { Name = "Cthulhu", BodyShape = BodyShape.MALE.ToString() };
            world.Add(entity, pbAvatarShapeComponent);
            system.Update(0);

            var sdkAvatarShapeComponent = world.Get<SDKAvatarShapeComponent>(entity);
            Assert.IsFalse(sdkAvatarShapeComponent.LocalSceneEmotePromise.HasValue);

            // ACT
            // Update component to trigger emote
            var shape = world.Get<PBAvatarShape>(entity);
            shape.ExpressionTriggerId = emoteId;
            shape.IsDirty = true;
            world.Set(entity, shape);
            system.Update(0);

            // Simulate ResetDirtyFlagSystem effect
            shape.IsDirty = false;
            world.Set(entity, shape);

            // ASSERT
            // Promise is created
            sdkAvatarShapeComponent = world.Get<SDKAvatarShapeComponent>(entity);
            Assert.IsTrue(sdkAvatarShapeComponent.RealmSceneEmotePromise.HasValue);

            var promise = sdkAvatarShapeComponent.RealmSceneEmotePromise.Value;
            var promiseQuery = new QueryDescription().WithAll<GetSceneEmoteFromRealmIntention>();
            Assert.AreEqual(1, globalWorld.CountEntities(promiseQuery));

            globalWorld.Query(promiseQuery, (ref GetSceneEmoteFromRealmIntention intent) =>
            {
                Assert.AreEqual(hash, intent.EmoteHash);
                Assert.AreEqual(BodyShape.MALE, intent.BodyShape);
            });

            // ARRANGE 2
            // Resolve promise
            var emoteUrn = new URN("urn:decentraland:off-chain:scene-emote-1");
            var emote = Substitute.For<IEmote>();
            var dto = new EmoteDTO();
            dto.metadata = new EmoteDTO.EmoteMetadataDto();
            dto.metadata.id = emoteUrn;
            emote.DTO.Returns(dto);
            var emotes = RepoolableList<IEmote>.NewListWithContentOf(emote);
            var resolution = new EmotesResolution(emotes, BodyShape.MALE);
            var result = new StreamableLoadingResult<EmotesResolution>(resolution);
            globalWorld.Add(promise.Entity, result);

            // ACT 2
            system.Update(0);

            // ASSERT 2
            // Emote is triggered
            sdkAvatarShapeComponent = world.Get<SDKAvatarShapeComponent>(entity);
            Assert.IsFalse(sdkAvatarShapeComponent.RealmSceneEmotePromise.HasValue);

            var globalEntity = sdkAvatarShapeComponent.GlobalWorldEntity;
            Assert.IsTrue(globalWorld.Has<CharacterEmoteIntent>(globalEntity));

            var characterEmoteIntent = globalWorld.Get<CharacterEmoteIntent>(globalEntity);
            Assert.AreEqual(emoteUrn, characterEmoteIntent.EmoteId);
            Assert.IsTrue(characterEmoteIntent.Spatial);
            Assert.AreEqual(TriggerSource.SCENE, characterEmoteIntent.TriggerSource);
        }

        [Test]
        public void NotTriggerSceneEmoteIfNotFound()
        {
            // ARRANGE
            // System
            IComponentPool<Transform> pool = Substitute.For<IComponentPool<Transform>>();
            pool.Get().Returns(new GameObject().transform);
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneLoadingConcluded.Returns(true);
            var sceneContent = Substitute.For<ISceneContent>();
            const string emoteId = "emote.glb";

            sceneContent.TryGetHash(emoteId, out Arg.Any<string>())
                        .Returns(false);

            sceneData.SceneContent.Returns(sceneContent);
            system = new AvatarShapeHandlerSystem(world, globalWorld, pool, sceneData, true);

            // Avatar Shape
            var pbAvatarShapeComponent = new PBAvatarShape { Name = "Cthulhu", BodyShape = BodyShape.MALE.ToString() };
            world.Add(entity, pbAvatarShapeComponent);
            system.Update(0);

            // ACT
            // Update component to trigger emote
            var shape = world.Get<PBAvatarShape>(entity);
            shape.ExpressionTriggerId = emoteId;
            world.Set(entity, shape);
            system.Update(0);

            // ASSERT
            // No promise is created
            var sdkAvatarShapeComponent = world.Get<SDKAvatarShapeComponent>(entity);
            Assert.IsFalse(sdkAvatarShapeComponent.LocalSceneEmotePromise.HasValue);
            Assert.IsFalse(sdkAvatarShapeComponent.RealmSceneEmotePromise.HasValue);
        }

        [Test]
        public void NotTriggerSceneEmoteIfPromiseFails()
        {
            // ARRANGE
            // System
            IComponentPool<Transform> pool = Substitute.For<IComponentPool<Transform>>();
            pool.Get().Returns(new GameObject().transform);
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneLoadingConcluded.Returns(true);
            var sceneContent = Substitute.For<ISceneContent>();
            const string emoteId = "emote.glb";
            const string hash = "emote_hash";

            sceneContent.TryGetHash(emoteId, out Arg.Any<string>())
                        .Returns(x =>
                         {
                             x[1] = hash;
                             return true;
                         });

            sceneData.SceneContent.Returns(sceneContent);
            system = new AvatarShapeHandlerSystem(world, globalWorld, pool, sceneData, true);

            // Avatar Shape
            var pbAvatarShapeComponent = new PBAvatarShape { Name = "Cthulhu", BodyShape = BodyShape.MALE.ToString(), ExpressionTriggerId = emoteId };
            world.Add(entity, pbAvatarShapeComponent);
            system.Update(0);

            // ACT & ARRANGE 2
            // Resolve promise with failure
            var sdkAvatarShapeComponent = world.Get<SDKAvatarShapeComponent>(entity);
            Assert.IsTrue(sdkAvatarShapeComponent.LocalSceneEmotePromise.HasValue);
            var promise = sdkAvatarShapeComponent.LocalSceneEmotePromise.Value;
            LogAssert.Expect(LogType.Exception, "Exception: Emote loading failed");
            var result = new StreamableLoadingResult<EmotesResolution>(ReportData.UNSPECIFIED, new Exception("Emote loading failed"));
            globalWorld.Add(promise.Entity, result);

            system.Update(0);

            // ASSERT
            // Emote is not triggered
            sdkAvatarShapeComponent = world.Get<SDKAvatarShapeComponent>(entity);
            Assert.IsFalse(sdkAvatarShapeComponent.LocalSceneEmotePromise.HasValue);

            var globalEntity = sdkAvatarShapeComponent.GlobalWorldEntity;
            Assert.IsFalse(globalWorld.Has<CharacterEmoteIntent>(globalEntity));
        }

        [Test]
        public void InterruptPreviouslyLoadingEmote()
        {
            // ARRANGE
            // System
            IComponentPool<Transform> pool = Substitute.For<IComponentPool<Transform>>();
            pool.Get().Returns(new GameObject().transform);
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneLoadingConcluded.Returns(true);
            var sceneContent = Substitute.For<ISceneContent>();
            const string firstEmoteId = "emote1.glb";
            const string firstHash = "emote1_hash";
            const string secondEmoteId = "emote2.glb";
            const string secondHash = "emote2_hash";

            sceneContent.TryGetHash(firstEmoteId, out Arg.Any<string>())
                        .Returns(x =>
                         {
                             x[1] = firstHash;
                             return true;
                         });

            sceneContent.TryGetHash(secondEmoteId, out Arg.Any<string>())
                        .Returns(x =>
                         {
                             x[1] = secondHash;
                             return true;
                         });

            sceneData.SceneContent.Returns(sceneContent);
            system = new AvatarShapeHandlerSystem(world, globalWorld, pool, sceneData, true);

            // Avatar Shape
            var pbAvatarShapeComponent = new PBAvatarShape { Name = "Cthulhu", BodyShape = BodyShape.MALE.ToString() };
            world.Add(entity, pbAvatarShapeComponent);
            system.Update(0);

            // ACT 1
            // Trigger first emote
            var shape = world.Get<PBAvatarShape>(entity);
            shape.ExpressionTriggerId = firstEmoteId;
            shape.IsDirty = true;
            world.Set(entity, shape);
            system.Update(0);

            // Simulate ResetDirtyFlagSystem effect
            shape.IsDirty = false;
            world.Set(entity, shape);

            // ASSERT 1
            // First promise is created
            var sdkAvatarShapeComponent = world.Get<SDKAvatarShapeComponent>(entity);
            Assert.IsTrue(sdkAvatarShapeComponent.LocalSceneEmotePromise.HasValue);
            var firstPromise = sdkAvatarShapeComponent.LocalSceneEmotePromise.Value;
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<GetSceneEmoteFromLocalSceneIntention>()));

            // ACT 2
            // Interrupt with second emote before first resolves
            shape = world.Get<PBAvatarShape>(entity);
            shape.ExpressionTriggerId = secondEmoteId;
            shape.IsDirty = true;
            world.Set(entity, shape);
            system.Update(0);

            // Simulate ResetDirtyFlagSystem effect
            shape.IsDirty = false;
            world.Set(entity, shape);

            // ASSERT 2
            // First promise is replaced with second promise
            sdkAvatarShapeComponent = world.Get<SDKAvatarShapeComponent>(entity);
            Assert.IsTrue(sdkAvatarShapeComponent.LocalSceneEmotePromise.HasValue);
            var secondPromise = sdkAvatarShapeComponent.LocalSceneEmotePromise.Value;
            Assert.AreNotEqual(firstPromise.Entity, secondPromise.Entity);
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<GetSceneEmoteFromLocalSceneIntention>()));

            // Verify second promise has correct parameters
            globalWorld.Query(new QueryDescription().WithAll<GetSceneEmoteFromLocalSceneIntention>(), (Entity entity, ref GetSceneEmoteFromLocalSceneIntention intent) =>
            {
                if (entity == secondPromise.Entity)
                {
                    Assert.AreEqual(secondEmoteId, intent.EmotePath);
                    Assert.AreEqual(secondHash, intent.EmoteHash);
                    Assert.AreEqual(BodyShape.MALE, intent.BodyShape);
                }
            });

            // ACT 3
            // Resolve only the second promise
            var emoteUrn = new URN("urn:decentraland:off-chain:scene-emote-2");
            var emote = Substitute.For<IEmote>();
            var dto = new EmoteDTO();
            dto.metadata = new EmoteDTO.EmoteMetadataDto();
            dto.metadata.id = emoteUrn;
            emote.DTO.Returns(dto);
            var emotes = RepoolableList<IEmote>.NewListWithContentOf(emote);
            var resolution = new EmotesResolution(emotes, BodyShape.MALE);
            var result = new StreamableLoadingResult<EmotesResolution>(resolution);
            globalWorld.Add(secondPromise.Entity, result);

            system.Update(0);

            // ASSERT 3
            // Only the second emote is triggered
            sdkAvatarShapeComponent = world.Get<SDKAvatarShapeComponent>(entity);
            Assert.IsFalse(sdkAvatarShapeComponent.LocalSceneEmotePromise.HasValue);

            var globalEntity = sdkAvatarShapeComponent.GlobalWorldEntity;
            Assert.IsTrue(globalWorld.Has<CharacterEmoteIntent>(globalEntity));

            var characterEmoteIntent = globalWorld.Get<CharacterEmoteIntent>(globalEntity);
            Assert.AreEqual(emoteUrn, characterEmoteIntent.EmoteId);
            Assert.IsTrue(characterEmoteIntent.Spatial);
            Assert.AreEqual(TriggerSource.SCENE, characterEmoteIntent.TriggerSource);
        }
    }
}
