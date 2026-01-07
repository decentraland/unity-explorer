using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Profiles;
using DCL.SDKComponents.InputModifier.Components;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.AvatarRendering.Emotes.Tests
{
    [TestFixture]
    public class UpdateEmoteInputSystemShould
    {
        private World world;
        private UpdateEmoteInputSystem system;
        private MockEmotesMessageBus mockMessageBus;
        private EmotesBus emotesBus;
        private GameObject testGameObject;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();

            mockMessageBus = new MockEmotesMessageBus();
            emotesBus = new EmotesBus();

            var builder = new ArchSystemsWorldBuilder<World>(world);
            system = UpdateEmoteInputSystem.InjectToWorld(ref builder, mockMessageBus, emotesBus);
            system.Initialize();

            testGameObject = new GameObject("TestPlayer");
        }

        [TearDown]
        public void TearDown()
        {
            system?.Dispose();
            world?.Dispose();

            if (testGameObject != null)
                UnityEngine.Object.DestroyImmediate(testGameObject);
        }

        private Entity CreatePlayerEntity(Profile profile, bool isVisible = true, bool disableEmote = false)
        {
            var avatarShape = new AvatarShapeComponent("TestPlayer", "test-id")
            {
                IsVisible = isVisible,
            };

            var inputModifier = new InputModifierComponent { DisableEmote = disableEmote };
            var entity = world.Create(
                new PlayerComponent(testGameObject.transform),
                profile,
                avatarShape,
                inputModifier
            );

            return entity;
        }

        private Profile CreateProfileWithEmotes(params string[] emoteUrns)
        {
            var profile = Profile.Create();
            var avatar = new Avatar();

            for (int i = 0; i < emoteUrns.Length && i < Avatar.MAX_EQUIPPED_EMOTES; i++)
                avatar.emotes[i] = new URN(emoteUrns[i]);

            profile.Avatar = avatar;
            return profile;
        }

        [Test]
        public void TriggerEmoteBySlotIntent()
        {
            // Arrange
            var profile = CreateProfileWithEmotes("urn:decentraland:off-chain:base-avatars:wave");
            var entity = CreatePlayerEntity(profile);
            world.Add(entity, new TriggerEmoteBySlotIntent { Slot = 0 });

            // Act
            system.Update(0);

            // Assert
            Assert.IsTrue(world.Has<CharacterEmoteIntent>(entity));
            var intent = world.Get<CharacterEmoteIntent>(entity);
            Assert.AreEqual("urn:decentraland:off-chain:base-avatars:wave", intent.EmoteId.ToString());
            Assert.AreEqual(TriggerSource.SELF, intent.TriggerSource);
            Assert.IsTrue(intent.Spatial);
            Assert.IsFalse(world.Has<TriggerEmoteBySlotIntent>(entity), "TriggerEmoteBySlotIntent should be removed after processing");
        }

        [Test]
        public void SendEmoteToMessageBusWhenTriggered()
        {
            // Arrange
            var profile = CreateProfileWithEmotes("urn:decentraland:off-chain:base-avatars:clap");
            var entity = CreatePlayerEntity(profile);
            world.Add(entity, new TriggerEmoteBySlotIntent { Slot = 0 });

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(1, mockMessageBus.SentEmotes.Count);
            Assert.AreEqual("urn:decentraland:off-chain:base-avatars:clap", mockMessageBus.SentEmotes[0].emoteId.ToString());
            Assert.IsFalse(mockMessageBus.SentEmotes[0].loopCyclePassed);
        }

        [Test]
        public void NotTriggerEmoteWhenAvatarIsNotVisible()
        {
            // Arrange
            var profile = CreateProfileWithEmotes("urn:decentraland:off-chain:base-avatars:dance");
            var entity = CreatePlayerEntity(profile, isVisible: false);
            world.Add(entity, new TriggerEmoteBySlotIntent { Slot = 0 });

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Has<CharacterEmoteIntent>(entity));
            Assert.AreEqual(0, mockMessageBus.SentEmotes.Count);
        }

        [Test]
        public void NotTriggerEmoteWhenInputModifierDisablesEmote()
        {
            // Arrange
            var profile = CreateProfileWithEmotes("urn:decentraland:off-chain:base-avatars:fist_pump");
            var entity = CreatePlayerEntity(profile, isVisible: true, disableEmote: true);
            world.Add(entity, new TriggerEmoteBySlotIntent { Slot = 0 });

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Has<CharacterEmoteIntent>(entity));
            Assert.AreEqual(0, mockMessageBus.SentEmotes.Count);
        }

        [Test]
        public void TriggerCorrectEmoteFromMultipleSlots()
        {
            // Arrange
            var profile = CreateProfileWithEmotes(
                "urn:decentraland:off-chain:base-avatars:wave",
                "urn:decentraland:off-chain:base-avatars:clap",
                "urn:decentraland:off-chain:base-avatars:dance"
            );
            var entity = CreatePlayerEntity(profile);
            world.Add(entity, new TriggerEmoteBySlotIntent { Slot = 2 });

            // Act
            system.Update(0);

            // Assert
            Assert.IsTrue(world.Has<CharacterEmoteIntent>(entity));
            var intent = world.Get<CharacterEmoteIntent>(entity);
            Assert.AreEqual("urn:decentraland:off-chain:base-avatars:dance", intent.EmoteId.ToString());
        }

        [Test]
        public void NotTriggerEmoteWhenEntityAlreadyHasCharacterEmoteIntent()
        {
            // Arrange
            var profile = CreateProfileWithEmotes("urn:decentraland:off-chain:base-avatars:wave");
            var entity = CreatePlayerEntity(profile);

            // Add existing emote intent
            world.Add(entity, new CharacterEmoteIntent { EmoteId = new URN("existing-emote") });
            world.Add(entity, new TriggerEmoteBySlotIntent { Slot = 0 });

            // Act
            system.Update(0);

            // Assert - should still have the original emote intent
            var intent = world.Get<CharacterEmoteIntent>(entity);
            Assert.AreEqual("existing-emote", intent.EmoteId.ToString());
            Assert.AreEqual(0, mockMessageBus.SentEmotes.Count);
        }

        [Test]
        public void HandleMultiplePlayersCorrectly()
        {
            // Arrange
            var profile1 = CreateProfileWithEmotes("urn:decentraland:off-chain:base-avatars:wave");
            var profile2 = CreateProfileWithEmotes("urn:decentraland:off-chain:base-avatars:clap");

            var testGo2 = new GameObject("TestPlayer2");

            try
            {
                var avatarShape1 = new AvatarShapeComponent("TestPlayer1", "test-id1") { IsVisible = true };
                var avatarShape2 = new AvatarShapeComponent("TestPlayer2", "test-id2") { IsVisible = true };

                var entity1 = world.Create(
                    new PlayerComponent(testGameObject.transform),
                    profile1,
                    avatarShape1,
                    new InputModifierComponent()
                );

                var entity2 = world.Create(
                    new PlayerComponent(testGo2.transform),
                    profile2,
                    avatarShape2,
                    new InputModifierComponent()
                );

                world.Add(entity1, new TriggerEmoteBySlotIntent { Slot = 0 });
                world.Add(entity2, new TriggerEmoteBySlotIntent { Slot = 0 });

                // Act
                system.Update(0);

                // Assert - both should have triggered
                Assert.IsTrue(world.Has<CharacterEmoteIntent>(entity1));
                Assert.IsTrue(world.Has<CharacterEmoteIntent>(entity2));
                Assert.AreEqual(2, mockMessageBus.SentEmotes.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGo2);
            }
        }

        [Test]
        public void ProcessTriggerEmoteBySlotIntentBeforeTriggerEmoteQuery()
        {
            // Arrange - This tests that intent is processed in the same frame
            var profile = CreateProfileWithEmotes("urn:decentraland:off-chain:base-avatars:wave");
            var entity = CreatePlayerEntity(profile);
            world.Add(entity, new TriggerEmoteBySlotIntent { Slot = 0 });

            // Act
            system.Update(0);

            // Assert - The intent should be removed and emote should be triggered
            Assert.IsFalse(world.Has<TriggerEmoteBySlotIntent>(entity));
            Assert.IsTrue(world.Has<CharacterEmoteIntent>(entity));
        }

        [Test]
        public void NotFireQuickActionEmotePlayedEventOnTriggerEmoteBySlotIntent()
        {
            // Arrange
            var profile = CreateProfileWithEmotes("urn:decentraland:off-chain:base-avatars:wave");
            var entity = CreatePlayerEntity(profile);
            world.Add(entity, new TriggerEmoteBySlotIntent { Slot = 0 });

            bool eventFired = false;
            emotesBus.QuickActionEmotePlayed += () => eventFired = true;

            // Act
            system.Update(0);

            // Assert - TriggerEmoteBySlotIntent does not fire the QuickActionEmotePlayed event
            Assert.IsFalse(eventFired, "QuickActionEmotePlayed event should not be fired for TriggerEmoteBySlotIntent");
            Assert.IsTrue(world.Has<CharacterEmoteIntent>(entity), "Emote should still be triggered");
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 4)]
        [TestCase(5, 5)]
        [TestCase(6, 6)]
        [TestCase(7, 7)]
        [TestCase(8, 8)]
        [TestCase(9, 9)]
        public void MapEmoteSlotToCorrectIndex(int slot, int expectedIndex)
        {
            // Arrange
            var emoteUrns = new string[Avatar.MAX_EQUIPPED_EMOTES];
            for (int i = 0; i < emoteUrns.Length; i++)
                emoteUrns[i] = $"urn:test:emote{i}";

            var profile = CreateProfileWithEmotes(emoteUrns);
            var entity = CreatePlayerEntity(profile);
            world.Add(entity, new TriggerEmoteBySlotIntent { Slot = slot });

            // Act
            system.Update(0);

            // Assert
            Assert.IsTrue(world.Has<CharacterEmoteIntent>(entity));
            var intent = world.Get<CharacterEmoteIntent>(entity);
            Assert.AreEqual($"urn:test:emote{expectedIndex}", intent.EmoteId.ToString());
        }

        // Mock implementation of IEmotesMessageBus
        private class MockEmotesMessageBus : IEmotesMessageBus
        {
            public List<(URN emoteId, bool loopCyclePassed)> SentEmotes = new ();

            public void Send(URN urn, bool loopCyclePassed)
            {
                SentEmotes.Add((urn, loopCyclePassed));
            }

            public OwnedBunch<RemoteEmoteIntention> EmoteIntentions() => throw new NotImplementedException();
            public void OnPlayerRemoved(string walletId) => throw new NotImplementedException();
            public void SaveForRetry(RemoteEmoteIntention intention) => throw new NotImplementedException();
        }
    }
}

