using DCL.AvatarRendering.AvatarShape.ComputeShader;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace DCL.AvatarRendering.AvatarShape.Tests.PlayMode
{
    /// <summary>
    /// Guards the optimization in <see cref="BoneMatrixCalculationJob"/>: instead of recomputing all
    /// <c>MAX_BONE_COUNT</c> (256) matrices per avatar every frame, the job now produces only the first
    /// <c>PerAvatarBoneCount[avatarIdx]</c> matrices — the authoritative count the consumer uploads
    /// (<c>AvatarCustomSkinningComponent.ComputeSkinning</c> does
    /// <c>bones.SetData(bonesResult, validIndex * MAX_BONE_COUNT, 0, BoneCount)</c>).
    ///
    /// <para>
    /// The regression this falsifies (per the review of the first attempt): bounding the loop by a
    /// SMALLER, re-derived count than the upload count. There, the gathered/filtered spring-bone count
    /// (<c>actualCount</c>) can be strictly less than the uploaded <c>BoneCount</c> whenever a wearable's
    /// spring chain trips the root-guard, so slots <c>[actualCount, BoneCount)</c> were left uncomputed
    /// and <c>ComputeSkinning</c> uploaded stale/garbage matrices for spring-bone-weighted vertices.
    /// This test models exactly that hazard: it feeds <c>PerAvatarBoneCount</c> (the upload count)
    /// STRICTLY GREATER than a filtered baseline (<c>BASE_BONE_COUNT</c>) and asserts the whole uploaded
    /// range — including the extended spring-bone tail past the baseline — is computed with no stale slot.
    /// </para>
    ///
    /// <para>
    /// Symmetrically it asserts the optimization is real: the padding slots
    /// <c>[BoneCount, MAX_BONE_COUNT)</c> (never uploaded) are left untouched, so a revert to
    /// full-stride recompute is caught too.
    /// </para>
    ///
    /// Execute() is invoked directly per index (single-threaded) so the per-avatar bounds logic is
    /// exercised deterministically without the Burst/Jobs scheduler.
    /// </summary>
    [Category("Performance")]
    public class BoneMatrixCalculationJobPerformanceTest
    {
        private const int STRIDE = ComputeShaderConstants.MAX_BONE_COUNT;   // 256
        private const int FILTERED_BASELINE = ComputeShaderConstants.BASE_BONE_COUNT; // 62

        // A float4x4 that a correctly-computed slot can never equal (all-NaN), so "computed" vs
        // "left untouched" is unambiguous.
        private static readonly float4x4 SENTINEL = new float4x4(
            new float4(float.NaN), new float4(float.NaN), new float4(float.NaN), new float4(float.NaN));

        [Test]
        [Performance]
        [TestCase(32)]
        [TestCase(64)]
        public void ProducesExactlyTheUploadedRange_NoStaleTail_AndSkipsPadding(int avatarCount)
        {
            var boneWorld = new NativeArray<float4x4>(avatarCount * STRIDE, Allocator.Persistent);
            var avatarTransform = new NativeArray<float4x4>(avatarCount, Allocator.Persistent);
            var updateAvatar = new NativeArray<bool>(avatarCount, Allocator.Persistent);
            var perAvatarBoneCount = new NativeArray<int>(avatarCount, Allocator.Persistent);

            // Deterministic, distinct, non-identity inputs so avatarMatrix * boneWorld is observable.
            for (int a = 0; a < avatarCount; a++)
            {
                avatarTransform[a] = float4x4.Translate(new float3(a + 1, 0f, 0f));

                // Every second avatar is "released" (UpdateAvatar == false) — its whole block must stay
                // untouched, matching production where the calculation job skips pooled slots.
                updateAvatar[a] = (a % 2) == 0;

                // Upload count = filtered baseline + a spring-bone tail (1..16). This is STRICTLY GREATER
                // than FILTERED_BASELINE, reproducing BoneCount > actualCount, and STRICTLY LESS than the
                // stride, so the optimization has a real padding tail to skip.
                perAvatarBoneCount[a] = FILTERED_BASELINE + 1 + (a % 16);

                for (int b = 0; b < STRIDE; b++)
                    boneWorld[(a * STRIDE) + b] = float4x4.Translate(new float3(0f, (a * STRIDE) + b + 1, 0f));
            }

            var job = new BoneMatrixCalculationJob(STRIDE, avatarCount * STRIDE, boneWorld)
            {
                AvatarTransform = avatarTransform,
                UpdateAvatar = updateAvatar,
                PerAvatarBoneCount = perAvatarBoneCount,
            };

            NativeArray<float4x4> result = job.BonesMatricesResult;

            void RunOnce()
            {
                for (int i = 0; i < result.Length; i++)
                    result[i] = SENTINEL;

                for (int a = 0; a < avatarCount; a++)
                    job.Execute(a);
            }

            RunOnce();

            int computedSlots = 0;

            for (int a = 0; a < avatarCount; a++)
            {
                int offset = a * STRIDE;
                int uploaded = perAvatarBoneCount[a];

                if (!updateAvatar[a])
                {
                    // Skipped avatar: entire block untouched.
                    for (int b = 0; b < STRIDE; b++)
                        Assert.IsTrue(IsSentinel(result[offset + b]),
                            $"Released avatar {a} slot {b} was written but should have been skipped.");

                    continue;
                }

                // Correctness invariant (falsifies the first attempt): the FULL uploaded range — including
                // the spring-bone tail past the filtered baseline — is computed, no stale slot.
                for (int b = 0; b < uploaded; b++)
                {
                    float4x4 expected = math.mul(avatarTransform[a], boneWorld[offset + b]);
                    Assert.IsFalse(IsSentinel(result[offset + b]),
                        $"Avatar {a} slot {b} in the uploaded range [0,{uploaded}) was left stale — " +
                        $"ComputeSkinning would upload uninitialised matrices here.");
                    Assert.IsTrue(Equalish(result[offset + b], expected),
                        $"Avatar {a} slot {b} computed the wrong matrix.");
                }

                // Optimization invariant (falsifies a revert to full-stride compute): the padding slots
                // that are never uploaded are left untouched.
                for (int b = uploaded; b < STRIDE; b++)
                    Assert.IsTrue(IsSentinel(result[offset + b]),
                        $"Avatar {a} padding slot {b} (>= BoneCount {uploaded}) was recomputed — the " +
                        "per-avatar bound was not applied.");

                computedSlots += uploaded;
            }

            // Work is strictly less than the old full-stride pass over every active avatar.
            int activeAvatars = 0;
            for (int a = 0; a < avatarCount; a++)
                if (updateAvatar[a]) activeAvatars++;

            Assert.Less(computedSlots, activeAvatars * STRIDE,
                "Expected fewer matrix multiplies than the full-stride baseline.");

            Measure.Method(RunOnce)
                   .WarmupCount(5)
                   .MeasurementCount(20)
                   .Run();

            job.Dispose();
            boneWorld.Dispose();
            avatarTransform.Dispose();
            updateAvatar.Dispose();
            perAvatarBoneCount.Dispose();
        }

        private static bool IsSentinel(float4x4 m) =>
            math.all(math.isnan(m.c0)) && math.all(math.isnan(m.c1)) &&
            math.all(math.isnan(m.c2)) && math.all(math.isnan(m.c3));

        private static bool Equalish(float4x4 a, float4x4 b)
        {
            const float EPS = 1e-4f;
            return math.all(math.abs(a.c0 - b.c0) <= EPS) &&
                   math.all(math.abs(a.c1 - b.c1) <= EPS) &&
                   math.all(math.abs(a.c2 - b.c2) <= EPS) &&
                   math.all(math.abs(a.c3 - b.c3) <= EPS);
        }
    }
}
