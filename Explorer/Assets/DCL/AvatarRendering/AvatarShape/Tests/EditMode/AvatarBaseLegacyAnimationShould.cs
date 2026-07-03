using DCL.AvatarRendering.AvatarShape.UnityInterface;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Utility.Animations;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarBaseLegacyAnimationShould
    {
        private const string AVATAR_BASE_TEST_ASSET_PATH = "Assets/DCL/AvatarRendering/AvatarShape/Tests/Instantiate/TestAssets/AvatarBase_TestAsset.prefab";

        private GameObject avatarGameObject = null!;
        private AvatarBase avatarBase = null!;

        [SetUp]
        public void SetUp()
        {
            var avatarBasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR_BASE_TEST_ASSET_PATH);
            Assert.IsNotNull(avatarBasePrefab, $"Could not load AvatarBase test prefab from {AVATAR_BASE_TEST_ASSET_PATH}");

            avatarGameObject = Object.Instantiate(avatarBasePrefab);
            avatarBase = avatarGameObject.GetComponentInChildren<AvatarBase>();
            Assert.IsNotNull(avatarBase, "AvatarBase component not found on test prefab");
            Assert.IsNotNull(avatarBase.AvatarAnimator, "AvatarAnimator not configured on test prefab");
        }

        [TearDown]
        public void TearDown()
        {
            if (avatarGameObject != null) Object.DestroyImmediate(avatarGameObject);
        }

        [Test]
        public void AddOrGetLegacyAnimation_CreatesAnimationOnAvatarAnimatorGameObject()
        {
            Animation legacyAnimation = avatarBase.AddOrGetLegacyAnimation();

            Assert.IsNotNull(legacyAnimation);
            Assert.AreSame(legacyAnimation, avatarBase.LegacyAnimation);
            Assert.AreSame(avatarBase.AvatarAnimator.gameObject, legacyAnimation.gameObject,
                "PlayLegacyEmote expects the Animation component to live on the same GameObject as the Animator.");
        }

        [Test]
        public void AddOrGetLegacyAnimation_ReturnsSameInstance_OnRepeatedCalls()
        {
            Animation first = avatarBase.AddOrGetLegacyAnimation();
            Animation second = avatarBase.AddOrGetLegacyAnimation();

            Assert.AreSame(first, second,
                "Repeated calls must not create duplicate Animation components — AddClip relies on a single stable Animation instance.");
        }

        [Test]
        public void IsLegacyAnimationPlaying_ReturnsFalse_WhenLegacyAnimationExistsButIsNotPlaying()
        {
            avatarBase.AddOrGetLegacyAnimation();

            Assert.IsFalse(avatarBase.IsLegacyAnimationPlaying,
                "An idle Animation component must not gate the Mecanim animator or its setters.");
        }

        [Test]
        public void StopLegacyAnimation_DoesNotThrow_WhenLegacyAnimationIsIdle()
        {
            avatarBase.AddOrGetLegacyAnimation();

            Assert.DoesNotThrow(() => avatarBase.StopLegacyAnimation());
        }

        [Test]
        public void SetAnimatorTrigger_EnablesAnimator_WhenLegacyNotPlaying()
        {
            avatarBase.AvatarAnimator.enabled = false;

            avatarBase.SetAnimatorTrigger(AnimationHashes.EMOTE);

            Assert.IsTrue(avatarBase.AvatarAnimator.enabled,
                "With no legacy animation playing, Mecanim setters must re-enable the Animator to process the new trigger.");
        }

        [Test]
        public void StopLegacyAnimation_StopsAPlayingLegacyClip()
        {
            Animation legacyAnimation = avatarBase.AddOrGetLegacyAnimation();
            AnimationClip clip = CreateLegacyClip();

            legacyAnimation.AddClip(clip, clip.name);
            legacyAnimation.Play(clip.name);

            Assume.That(avatarBase.IsLegacyAnimationPlaying, Is.True,
                "Sanity check: legacy clip should report as playing right after Play() in EditMode.");

            avatarBase.StopLegacyAnimation();

            Assert.IsFalse(avatarBase.IsLegacyAnimationPlaying,
                "StopLegacyAnimation must clear the playing flag so the IsLegacyAnimationPlaying gate releases for the next emote.");

            Object.DestroyImmediate(clip);
        }

        [Test]
        public void SetAnimatorTrigger_Ignored_WhileLegacyAnimationIsPlaying()
        {
            Animation legacyAnimation = avatarBase.AddOrGetLegacyAnimation();
            AnimationClip clip = CreateLegacyClip();
            legacyAnimation.AddClip(clip, clip.name);
            legacyAnimation.Play(clip.name);
            Assume.That(avatarBase.IsLegacyAnimationPlaying, Is.True);

            avatarBase.AvatarAnimator.enabled = false;

            avatarBase.SetAnimatorTrigger(AnimationHashes.EMOTE);

            Assert.IsFalse(avatarBase.AvatarAnimator.enabled,
                "While a legacy Animation is playing the Mecanim animator must stay disabled — otherwise motion systems stomp the legacy pose every frame.");

            Object.DestroyImmediate(clip);
        }

        private static AnimationClip CreateLegacyClip()
        {
            var clip = new AnimationClip { name = "TestLegacyClip", legacy = true };
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
            return clip;
        }
    }
}
