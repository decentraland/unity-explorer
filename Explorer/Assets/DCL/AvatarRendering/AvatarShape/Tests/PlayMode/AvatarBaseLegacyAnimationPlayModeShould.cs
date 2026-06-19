using DCL.AvatarRendering.AvatarShape.UnityInterface;
using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.PlayModeTests
{
    public class AvatarBaseLegacyAnimationPlayModeShould
    {
        private const string AVATAR_BASE_TEST_ASSET_PATH = "Assets/DCL/AvatarRendering/AvatarShape/Tests/Instantiate/TestAssets/AvatarBase_TestAsset.prefab";
        private const string ANIMATOR_CONTROLLER_PATH = "Assets/DCL/AvatarRendering/AvatarShape/Assets/Animator/CharacterAnimator.controller";

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

            // The test prefab ships without a runtime animator controller, so AvatarBase.Awake() builds an
            // AnimatorOverrideController over a null source — its indexer (used by ReplaceEmoteAnimation) then
            // throws NRE. Inject a real controller and rebuild the override so these tests can exercise the
            // legacy-gate logic in ReplaceEmoteAnimation.
            var runtimeController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ANIMATOR_CONTROLLER_PATH);
            Assert.IsNotNull(runtimeController, $"Could not load animator controller from {ANIMATOR_CONTROLLER_PATH}");

            avatarBase.AvatarAnimator.runtimeAnimatorController = runtimeController;

            FieldInfo overrideField = typeof(AvatarBase).GetField("overrideController", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(overrideField, "AvatarBase.overrideController field not found — has it been renamed?");
            overrideField!.SetValue(avatarBase, new AnimatorOverrideController(runtimeController));
        }

        [TearDown]
        public void TearDown()
        {
            if (avatarGameObject != null) Object.DestroyImmediate(avatarGameObject);
        }

        [Test]
        public void ReplaceEmoteAnimation_EnablesAnimator_WhenLegacyNotPlaying()
        {
            avatarBase.AvatarAnimator.enabled = false;
            var clip = new AnimationClip { legacy = false };

            avatarBase.ReplaceEmoteAnimation(clip);

            Assert.IsTrue(avatarBase.AvatarAnimator.enabled,
                "ReplaceEmoteAnimation must re-enable the Mecanim animator when no legacy animation is blocking it.");

            Object.DestroyImmediate(clip);
        }

        [Test]
        public void ReplaceEmoteAnimation_DoesNotEnableAnimator_WhileLegacyAnimationIsPlaying()
        {
            Animation legacyAnimation = avatarBase.AddOrGetLegacyAnimation();
            AnimationClip clip = CreateLegacyClip();
            legacyAnimation.AddClip(clip, clip.name);
            legacyAnimation.Play(clip.name);
            Assume.That(avatarBase.IsLegacyAnimationPlaying, Is.True);

            avatarBase.AvatarAnimator.enabled = false;
            var mecanimClip = new AnimationClip { legacy = false };

            avatarBase.ReplaceEmoteAnimation(mecanimClip);

            Assert.IsFalse(avatarBase.AvatarAnimator.enabled,
                "Overriding the emote slot while legacy is playing must not re-enable the animator — the switch happens after StopLegacyAnimation releases the gate.");

            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(mecanimClip);
        }

        private static AnimationClip CreateLegacyClip()
        {
            var clip = new AnimationClip { name = "TestLegacyClip", legacy = true };
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
            return clip;
        }
    }
}
