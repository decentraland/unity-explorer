using Arch.Core;
using DCL.ECSComponents;
using DCL.SDKComponents.Animator.Components;
using DCL.SDKComponents.Animator.Systems;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    /// Verifies SDKAnimationState's memoized per-clip param hashes match Animator.StringToHash for the interpolated
    /// names, that AnimationPlayerSystem.SetAnimationState applies the same bool/trigger/weight values as the
    /// string-keyed API, and that steady-state apply over an unchanged List is allocation-free.
    /// </summary>
    [Category("Performance")]
    public class SdkAnimatorParamHashPerformanceTest
    {
#if UNITY_EDITOR
        private static readonly string[] CLIPS = { "clipA", "walk-cycle" };

        private GameObject go = null!;
        private Animator animator = null!;
        private World world;
        private AnimationPlayerSystem system = null!;

        [SetUp]
        public void SetUp()
        {
            var controller = new AnimatorController();

            foreach (string clip in CLIPS)
            {
                controller.AddParameter($"{clip}_Enabled", AnimatorControllerParameterType.Bool);
                controller.AddParameter($"{clip}_Loop", AnimatorControllerParameterType.Bool);
                controller.AddParameter($"{clip}_Trigger", AnimatorControllerParameterType.Trigger);

                controller.AddLayer(clip);
                AnimatorControllerLayer layer = controller.layers[controller.layers.Length - 1];
                layer.stateMachine.AddState(clip);
            }

            go = new GameObject("sdk-animator-test");
            animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Update(0f);

            world = World.Create();
            system = new AnimationPlayerSystem(world);
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            World.Destroy(world);
        }

        private static List<SDKAnimationState> BuildStates(bool playing)
        {
            var states = new List<SDKAnimationState>();

            foreach (string clip in CLIPS)
                states.Add(new SDKAnimationState(new PBAnimationState
                {
                    Clip = clip, Playing = playing, Loop = true, Speed = 1, Weight = 1, ShouldReset = false,
                }));

            return states;
        }

        [Test]
        [Performance]
        public void Apply_HashedParams_ZeroAllocAndValueParity()
        {
            List<SDKAnimationState> states = BuildStates(playing: true);

            foreach (SDKAnimationState s in states)
            {
                Assert.AreEqual(Animator.StringToHash($"{s.Clip}_Enabled"), s.EnabledParamHash);
                Assert.AreEqual(Animator.StringToHash($"{s.Clip}_Loop"), s.LoopParamHash);
                Assert.AreEqual(Animator.StringToHash($"{s.Clip}_Trigger"), s.TriggerParamHash);
            }

            system.SetAnimationState(states, animator);

            foreach (string clip in CLIPS)
            {
                int layerIndex = animator.GetLayerIndex(clip);
                Assert.GreaterOrEqual(layerIndex, 0, $"layer for clip {clip} not found");
                Assert.IsTrue(animator.GetBool($"{clip}_Enabled"), $"{clip}_Enabled not set");
                Assert.IsTrue(animator.GetBool($"{clip}_Loop"), $"{clip}_Loop not set");
                Assert.AreEqual(1f, animator.GetLayerWeight(layerIndex), 1e-4f, $"{clip} weight should be 1 while playing");
            }

            system.SetAnimationState(BuildStates(playing: false), animator);
            foreach (string clip in CLIPS)
            {
                int layerIndex = animator.GetLayerIndex(clip);
                Assert.IsFalse(animator.GetBool($"{clip}_Enabled"), $"{clip}_Enabled should be cleared");
                Assert.AreEqual(0f, animator.GetLayerWeight(layerIndex), 1e-4f, $"{clip} weight should be 0 when not playing");
            }

            List<SDKAnimationState> playingStates = BuildStates(playing: true);
            for (int i = 0; i < 3; i++) system.SetAnimationState(playingStates, animator);

            ProfilerRecorder gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
            Measure.Method(() =>
                    {
                        for (int k = 0; k < 1000; k++) system.SetAnimationState(playingStates, animator);
                    })
                   .WarmupCount(3).MeasurementCount(10).GC().Run();
            long gcBytes = gcAlloc.LastValue;
            gcAlloc.Dispose();

            Assert.AreEqual(0, gcBytes, $"SetAnimationState steady state must be allocation-free, allocated {gcBytes} bytes");
        }
#endif
    }
}
