using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Play;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class EmotePlayerShould
    {
        private GameObject poolRoot = null!;
        private GameObject audioSourcePrefab = null!;
        private GameObject legacyEmoteAsset = null!;
        private GameObject avatarGameObject = null!;
        private EmotePlayer emotePlayer = null!;
        private IAvatarView avatarView = null!;

        [SetUp]
        public void Setup()
        {
            poolRoot = new GameObject("ROOT_POOL_CONTAINER");
            audioSourcePrefab = new GameObject("EmoteAudioSource");
            audioSourcePrefab.AddComponent<AudioSource>();

            emotePlayer = new EmotePlayer(audioSourcePrefab.GetComponent<AudioSource>(),
                ScriptableObject.CreateInstance<EmoteMaskCatalog>(), legacyAnimationsEnabled: false);

            // Legacy emote asset: an Animator with no controller (so CreateNewEmoteReference takes the
            // legacy branch) plus an Animation carrying a legacy clip.
            legacyEmoteAsset = new GameObject("LegacyEmoteAsset");
            legacyEmoteAsset.AddComponent<Animator>();
            var animation = legacyEmoteAsset.AddComponent<Animation>();
            var clip = new AnimationClip { legacy = true, name = "wave" };
            animation.AddClip(clip, clip.name);

            avatarGameObject = new GameObject("Avatar");

            avatarView = Substitute.For<IAvatarView>();
            avatarView.GetTransform().Returns(avatarGameObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(avatarGameObject);
            Object.DestroyImmediate(legacyEmoteAsset);
            Object.DestroyImmediate(audioSourcePrefab);
            Object.DestroyImmediate(poolRoot);
        }

        // Regression for https://github.com/decentraland/unity-explorer/issues/9665: a legacy emote
        // rejected because legacyAnimationsEnabled is false must return its pooled instance to the pool.
        // Before the fix the reference was registered in emotesInUse only after the early-out, so Stop()
        // released nothing and the instance stayed parented under the avatar, leaking on every play.
        [Test]
        public void NotLeakPooledInstanceWhenLegacyEmoteRejected()
        {
            var emoteComponent = new CharacterEmoteComponent();

            bool played = emotePlayer.Play(legacyEmoteAsset, null, false, false, in avatarView, ref emoteComponent);

            Assert.IsFalse(played);
            Assert.IsNull(emoteComponent.CurrentEmoteReference);
            Assert.IsNull(avatarGameObject.GetComponentInChildren<EmoteReferences>(true),
                "Rejected legacy emote leaked a pooled instance under the avatar.");
        }
    }
}
