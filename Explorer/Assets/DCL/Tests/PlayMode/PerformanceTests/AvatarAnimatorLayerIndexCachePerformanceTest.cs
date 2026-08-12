using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.ECSComponents;
using NUnit.Framework;
using System.Diagnostics;
using System.Reflection;
using Unity.PerformanceTesting;
using UnityEngine;
using Utility.Animations;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    /// Verifies the int-indexed layer lookups exposed by AvatarBase (cached in Awake) return identical results to
    /// the string-based Animator lookups for every layer, including after a state change on the Upper Body layer,
    /// and that the int path is faster than the string path it replaces.
    /// </summary>
    [Category("Performance")]
    public class AvatarAnimatorLayerIndexCachePerformanceTest
    {
#if UNITY_EDITOR
        private const string AVATAR_BASE_TEST_ASSET_PATH = "Assets/DCL/AvatarRendering/AvatarShape/Tests/Instantiate/TestAssets/AvatarBase_TestAsset.prefab";
        private const string ANIMATOR_CONTROLLER_PATH = "Assets/DCL/AvatarRendering/AvatarShape/Assets/Animator/CharacterAnimator.controller";

        private GameObject avatarGameObject = null!;
        private AvatarBase avatarBase = null!;
        private Animator animator = null!;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR_BASE_TEST_ASSET_PATH);
            Assert.IsNotNull(prefab, $"Could not load AvatarBase test prefab from {AVATAR_BASE_TEST_ASSET_PATH}");

            avatarGameObject = Object.Instantiate(prefab);
            avatarBase = avatarGameObject.GetComponentInChildren<AvatarBase>();
            Assert.IsNotNull(avatarBase, "AvatarBase component not found on test prefab");

            animator = avatarBase.AvatarAnimator;
            Assert.IsNotNull(animator, "AvatarAnimator not configured on test prefab");

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ANIMATOR_CONTROLLER_PATH);
            Assert.IsNotNull(controller, $"Could not load animator controller from {ANIMATOR_CONTROLLER_PATH}");
            animator.runtimeAnimatorController = controller;

            typeof(AvatarBase).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)!
                              .Invoke(avatarBase, null);

            animator.Update(0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (avatarGameObject != null) Object.DestroyImmediate(avatarGameObject);
        }

        [Test]
        [Performance]
        public void IntTagLookup_MatchesStringPath_AndIsFaster()
        {
            Assert.AreEqual(0, animator.GetLayerIndex(AnimatorEmoteLayers.BASE_LAYER), "Base Layer must be index 0");
            Assert.AreEqual(AnimatorEmoteLayers.BASE_LAYER_INDEX, animator.GetLayerIndex(AnimatorEmoteLayers.BASE_LAYER));

            int upperBodyIndex = animator.GetLayerIndex(AnimatorEmoteLayers.UPPER_BODY_LAYER);
            Assert.AreEqual(upperBodyIndex, avatarBase.UpperBodyLayerIndex, "Cached UpperBodyLayerIndex must match the string lookup");

            for (int i = 0; i < animator.layerCount; i++)
            {
                string layerName = animator.GetLayerName(i);
                Assert.AreEqual(avatarBase.GetAnimatorCurrentStateTag(layerName), avatarBase.GetAnimatorCurrentStateTag(i),
                    $"int/string tag lookup diverged on layer {i} ({layerName})");
            }

            if (upperBodyIndex >= 0)
            {
                animator.Play(animator.GetCurrentAnimatorStateInfo(upperBodyIndex).fullPathHash, upperBodyIndex, 0f);
                animator.Update(0f);

                for (int i = 0; i < animator.layerCount; i++)
                {
                    string layerName = animator.GetLayerName(i);
                    Assert.AreEqual(avatarBase.GetAnimatorCurrentStateTag(layerName), avatarBase.GetAnimatorCurrentStateTag(i));
                }
            }

            Assert.AreEqual(0, avatarBase.GetEmoteLayerIndex(AvatarEmoteMask.AemFullBody));
            Assert.AreEqual(avatarBase.UpperBodyLayerIndex, avatarBase.GetEmoteLayerIndex(AvatarEmoteMask.AemUpperBody));

            const int ITER = 200_000;
            long bestString = long.MaxValue, bestInt = long.MaxValue;

            for (int run = 0; run < 5; run++)
            {
                var sw = Stopwatch.StartNew();
                for (int k = 0; k < ITER; k++) avatarBase.GetAnimatorCurrentStateTag(AnimatorEmoteLayers.UPPER_BODY_LAYER);
                sw.Stop();
                bestString = System.Math.Min(bestString, sw.ElapsedTicks);

                sw.Restart();
                for (int k = 0; k < ITER; k++) avatarBase.GetAnimatorCurrentStateTag(upperBodyIndex);
                sw.Stop();
                bestInt = System.Math.Min(bestInt, sw.ElapsedTicks);
            }

            Measure.Custom(new SampleGroup("StringLayerLookup", SampleUnit.Nanosecond), bestString);
            Measure.Custom(new SampleGroup("IntLayerLookup", SampleUnit.Nanosecond), bestInt);

            Assert.Less(bestInt, bestString, $"int lookup ({bestInt} ticks) must beat string lookup ({bestString} ticks)");
        }
#endif
    }
}
