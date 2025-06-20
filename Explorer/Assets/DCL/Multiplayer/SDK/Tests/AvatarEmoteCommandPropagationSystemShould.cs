using Arch.Core;
using CommunicationData.URLHelpers;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Optimization.PerformanceBudgeting;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.SDK.Tests
{
    public class AvatarEmoteCommandPropagationSystemShould : UnitySystemTestBase<AvatarEmoteCommandPropagationSystem>
    {
        private readonly URN emoteUrn1 = new ("thunder-kiss-65");
        private readonly URN emoteUrn2 = new ("more-human-than-human");
        private readonly FakeEmoteStorage emoteStorage = new ();
        private Entity entity;
        private World sceneWorld;
        private PlayerCRDTEntity playerCRDTEntity;

        [SetUp]
        public void Setup()
        {
            sceneWorld = World.Create();
            Entity sceneWorldEntity = sceneWorld.Create();
            ISceneFacade sceneFacade = SceneFacadeUtils.CreateSceneFacadeSubstitute(Vector2Int.zero, sceneWorld);

            IEmote emote1 = Substitute.For<IEmote>();
            emote1.IsLooping().Returns(true);
            IEmote emote2 = Substitute.For<IEmote>();
            emote2.IsLooping().Returns(false);
            emoteStorage.emotes.Clear();
            emoteStorage.emotes.Add(emoteUrn1, emote1);
            emoteStorage.emotes.Add(emoteUrn2, emote2);

            system = new AvatarEmoteCommandPropagationSystem(world, emoteStorage);

            playerCRDTEntity = new PlayerCRDTEntity(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);

            playerCRDTEntity.AssignToScene(sceneFacade, sceneWorldEntity);

            entity = world.Create(playerCRDTEntity);
        }

        protected override void OnTearDown()
        {
            sceneWorld.Dispose();
            world.Dispose();
        }

        [Test]
        public void PropagateEmoteCommandsCorrectly()
        {
            Assert.IsFalse(world.Has<AvatarEmoteCommandComponent>(entity));
            Assert.IsFalse(sceneWorld.Has<AvatarEmoteCommandComponent>(playerCRDTEntity.SceneWorldEntity));

            // Add emote intent
            var emoteIntent = new CharacterEmoteIntent
                { EmoteId = emoteUrn1 };

            world.Add(entity, emoteIntent);

            system.Update(0);

            Assert.IsTrue(sceneWorld.TryGet(playerCRDTEntity.SceneWorldEntity, out AvatarEmoteCommandComponent sceneEmoteCommand));

            Assert.AreEqual(emoteIntent.EmoteId, sceneEmoteCommand.PlayingEmote);
            Assert.AreEqual(emoteStorage.emotes[emoteIntent.EmoteId].IsLooping(), sceneEmoteCommand.LoopingEmote);

            // Update emote intent with different emote
            emoteIntent.EmoteId = emoteUrn2;
            world.Set(entity, emoteIntent);
            system.Update(0);

            Assert.IsTrue(sceneWorld.TryGet(playerCRDTEntity.SceneWorldEntity, out sceneEmoteCommand));

            Assert.AreEqual(emoteIntent.EmoteId, sceneEmoteCommand.PlayingEmote);
            Assert.AreEqual(emoteStorage.emotes[emoteIntent.EmoteId].IsLooping(), sceneEmoteCommand.LoopingEmote);
        }

        [Test]
        public void StopPropagationWithoutPlayerCRDTEntity()
        {
            Assert.IsFalse(world.Has<AvatarEmoteCommandComponent>(entity));
            Assert.IsFalse(sceneWorld.Has<AvatarEmoteCommandComponent>(playerCRDTEntity.SceneWorldEntity));

            // Add emote intent
            var emoteIntent = new CharacterEmoteIntent
                { EmoteId = emoteUrn1 };

            world.Add(entity, emoteIntent);

            system.Update(0);

            Assert.IsTrue(sceneWorld.TryGet(playerCRDTEntity.SceneWorldEntity, out AvatarEmoteCommandComponent sceneEmoteCommand));

            Assert.AreEqual(emoteIntent.EmoteId, sceneEmoteCommand.PlayingEmote);
            Assert.AreEqual(emoteStorage.emotes[emoteIntent.EmoteId].IsLooping(), sceneEmoteCommand.LoopingEmote);

            // Update emote intent with different emote + remove PlayerCRDTEntity
            emoteIntent.EmoteId = emoteUrn2;
            world.Set(entity, emoteIntent);
            world.Remove<PlayerCRDTEntity>(entity);
            system.Update(0);

            Assert.IsTrue(sceneWorld.TryGet(playerCRDTEntity.SceneWorldEntity, out sceneEmoteCommand));

            Assert.AreNotEqual(emoteIntent.EmoteId, sceneEmoteCommand.PlayingEmote);
            Assert.AreNotEqual(emoteStorage.emotes[emoteIntent.EmoteId].IsLooping(), sceneEmoteCommand.LoopingEmote);
        }

        private class FakeEmoteStorage : IEmoteStorage
        {
            internal readonly Dictionary<URN, IEmote> emotes = new ();
            public List<URN> EmbededURNs { get; }

            public bool TryGetElement(URN urn, out IEmote element)
            {
                if (!emotes.TryGetValue(urn, out element)) return false;
                return true;
            }

            public void Set(URN urn, IEmote element)
            {
                throw new NotImplementedException();
            }

            public IEmote GetOrAddByDTO(EmoteDTO emoteDto, bool qualifiedForUnloading = true) =>
                throw new NotImplementedException();

            public void Unload(IPerformanceBudget frameTimeBudget)
            {
                throw new NotImplementedException();
            }

            public void SetOwnedNft(URN urn, NftBlockchainOperationEntry operation)
            {
                throw new NotImplementedException();
            }

            public bool TryGetOwnedNftRegistry(URN nftUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry) =>
                throw new NotImplementedException();

            public void ClearOwnedNftRegistry()
            {
                throw new NotImplementedException();
            }

            public void AddEmbeded(URN urn, IEmote emote)
            {
                throw new NotImplementedException();
            }
        }
    }
}
