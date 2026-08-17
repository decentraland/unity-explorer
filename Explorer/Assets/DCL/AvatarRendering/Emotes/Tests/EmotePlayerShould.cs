using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Play;
using DCL.ECSComponents;
using NSubstitute;
using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class EmotePlayerShould
    {
        private const string LEGACY_CLIP_NAME = "emote_avatar";

        private GameObject poolRoot = null!;
        private GameObject audioSourcePrefab = null!;
        private GameObject avatarRoot = null!;
        private GameObject legacyEmoteAsset = null!;
        private AnimationClip legacyClip = null!;
        private AudioSource audioSource = null!;
        private IAvatarView avatarView = null!;

        [SetUp]
        public void SetUp()
        {
            poolRoot = new GameObject("ROOT_POOL_CONTAINER");
            audioSourcePrefab = new GameObject("EmoteAudioSource");
            audioSource = audioSourcePrefab.AddComponent<AudioSource>();

            avatarRoot = new GameObject("Avatar");
            avatarView = Substitute.For<IAvatarView>();
            avatarView.GetTransform().Returns(avatarRoot.transform);

            legacyClip = new AnimationClip { legacy = true, name = LEGACY_CLIP_NAME };

            // The Animator makes the asset playable while legacy animations are disabled, and the controller-less
            // Animator plus a legacy clip is what makes the acquired EmoteReferences report legacy == true.
            legacyEmoteAsset = new GameObject("LegacyEmoteAsset");
            legacyEmoteAsset.AddComponent<Animator>();
            legacyEmoteAsset.AddComponent<Animation>().AddClip(legacyClip, LEGACY_CLIP_NAME);
        }

        [TearDown]
        public void TearDown()
        {
            if (legacyEmoteAsset != null) Object.DestroyImmediate(legacyEmoteAsset);
            if (legacyClip != null) Object.DestroyImmediate(legacyClip);
            if (avatarRoot != null) Object.DestroyImmediate(avatarRoot);
            if (audioSourcePrefab != null) Object.DestroyImmediate(audioSourcePrefab);
            if (poolRoot != null) Object.DestroyImmediate(poolRoot);
        }

        [Test]
        public void ReleaseEmoteReferencesWhenLegacyPlaybackIsDisabled()
        {
            //Arrange
            var emotePlayer = new EmotePlayer(audioSource, ScriptableObject.CreateInstance<EmoteMaskCatalog>(), legacyAnimationsEnabled: false);
            var emoteComponent = new CharacterEmoteComponent();

            //Act
            bool played = emotePlayer.Play(legacyEmoteAsset, null, isLooping: false, isSpatial: false, avatarView, ref emoteComponent);

            //Assert
            Assert.IsFalse(played);
            Assert.IsNull(emoteComponent.CurrentEmoteReference);
            AssertEmoteReferencesReleasedToPool();
        }

        [Test]
        public void ReleaseEmoteReferencesWhenMaskedLegacyPlaybackFails()
        {
            //Arrange: an empty mask catalog makes the masked legacy path bail out after the references were acquired.
            var emptyMaskCatalog = ScriptableObject.CreateInstance<EmoteMaskCatalog>();
            var emotePlayer = new EmotePlayer(audioSource, emptyMaskCatalog, legacyAnimationsEnabled: true);
            var maskedEmote = new CharacterMaskedEmoteComponent { Mask = AvatarEmoteMask.AemUpperBody };
            LogAssert.Expect(LogType.Error, new Regex($"{nameof(EmoteMaskCatalog)} has no entry for"));

            //Act
            bool played = emotePlayer.PlayMasked(legacyEmoteAsset, null, isLooping: false, isSpatial: false, avatarView, ref maskedEmote);

            //Assert
            Assert.IsFalse(played);
            Assert.IsNull(maskedEmote.CurrentEmoteReference);
            AssertEmoteReferencesReleasedToPool();
        }

        private void AssertEmoteReferencesReleasedToPool()
        {
            Assert.AreEqual(1, poolRoot.GetComponentsInChildren<EmoteReferences>(true).Length,
                "the acquired EmoteReferences must be released back into the pool hierarchy");

            Assert.IsEmpty(avatarRoot.GetComponentsInChildren<EmoteReferences>(true),
                "a failed play must not leave the EmoteReferences parented to the avatar");
        }
    }
}
