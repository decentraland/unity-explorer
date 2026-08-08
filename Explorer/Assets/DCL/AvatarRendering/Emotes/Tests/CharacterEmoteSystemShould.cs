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
        /// Regression for the "emote-lock-after-fish-catch" bug: a scene emote whose asset never resolves
        /// (e.g. evicted from the memory-pressure cache sweep before it ever played, as happens with the
        /// Genesis Plaza fishing catch/reveal emotes when the cinematic breaks) leaves a stranded
        /// CharacterEmoteIntent on the player entity. UpdateEmoteInputSystem.TriggerEmote is
        /// [None(typeof(CharacterEmoteIntent))], so a stranded intent silently blocks every user emote
        /// (slot shortcuts and the wheel) until the #6531 play-timeout watchdog removes it.
        /// </summary>
        [Test]
        public void RemoveStrandedCharacterEmoteIntentAfterPlayTimeoutElapses()
        {
            // The fixed watchdog path legitimately emits "[Error] Cant play emote ... timeout reached."
            // (pre-existing ReportHub.LogError relocated by potential-fix.patch); without this the
            // Unity Test Framework fails the test on the unhandled error log. Set inside the body:
            // the framework resets LogAssert state after [SetUp], before the test body runs.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            // Arrange: a full-body scene emote resolved in the storage but whose per-body-shape asset
            // never arrived (AssetResults entry stays null), so ConsumeEmoteIntent keeps parking on the
            // "Loading not complete" branch every frame instead of playing or failing outright.
            IAvatarView strandedAvatarView = Substitute.For<IAvatarView>();

            // Grounded and not jumping/moving: bypasses the animator park gate so the query reaches the
            // emote-storage branch below instead of parking earlier for an unrelated reason.
            strandedAvatarView.GetAnimatorBool(AnimationHashes.GROUNDED).Returns(true);

            IEmote emote = Substitute.For<IEmote>();
            emote.IsLoading.Returns(false);

            // Empty results: the asset is not resident (e.g. evicted with a zero refcount before it ever
            // played), so ConsumeEmoteIntent takes the "assetResult == null" return every frame.
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
                    EmoteId = new URN("urn:decentraland:off-chain:scene-emote:pond-fishing_catch-false"),
                    Mask = AvatarEmoteMask.AemFullBody,
                },
                strandedAvatarView,
                new AvatarShapeComponent { BodyShape = BodyShape.MALE });

            // Act: simulate more than StreamableLoadingDefaults.TIMEOUT seconds of frames, one second of
            // dt at a time. A single call with a large dt would not reproduce the pin's bug (the very
            // first call is unaffected by the precedence bug — see CharacterEmoteIntentShould), so the
            // repeated small-dt calls are load-bearing for the regression.
            for (var second = 0; second < StreamableLoadingDefaults.TIMEOUT + 1; second++)
                system!.Update(1f);

            // Assert
            Assert.IsFalse(world.Has<CharacterEmoteIntent>(strandedEntity),
                "A CharacterEmoteIntent whose asset never resolves must expire after StreamableLoadingDefaults.TIMEOUT " +
                "seconds via the #6531 watchdog, instead of permanently blocking UpdateEmoteInputSystem.TriggerEmote's " +
                "[None(CharacterEmoteIntent)] gate (the restart-only emote lock from emote-lock-after-fish-catch).");
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
