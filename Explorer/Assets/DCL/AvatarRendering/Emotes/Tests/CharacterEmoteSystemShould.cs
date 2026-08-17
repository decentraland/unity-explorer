using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Play;
using DCL.ECSComponents;
using DCL.Character.Components;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using ECS.SceneLifeCycle;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using UnityEngine;
using Utility.Animations;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class CharacterEmoteSystemShould : UnitySystemTestBase<CharacterEmoteSystem>
    {
        private const string SMART_WEARABLE_ENTITY_ID = "bafkreiwearable";
        private const string EMOTE_HASH = "bafkreiemotehash";

        private ScenesCache scenesCache = null!;
        private IEmotesMessageBus messageBus = null!;
        private IAvatarView avatarView = null!;
        private GameObject poolRoot = null!;
        private GameObject audioSourcePrefab = null!;
        private EmoteReferences emoteReferences = null!;
        private Entity playerEntity;

        [SetUp]
        public void Setup()
        {
            poolRoot = new GameObject("ROOT_POOL_CONTAINER");
            audioSourcePrefab = new GameObject("EmoteAudioSource");
            AudioSource audioSource = audioSourcePrefab.AddComponent<AudioSource>();
            var emotePlayer = new EmotePlayer(audioSource, ScriptableObject.CreateInstance<EmoteMaskCatalog>(), legacyAnimationsEnabled: true);

            scenesCache = new ScenesCache();
            messageBus = Substitute.For<IEmotesMessageBus>();

            system = new CharacterEmoteSystem(world, Substitute.For<IEmoteStorage>(), messageBus, emotePlayer,
                Substitute.For<IDebugContainerBuilder>(), localSceneDevelopment: false, scenesCache);

            avatarView = Substitute.For<IAvatarView>();
            avatarView.IsLegacyAnimationPlaying.Returns(true);

            emoteReferences = new GameObject(nameof(EmoteReferences)).AddComponent<EmoteReferences>();
            emoteReferences.Initialize(null, null, null, null, 0, legacy: true);

            var emoteComponent = new CharacterEmoteComponent
            {
                EmoteUrn = $"{GetSceneEmoteFromRealmIntention.SCENE_EMOTE_PREFIX}:{SMART_WEARABLE_ENTITY_ID}-{EMOTE_HASH}-false",
                CurrentEmoteReference = emoteReferences,
            };

            playerEntity = world.Create(new PlayerComponent(), emoteComponent, avatarView);
        }

        protected override void OnTearDown()
        {
            Object.DestroyImmediate(emoteReferences.gameObject);
            Object.DestroyImmediate(audioSourcePrefab);
            Object.DestroyImmediate(poolRoot);
        }

        [Test]
        public void KeepPortableExperienceSceneEmotePlayingWhenItsSceneIsNotCurrent()
        {
            //Arrange
            ISceneFacade portableExperienceScene = NewSceneFacadeWithName(SMART_WEARABLE_ENTITY_ID);
            scenesCache.AddPortableExperienceScene(portableExperienceScene, SMART_WEARABLE_ENTITY_ID);

            //Act
            system!.Update(0);

            //Assert
            Assert.IsNotNull(world.Get<CharacterEmoteComponent>(playerEntity).CurrentEmoteReference);
            messageBus.DidNotReceive().SendStop();
        }

        [Test]
        public void StopSceneEmoteWhenItsSceneIsNoLongerLoaded()
        {
            //Act
            system!.Update(0);

            //Assert
            Assert.IsNull(world.Get<CharacterEmoteComponent>(playerEntity).CurrentEmoteReference);
            avatarView.Received().StopLegacyAnimation();
            messageBus.Received().SendStop();
        }

        [Test]
        public void RecordInterruptedStopWhenSceneEmoteSceneIsNoLongerLoaded()
        {
            //Arrange
            URN emoteUrn = world.Get<CharacterEmoteComponent>(playerEntity).EmoteUrn;

            //Act
            system!.Update(0);

            //Assert: a scene-change cancellation is an interruption, and it survives Reset().
            CharacterEmoteComponent emoteComponent = world.Get<CharacterEmoteComponent>(playerEntity);
            Assert.IsTrue(emoteComponent.PendingStop.IsSet);
            Assert.AreEqual(EmoteState.EsInterrupted, emoteComponent.PendingStop.Reason);
            Assert.AreEqual(emoteUrn, emoteComponent.PendingStop.Urn);
        }

        [Test]
        public void RecordFinishedStopWhenLegacyEmoteEndsNaturally()
        {
            //Arrange: a non-scene emote whose legacy animation stopped on its own.
            const string EMOTE_URN = "urn:decentraland:off-chain:base-emotes:wave";

            ref CharacterEmoteComponent emoteComponent = ref world.Get<CharacterEmoteComponent>(playerEntity);
            emoteComponent.EmoteUrn = EMOTE_URN;
            avatarView.IsLegacyAnimationPlaying.Returns(false);

            //Act
            system!.Update(0);

            //Assert
            CharacterEmoteComponent updated = world.Get<CharacterEmoteComponent>(playerEntity);
            Assert.IsNull(updated.CurrentEmoteReference);
            Assert.IsTrue(updated.PendingStop.IsSet);
            Assert.AreEqual(EmoteState.EsFinished, updated.PendingStop.Reason);
            Assert.AreEqual(EMOTE_URN, updated.PendingStop.Urn.ToString());
        }

        [Test]
        public void RecordInterruptedStopWhenStopIsRequested()
        {
            //Arrange: a non-scene emote explicitly asked to stop (e.g. remote stop or restricted action).
            const string EMOTE_URN = "urn:decentraland:off-chain:base-emotes:dance";

            ref CharacterEmoteComponent emoteComponent = ref world.Get<CharacterEmoteComponent>(playerEntity);
            emoteComponent.EmoteUrn = EMOTE_URN;
            emoteComponent.StopEmote = true;

            //Act
            system!.Update(0);

            //Assert
            CharacterEmoteComponent updated = world.Get<CharacterEmoteComponent>(playerEntity);
            Assert.IsNull(updated.CurrentEmoteReference);
            Assert.IsTrue(updated.PendingStop.IsSet);
            Assert.AreEqual(EmoteState.EsInterrupted, updated.PendingStop.Reason);
            Assert.AreEqual(EMOTE_URN, updated.PendingStop.Urn.ToString());
        }

        [Test]
        public void SkipAnimatorPollForAvatarsThatAreNotEmoting()
        {
            //Arrange: no emote reference and no animation tag left over from a previous emote.
            IAvatarView idleAvatarView = Substitute.For<IAvatarView>();
            world.Create(new CharacterEmoteComponent(), idleAvatarView);

            //Act
            system!.Update(0);

            //Assert
            idleAvatarView.DidNotReceive().GetAnimatorCurrentStateTag(Arg.Any<int>());
        }

        [Test]
        public void PollAnimatorForAvatarsHoldingAnEmoteReference()
        {
            //Arrange
            IAvatarView emotingAvatarView = Substitute.For<IAvatarView>();
            emotingAvatarView.IsLegacyAnimationPlaying.Returns(true);
            world.Create(new CharacterEmoteComponent { CurrentEmoteReference = emoteReferences }, emotingAvatarView);

            //Act
            system!.Update(0);

            //Assert
            emotingAvatarView.Received(1).GetAnimatorCurrentStateTag(AnimatorEmoteLayers.BASE_LAYER_INDEX);
        }

        [Test]
        public void PollAnimatorUntilAStaleAnimationTagIsCleared()
        {
            //Arrange: the emote reference is gone but the tag from its last poll is still set.
            IAvatarView staleTagAvatarView = Substitute.For<IAvatarView>();
            staleTagAvatarView.GetAnimatorCurrentStateTag(AnimatorEmoteLayers.BASE_LAYER_INDEX).Returns(0);

            var emoteComponent = new CharacterEmoteComponent();
            emoteComponent.SetAnimationTag(AnimationHashes.EMOTE);
            Entity entity = world.Create(emoteComponent, staleTagAvatarView);

            //Act
            system!.Update(0);

            //Assert
            staleTagAvatarView.Received(1).GetAnimatorCurrentStateTag(AnimatorEmoteLayers.BASE_LAYER_INDEX);
            Assert.AreEqual(0, world.Get<CharacterEmoteComponent>(entity).CurrentAnimationTag);
        }

        [TestCase("scene-QmHash", "scene", "QmHash")]
        [TestCase("my-scene-QmHash", "my-scene", "QmHash")]
        [TestCase("scene-", "scene", "")]
        [TestCase("scene-Qm-hash-with-dashes", "scene", "Qm-hash-with-dashes")]
        public void MatchSceneEmotePayloadAgainstCandidateName(string payload, string candidateName, string expectedHash)
        {
            Assert.IsTrue(CharacterEmoteSystem.TryMatchSceneEmotePayload(payload.AsSpan(), candidateName, out string parsedHash));
            Assert.AreEqual(expectedHash, parsedHash);
        }

        [TestCase("scenex-QmHash", "scene")]
        [TestCase("sce", "scene")]
        [TestCase("scene", "scene")]
        [TestCase("Scene-QmHash", "scene")]
        public void RejectSceneEmotePayloadThatIsNotTheCandidateNameFollowedByADash(string payload, string candidateName)
        {
            Assert.IsFalse(CharacterEmoteSystem.TryMatchSceneEmotePayload(payload.AsSpan(), candidateName, out string parsedHash));
            Assert.AreEqual(string.Empty, parsedHash);
        }

        private static ISceneFacade NewSceneFacadeWithName(string name)
        {
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneShortInfo.Returns(new SceneShortInfo(Vector2Int.zero, name));

            ISceneFacade sceneFacade = Substitute.For<ISceneFacade>();
            sceneFacade.SceneData.Returns(sceneData);
            return sceneFacade;
        }
    }
}
