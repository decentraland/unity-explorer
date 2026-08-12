using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using Google.Protobuf.Collections;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Tests
{
    /// <summary>
    /// Allocation guards for tween SETUP hot paths. The TweenerPool recycles tweeners so that
    /// (re)authoring a tween is GC-free once DOTween's pools are warm. Two leaks defeat that: (1) the
    /// interface-dispatched foreach over the protobuf RepeatedField in SequenceTweener.Initialize
    /// allocates a boxed enumerator per setup; (2) the per-call closure in each continuous tweener's
    /// CreateContinuousTweener allocates a display class + delegate per setup.
    ///
    /// Each test warms DOTween's pool, then asserts a burst of steady-state setups allocates zero managed
    /// bytes on this thread. GC.GetAllocatedBytesForCurrentThread() reads 0 even on known-boxing code in
    /// some headless lanes, so each measured assertion first runs a positive-control probe (a plain array
    /// allocation) and reports Inconclusive instead of passing vacuously when the API is not measuring.
    /// The enumerator-elimination property is additionally verified structurally (parameter-type
    /// reflection) so it holds in every lane.
    /// </summary>
    [TestFixture]
    public class TweenSetupAllocationShould
    {
        private const int WARMUP = 40;
        private const int ITERATIONS = 50;

        [Test]
        public void SequenceInitializeDoesNotAllocateEnumerator()
        {
            var go = new GameObject("seq-alloc-target");

            try
            {
                Transform transform = go.transform;
                PBTween firstTween = MoveTween();

                // Empty additional list: isolates the enumerator allocation from real per-step DOTween work.
                var additional = new RepeatedField<PBTween>();

                var tweener = new SequenceTweener();
                Action setup = () => tweener.Initialize(firstTween, additional, null, transform, null);

                AssertAllocationFree(setup, "SequenceTweener.Initialize");

                tweener.Kill(true);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Vector3ContinuousSetupDoesNotAllocateClosure()
        {
            var tweener = new Vector3Tweener();
            Action setup = () => tweener.InitializeContinuous(Vector3.zero, Vector3.up, 1f);

            AssertAllocationFree(setup, "Vector3Tweener.InitializeContinuous");

            tweener.Kill(true);
        }

        [Test]
        public void Vector2ContinuousSetupDoesNotAllocateClosure()
        {
            var tweener = new Vector2Tweener();
            Action setup = () => tweener.InitializeContinuous(Vector2.zero, Vector2.right, 1f);

            AssertAllocationFree(setup, "Vector2Tweener.InitializeContinuous");

            tweener.Kill(true);
        }

        [Test]
        public void QuaternionContinuousSetupDoesNotAllocateClosure()
        {
            var tweener = new QuaternionTweener();
            Quaternion direction = Quaternion.AngleAxis(90f, Vector3.up);
            Action setup = () => tweener.InitializeContinuous(Quaternion.identity, direction, 90f);

            AssertAllocationFree(setup, "QuaternionTweener.InitializeContinuous");

            tweener.Kill(true);
        }

        // Structural guard: the non-boxing property is fully determined by the STATIC type of the
        // additional-tweens parameter (concrete RepeatedField<PBTween> binds a struct enumerator/indexer;
        // IEnumerable<PBTween> boxes a heap enumerator per setup), so reflection verifies it in every lane.
        [Test]
        public void TypeSequenceInitializeAdditionalTweensAsConcreteRepeatedField()
        {
            MethodInfo initialize = typeof(SequenceTweener).GetMethod(nameof(SequenceTweener.Initialize))!;
            Type additionalTweensType = initialize.GetParameters()[1].ParameterType;

            Assert.That(additionalTweensType, Is.EqualTo(typeof(RepeatedField<PBTween>)),
                "SequenceTweener.Initialize must take the concrete RepeatedField<PBTween> so iteration binds the non-boxing struct enumerator/indexer.");

            Assert.That(additionalTweensType, Is.Not.EqualTo(typeof(IEnumerable<PBTween>)),
                "An IEnumerable<PBTween>-typed parameter boxes a heap enumerator on every sequence setup.");
        }

        private static void AssertAllocationFree(Action action, string label)
        {
            // Warm DOTween's tween pool + JIT so only steady-state setup cost is measured.
            for (int i = 0; i < WARMUP; i++)
                action();

            // Positive control: in some headless lanes GetAllocatedBytesForCurrentThread reads 0 even for
            // a plain heap allocation, which would make the == 0 assertion below pass vacuously.
            long probeBefore = GC.GetAllocatedBytesForCurrentThread();
            byte[] probe = new byte[256];
            long probeDelta = GC.GetAllocatedBytesForCurrentThread() - probeBefore;
            GC.KeepAlive(probe);

            if (probeDelta == 0)
                Assert.Inconclusive($"{label}: managed allocation is not measurable in this lane " +
                    $"(positive-control {probe.Length}-byte array allocation read 0 bytes); the structural signature test still guards the property.");

            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < ITERATIONS; i++)
                action();

            long delta = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(delta, Is.EqualTo(0),
                $"{label}: {ITERATIONS} steady-state setups allocated {delta} bytes (~{delta / ITERATIONS} B/call). " +
                "The pooled tweener must re-author allocation-free; a per-setup closure or boxed enumerator reintroduces this.");
        }

        private static PBTween MoveTween() =>
            new ()
            {
                Duration = 500,
                EasingFunction = EasingFunction.EfLinear,
                Playing = true,
                Move = new Move
                {
                    Start = new Decentraland.Common.Vector3 { X = 0, Y = 0, Z = 0 },
                    End = new Decentraland.Common.Vector3 { X = 1, Y = 0, Z = 0 },
                },
            };
    }
}
