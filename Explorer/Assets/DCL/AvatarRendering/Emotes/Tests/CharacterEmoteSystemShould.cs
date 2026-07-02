using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Play;
using DCL.DebugUtilities;
using DCL.Multiplayer.Emotes;
using ECS.SceneLifeCycle;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Utility.Animations;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Emotes.Tests
{
    /// <summary>
    ///     Covers the stuck-emote watchdog (https://github.com/decentraland/unity-explorer/issues/9115):
    ///     the avatar must always recover when the animator never leaves an emote-tagged state.
    /// </summary>
    public class CharacterEmoteSystemShould : UnitySystemTestBase<CharacterEmoteSystem>
    {
        private IAvatarView avatarView = null!;
        private IEmotesMessageBus messageBus = null!;
        private GameObject poolRoot = null!;
        private GameObject audioSourcePrefab = null!;
        private GameObject emoteReferencesGameObject = null!;
        private EmoteMaskCatalog emoteMaskCatalog = null!;

        [SetUp]
        public void SetUp()
        {
            poolRoot = new GameObject("ROOT_POOL_CONTAINER");
            audioSourcePrefab = new GameObject("AudioSourcePrefab");
            emoteReferencesGameObject = new GameObject(nameof(CharacterEmoteSystemShould));
            emoteMaskCatalog = ScriptableObject.CreateInstance<EmoteMaskCatalog>();

            var emotePlayer = new EmotePlayer(audioSourcePrefab.AddComponent<AudioSource>(), emoteMaskCatalog);

            messageBus = Substitute.For<IEmotesMessageBus>();

            system = new CharacterEmoteSystem(
                world,
                Substitute.For<IEmoteStorage>(),
                messageBus,
                emotePlayer,
                Substitute.For<IDebugContainerBuilder>(),
                false,
                Substitute.For<IScenesCache>());

            avatarView = Substitute.For<IAvatarView>();
        }

        protected override void OnTearDown()
        {
            if (poolRoot != null) Object.DestroyImmediate(poolRoot);
            if (audioSourcePrefab != null) Object.DestroyImmediate(audioSourcePrefab);
            if (emoteReferencesGameObject != null) Object.DestroyImmediate(emoteReferencesGameObject);
            if (emoteMaskCatalog != null) Object.DestroyImmediate(emoteMaskCatalog);
        }

        [Test]
        public void ReissueStopTrigger_WhenAnimatorStuckInEmoteTagWithoutReference()
        {
            // Arrange: the animator reports an emote tag but the emote was already torn down (no reference)
            avatarView.GetAnimatorCurrentStateTag(AnimatorEmoteLayers.BASE_LAYER).Returns(AnimationHashes.EMOTE);
            world.Create(new CharacterEmoteComponent(), avatarView);

            // Act: exceed the retry period
            system!.Update(0.5f);
            system.Update(0.5f);

            // Assert
            avatarView.Received(1).SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);
            avatarView.Received(1).SetAnimatorBool(AnimationHashes.EMOTE_LOOP, false);

            // Act: a further period elapses while still stuck
            system.Update(0.5f);
            system.Update(0.5f);

            // Assert: the stop trigger is re-issued periodically, not every frame
            avatarView.Received(2).SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);
        }

        [Test]
        public void NotReissueStopTrigger_WithinTransitionGraceWindow()
        {
            // Arrange: same dead-end state, but less time than the retry period elapses
            avatarView.GetAnimatorCurrentStateTag(AnimatorEmoteLayers.BASE_LAYER).Returns(AnimationHashes.EMOTE);
            world.Create(new CharacterEmoteComponent(), avatarView);

            // Act
            system!.Update(0.9f);

            // Assert: the normal post-stop transition window is left alone
            avatarView.DidNotReceive().SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);
        }

        [Test]
        public void ForceStopEmote_WhenNonLoopingEmoteExceedsDuration()
        {
            // Arrange: a non-looping emote whose animator never leaves the emote tag
            avatarView.GetAnimatorCurrentStateTag(AnimatorEmoteLayers.BASE_LAYER).Returns(AnimationHashes.EMOTE);
            EmoteReferences emoteReferences = emoteReferencesGameObject.AddComponent<EmoteReferences>();
            emoteReferences.Initialize(null, null, null, null, 0, legacy: false);
            Entity entity = world.Create(new CharacterEmoteComponent { CurrentEmoteReference = emoteReferences }, avatarView);

            // Act: exceed PlayingEmoteDuration (0 for a null clip) + grace
            system!.Update(0.6f);
            system.Update(0.6f);

            // Assert: full teardown ran, without broadcasting a stop for a non-player entity
            Assert.IsNull(world.Get<CharacterEmoteComponent>(entity).CurrentEmoteReference);
            avatarView.Received(1).SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);
            messageBus.DidNotReceive().SendStop();
        }

        [Test]
        public void NotStop_LoopingEmote()
        {
            // Arrange: an intentionally looping emote
            avatarView.GetAnimatorCurrentStateTag(AnimatorEmoteLayers.BASE_LAYER).Returns(AnimationHashes.EMOTE_LOOP);
            EmoteReferences emoteReferences = emoteReferencesGameObject.AddComponent<EmoteReferences>();
            emoteReferences.Initialize(null, null, null, null, 0, legacy: false);
            Entity entity = world.Create(new CharacterEmoteComponent { CurrentEmoteReference = emoteReferences, EmoteLoop = true }, avatarView);

            // Act: simulate a long time playing
            for (var i = 0; i < 30; i++)
                system!.Update(1f);

            // Assert
            Assert.IsNotNull(world.Get<CharacterEmoteComponent>(entity).CurrentEmoteReference);
            avatarView.DidNotReceive().SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);
        }

        [Test]
        public void NotStop_LegacyEmoteViaWatchdog()
        {
            // Arrange: legacy emotes are torn down by the IsLegacyAnimationPlaying poll, not by the watchdog
            avatarView.IsLegacyAnimationPlaying.Returns(true);
            EmoteReferences emoteReferences = emoteReferencesGameObject.AddComponent<EmoteReferences>();
            emoteReferences.Initialize(null, null, null, null, 0, legacy: true);
            Entity entity = world.Create(new CharacterEmoteComponent { CurrentEmoteReference = emoteReferences }, avatarView);

            // Act
            for (var i = 0; i < 30; i++)
                system!.Update(1f);

            // Assert
            Assert.IsNotNull(world.Get<CharacterEmoteComponent>(entity).CurrentEmoteReference);
            avatarView.DidNotReceive().SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);
        }
    }
}
