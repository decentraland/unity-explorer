using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Play;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.Character.Components;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Emotes;
using ECS.SceneLifeCycle;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using Utility.Animations;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class CharacterEmoteSystemShould : UnitySystemTestBase<CharacterEmoteSystem>
    {
        private const string SMART_WEARABLE_ENTITY_ID = "bafkreiwearable";
        private const string EMOTE_HASH = "bafkreiemotehash";
        private const string SCENE_EMOTE_URN = "urn:decentraland:off-chain:scene-emote:test-scene-bafkreiemotehash-false";

        private ScenesCache scenesCache = null!;
        private IEmotesMessageBus messageBus = null!;
        private IEmoteStorage emoteStorage = null!;
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
            emoteStorage = Substitute.For<IEmoteStorage>();

            system = new CharacterEmoteSystem(world, emoteStorage, messageBus, emotePlayer,
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

        /// <summary>
        /// Regression for https://github.com/decentraland/unity-explorer/issues/6531: an emote whose
        /// per-body-shape asset never resolves keeps ConsumeEmoteIntent parked on the "loading not complete"
        /// branch every frame, so the play timeout is the only thing that can release the intent (and with it
        /// the props of the previously played emote).
        /// </summary>
        [Test]
        public void RemoveStrandedCharacterEmoteIntentAfterPlayTimeoutElapses()
        {
            LogAssert.Expect(LogType.Error, new Regex("Cant play emote .* timeout reached"));

            IAvatarView strandedAvatarView = Substitute.For<IAvatarView>();

            // Grounded and not moving, so the intent reaches the emote-storage branch instead of parking
            // on the movement gate.
            strandedAvatarView.GetAnimatorBool(AnimationHashes.GROUNDED).Returns(true);

            IEmote emote = Substitute.For<IEmote>();
            emote.IsLoading.Returns(false);

            // Empty results: the asset is not resident, so playback stops short of the avatar view every frame.
            emote.AssetResults.Returns(new StreamableLoadingResult<AttachmentRegularAsset>?[BodyShape.COUNT]);

            emoteStorage.TryGetElement(Arg.Any<URN>(), out Arg.Any<IEmote>())
                        .Returns(call =>
                         {
                             call[1] = emote;
                             return true;
                         });

            Entity strandedEntity = world.Create(
                new CharacterEmoteComponent(),
                new CharacterEmoteIntent
                {
                    EmoteId = new URN(SCENE_EMOTE_URN),
                    Mask = AvatarEmoteMask.AemFullBody,
                },
                strandedAvatarView,
                new AvatarShapeComponent { BodyShape = BodyShape.MALE });

            for (var second = 0; second < StreamableLoadingDefaults.TIMEOUT + 1; second++)
                system!.Update(1f);

            Assert.IsFalse(world.Has<CharacterEmoteIntent>(strandedEntity),
                "A CharacterEmoteIntent whose asset never resolves must expire after StreamableLoadingDefaults.TIMEOUT seconds of elapsed play time.");
        }

        /// <summary>
        /// The play timeout must not count the time an intent spends waiting for the avatar to stop moving:
        /// that wait is user driven and unbounded, so counting it would discard the queued emote of a player
        /// who simply keeps running or jumping for StreamableLoadingDefaults.TIMEOUT seconds.
        /// </summary>
        [Test]
        public void KeepCharacterEmoteIntentWhileTheAvatarKeepsMoving()
        {
            IAvatarView movingAvatarView = Substitute.For<IAvatarView>();
            movingAvatarView.GetAnimatorBool(AnimationHashes.GROUNDED).Returns(true);
            movingAvatarView.GetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND).Returns(1f);

            Entity movingEntity = world.Create(
                new CharacterEmoteComponent(),
                new CharacterEmoteIntent
                {
                    EmoteId = new URN(SCENE_EMOTE_URN),
                    Mask = AvatarEmoteMask.AemFullBody,
                },
                movingAvatarView,
                new AvatarShapeComponent { BodyShape = BodyShape.MALE });

            for (var second = 0; second < StreamableLoadingDefaults.TIMEOUT + 1; second++)
                system!.Update(1f);

            Assert.IsTrue(world.Has<CharacterEmoteIntent>(movingEntity),
                "An intent held back by the movement gate must survive: the avatar is moving, which is not a stuck state.");

            emoteStorage.DidNotReceive().TryGetElement(Arg.Any<URN>(), out Arg.Any<IEmote>());
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
