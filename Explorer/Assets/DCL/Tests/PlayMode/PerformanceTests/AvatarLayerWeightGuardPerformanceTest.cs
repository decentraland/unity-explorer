using DCL.AvatarRendering.AvatarShape.UnityInterface;
using NUnit.Framework;
using System.Diagnostics;
using System.Reflection;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    /// Verifies SetPointAtLayerWeight / SetRotationLayerWeight skip the native Animator.SetLayerWeight write when the
    /// value is unchanged from the last call, and that ResetState clears the shadowed value so the next write after
    /// a rebind (e.g. pool reuse) is not skipped.
    /// </summary>
    [Category("Performance")]
    public class AvatarLayerWeightGuardPerformanceTest
    {
#if UNITY_EDITOR
        private const string AVATAR_BASE_TEST_ASSET_PATH = "Assets/DCL/AvatarRendering/AvatarShape/Tests/Instantiate/TestAssets/AvatarBase_TestAsset.prefab";
        private const string ANIMATOR_CONTROLLER_PATH = "Assets/DCL/AvatarRendering/AvatarShape/Assets/Animator/CharacterAnimator.controller";

        private GameObject avatarGameObject = null!;
        private AvatarBase avatarBase = null!;
        private Animator animator = null!;
        private int pointAtIndex;
        private int rotationIndex;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR_BASE_TEST_ASSET_PATH);
            Assert.IsNotNull(prefab, $"Could not load AvatarBase test prefab from {AVATAR_BASE_TEST_ASSET_PATH}");

            avatarGameObject = Object.Instantiate(prefab);
            avatarBase = avatarGameObject.GetComponentInChildren<AvatarBase>();
            animator = avatarBase.AvatarAnimator;

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ANIMATOR_CONTROLLER_PATH);
            Assert.IsNotNull(controller, $"Could not load animator controller from {ANIMATOR_CONTROLLER_PATH}");
            animator.runtimeAnimatorController = controller;

            typeof(AvatarBase).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)!
                              .Invoke(avatarBase, null);

            pointAtIndex = animator.GetLayerIndex("RightPointAtHand");
            rotationIndex = animator.GetLayerIndex("Rotation");
            Assert.GreaterOrEqual(pointAtIndex, 0, "RightPointAtHand layer missing from controller");
            Assert.GreaterOrEqual(rotationIndex, 0, "Rotation layer missing from controller");
        }

        [TearDown]
        public void TearDown()
        {
            if (avatarGameObject != null) Object.DestroyImmediate(avatarGameObject);
        }

        [Test]
        [Performance]
        public void RedundantLayerWeightWrites_AreElided_AndResetStateRearms()
        {
            avatarBase.SetPointAtLayerWeight(0.5f);
            Assert.AreEqual(0.5f, animator.GetLayerWeight(pointAtIndex), 1e-4f);
            avatarBase.SetPointAtLayerWeight(1f);
            Assert.AreEqual(1f, animator.GetLayerWeight(pointAtIndex), 1e-4f);

            avatarBase.SetRotationLayerWeight(0.25f);
            Assert.AreEqual(0.25f, animator.GetLayerWeight(rotationIndex), 1e-4f);

            avatarBase.SetPointAtLayerWeight(0.7f);
            avatarBase.SetRotationLayerWeight(0.7f);
            avatarBase.ResetState();
            Assert.AreNotEqual(0.7f, animator.GetLayerWeight(pointAtIndex), "Rebind should have reset the native weight");

            avatarBase.SetPointAtLayerWeight(0.7f);
            Assert.AreEqual(0.7f, animator.GetLayerWeight(pointAtIndex), 1e-4f, "shadow was not re-armed by ResetState (point-at)");
            avatarBase.SetRotationLayerWeight(0.7f);
            Assert.AreEqual(0.7f, animator.GetLayerWeight(rotationIndex), 1e-4f, "shadow was not re-armed by ResetState (rotation)");

            avatarBase.SetPointAtLayerWeight(0f);

            const int ITER = 200_000;
            long bestRedundant = long.MaxValue, bestAlternating = long.MaxValue;

            for (int run = 0; run < 3; run++)
            {
                var sw = Stopwatch.StartNew();
                for (int k = 0; k < ITER; k++) avatarBase.SetPointAtLayerWeight(0f);
                sw.Stop();
                bestRedundant = System.Math.Min(bestRedundant, sw.ElapsedTicks);

                sw.Restart();
                for (int k = 0; k < ITER; k++) avatarBase.SetPointAtLayerWeight(k % 2);
                sw.Stop();
                bestAlternating = System.Math.Min(bestAlternating, sw.ElapsedTicks);
            }

            Measure.Custom(new SampleGroup("RedundantWeightWrites", SampleUnit.Nanosecond), bestRedundant);
            Measure.Custom(new SampleGroup("AlternatingWeightWrites", SampleUnit.Nanosecond), bestAlternating);

            Assert.Less(bestRedundant, bestAlternating * 0.5, $"redundant writes ({bestRedundant}) should be far cheaper than alternating ({bestAlternating})");

            avatarBase.SetPointAtLayerWeight(0f);
            ProfilerRecorder gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
            Measure.Method(() => { for (int k = 0; k < 1000; k++) avatarBase.SetPointAtLayerWeight(0f); })
                   .WarmupCount(5).MeasurementCount(10).GC().Run();
            long gcBytes = gcAlloc.LastValue;
            gcAlloc.Dispose();
            Assert.AreEqual(0, gcBytes, $"redundant weight writes must be allocation-free, allocated {gcBytes} bytes");
        }
#endif
    }
}
