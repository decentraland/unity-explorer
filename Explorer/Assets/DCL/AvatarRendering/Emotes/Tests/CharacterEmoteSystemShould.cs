using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Play;
using DCL.Character.Components;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using ECS.SceneLifeCycle;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
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
