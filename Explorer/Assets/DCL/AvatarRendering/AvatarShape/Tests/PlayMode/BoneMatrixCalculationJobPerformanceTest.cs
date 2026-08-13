using DCL.AvatarRendering.AvatarShape.ComputeShader;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace DCL.AvatarRendering.AvatarShape.Tests.PlayMode
{
    /// <summary>
    /// <see cref="BoneMatrixCalculationJob"/> must compute exactly the matrices
    /// <c>AvatarCustomSkinningComponent.ComputeSkinning</c> uploads — the first
    /// <c>PerAvatarBoneCount[avatarIdx]</c> slots of each avatar's <c>MAX_BONE_COUNT</c> (256) stride
    /// (<c>bones.SetData(bonesResult, validIndex * MAX_BONE_COUNT, 0, BoneCount)</c>) — not a smaller,
    /// independently re-derived count.
    ///
    /// <para>
    /// A wearable's spring chain can trip the root-guard, making the gathered/filtered spring-bone
    /// count (<c>actualCount</c>) strictly less than the uploaded <c>BoneCount</c>; bounding the loop
    /// by that smaller count would leave slots <c>[actualCount, BoneCount)</c> uncomputed, and
    /// <c>ComputeSkinning</c> would then upload stale/garbage matrices for spring-bone-weighted
    /// vertices. This test feeds <c>PerAvatarBoneCount</c> (the upload count) strictly greater than a
    /// filtered baseline (<c>BASE_BONE_COUNT</c>) and asserts the whole uploaded range — including the
    /// extended spring-bone tail past the baseline — is computed with no stale slot.
    /// </para>
    ///
    /// <para>
    /// It also asserts the padding slots <c>[BoneCount, MAX_BONE_COUNT)</c> (never uploaded) stay
    /// untouched, catching a regression to full-stride recompute.
    /// </para>
    ///
    /// Execute() is invoked directly per index (single-threaded) so the per-avatar bounds logic is
    /// exercised deterministically without the Burst/Jobs scheduler.
    /// </summary>
    [Category("Performance")]
    public class BoneMatrixCalculationJobPerformanceTest
    {
        private const int STRIDE = ComputeShaderConstants.MAX_BONE_COUNT;
        private const int FILTERED_BASELINE = ComputeShaderConstants.BASE_BONE_COUNT;

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

            for (int a = 0; a < avatarCount; a++)
            {
                avatarTransform[a] = float4x4.Translate(new float3(a + 1, 0f, 0f));

                updateAvatar[a] = (a % 2) == 0;

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
                    for (int b = 0; b < STRIDE; b++)
                        Assert.IsTrue(IsSentinel(result[offset + b]),
                            $"Released avatar {a} slot {b} was written but should have been skipped.");

                    continue;
                }

                for (int b = 0; b < uploaded; b++)
                {
                    float4x4 expected = math.mul(avatarTransform[a], boneWorld[offset + b]);
                    Assert.IsFalse(IsSentinel(result[offset + b]),
                        $"Avatar {a} slot {b} in the uploaded range [0,{uploaded}) was left stale — " +
                        $"ComputeSkinning would upload uninitialised matrices here.");
                    Assert.IsTrue(Equalish(result[offset + b], expected),
                        $"Avatar {a} slot {b} computed the wrong matrix.");
                }

                for (int b = uploaded; b < STRIDE; b++)
                    Assert.IsTrue(IsSentinel(result[offset + b]),
                        $"Avatar {a} padding slot {b} (>= BoneCount {uploaded}) was recomputed — the " +
                        "per-avatar bound was not applied.");

                computedSlots += uploaded;
            }

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
