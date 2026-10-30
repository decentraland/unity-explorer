using DCL.ECSComponents;
using NUnit.Framework;
using UnityEngine;
using Utility.Animations;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class CharacterMaskedEmoteComponentShould
    {
        private const float CLIP_LENGTH = 2f;

        private GameObject gameObject = null!;
        private EmoteReferences emoteReferences = null!;
        private AnimationClip clip = null!;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject(nameof(CharacterMaskedEmoteComponentShould));
            emoteReferences = gameObject.AddComponent<EmoteReferences>();

            clip = new AnimationClip { name = "TestClip" };
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, CLIP_LENGTH, 1));
        }

        [TearDown]
        public void TearDown()
        {
            if (gameObject != null) Object.DestroyImmediate(gameObject);
            if (clip != null) Object.DestroyImmediate(clip);
        }

        [Test]
        public void PlayingEmoteDuration_ReturnsZero_WhenNoReferenceIsSet()
        {
            var masked = new CharacterMaskedEmoteComponent();

            Assert.AreEqual(0f, masked.PlayingEmoteDuration, 0.001f);
        }

        [Test]
        public void PlayingEmoteDuration_ReturnsZero_WhenReferenceHasNoAvatarClip()
        {
            emoteReferences.Initialize(null, null, null, null, 0, legacy: false);

            var masked = new CharacterMaskedEmoteComponent { CurrentEmoteReference = emoteReferences };

            Assert.AreEqual(0f, masked.PlayingEmoteDuration, 0.001f);
        }

        [Test]
        public void PlayingEmoteDuration_ReturnsAvatarClipLength()
        {
            emoteReferences.Initialize(clip, null, null, null, 0, legacy: false);

            var masked = new CharacterMaskedEmoteComponent { CurrentEmoteReference = emoteReferences };

            Assert.AreEqual(CLIP_LENGTH, masked.PlayingEmoteDuration, 0.001f,
                "The broadcast duration of a masked emote comes from the masked clip itself, not from the full body emote.");
        }

        [Test]
        public void PlayingEmoteDuration_ScalesWithAnimatorSpeed()
        {
            Animator animator = gameObject.AddComponent<Animator>();
            animator.speed = 2f;
            emoteReferences.Initialize(clip, null, animator, null, 0, legacy: false);

            var masked = new CharacterMaskedEmoteComponent { CurrentEmoteReference = emoteReferences };

            Assert.AreEqual(CLIP_LENGTH * 2f, masked.PlayingEmoteDuration, 0.001f);
        }

        [Test]
        public void IsPlaying_ReturnsFalse_WhenNoReferenceIsSet()
        {
            var masked = new CharacterMaskedEmoteComponent();
            masked.SetAnimationTag(AnimationHashes.MASKED_EMOTE);

            Assert.IsFalse(masked.IsPlaying);
        }

        [Test]
        public void IsPlaying_ReturnsTrue_WhenAnimatorInMaskedEmoteLoopTag()
        {
            emoteReferences.Initialize(clip, null, null, null, 0, legacy: false);

            var masked = new CharacterMaskedEmoteComponent { CurrentEmoteReference = emoteReferences };
            masked.SetAnimationTag(AnimationHashes.MASKED_EMOTE_LOOP);

            Assert.IsTrue(masked.IsPlaying);
        }

        [Test]
        public void Reset_ClearsTheEmoteUrn()
        {
            var masked = new CharacterMaskedEmoteComponent
            {
                EmoteUrn = "urn:decentraland:off-chain:scene-emote:test-scene-hash-false",
                EmoteLoop = true,
                CurrentEmoteReference = emoteReferences,
                Mask = AvatarEmoteMask.AemUpperBody,
            };

            masked.Reset();

            Assert.IsTrue(masked.EmoteUrn.IsNullOrEmpty(),
                "Clearing the urn is what stops a finished emote from being replayed.");
            Assert.IsNull(masked.CurrentEmoteReference);
            Assert.IsFalse(masked.EmoteLoop);
        }
    }
}
