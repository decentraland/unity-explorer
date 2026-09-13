using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// REQ-021: the frustum test's per-instance bounding sphere must enclose
    /// the whole instance-scaled prototype AABB, and honour the profile's
    /// frustum offset. Also the near-distance cull and maximum-LOD clamp the
    /// profile authors.
    ///
    /// The fixture instance is a tall, unevenly scaled box whose
    /// half-diagonal is meaningfully larger than its largest half-extent, so a
    /// sphere sized from the largest half-extent alone culls it while a sphere
    /// sized from the half-diagonal keeps it.
    /// </summary>
    [TestFixture]
    public class CullAndLODJobTests
    {
        private const float BOUND = 10f;

        // Instance scale chosen so the scaled half-extents are (1.5, 5, 0.5):
        //   largest half-extent = 5
        //   half-diagonal       = sqrt(1.5² + 5² + 0.5²) ≈ 5.2440
        private static readonly Vector3 INSTANCE_SCALE = new Vector3(3f, 1f, 1f);
        private const float LARGEST_SCALED_EXTENT = 5f;
        private const float HALF_DIAGONAL = 5.244f;

        // REQ-021 — centre outside the side plane by more than the largest
        // scaled half-extent, but less than the half-diagonal: the box's near
        // corner is still inside, so the instance must survive.
        [Test]
        public void FrustumCull_CentreJustOutsideSidePlane_LargeNonUniformExtents_Survives()
        {
            float overshoot = 0.5f * (LARGEST_SCALED_EXTENT + HALF_DIAGONAL);
            Assert.Greater(overshoot, LARGEST_SCALED_EXTENT, "fixture must defeat a largest-half-extent radius");
            Assert.Less(overshoot, HALF_DIAGONAL, "fixture must be covered by a half-diagonal radius");

            using var fixture = Scaled(BOUND + overshoot, frustumOffset: 0f);
            fixture.Run();

            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_DRAWN),
                "An instance whose scaled AABB still reaches into the frustum must not be culled");
            Assert.AreEqual(0, fixture.Stat(CullAndLODJob.STAT_CULLED_FRUSTUM));
        }

        // REQ-021 — the same test is not vacuous: push the centre past the
        // half-diagonal and the instance really is outside.
        [Test]
        public void FrustumCull_CentreBeyondHalfDiagonal_IsCulled()
        {
            using var fixture = Scaled(BOUND + HALF_DIAGONAL + 0.5f, frustumOffset: 0f);
            fixture.Run();

            Assert.AreEqual(0, fixture.Stat(CullAndLODJob.STAT_DRAWN));
            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_CULLED_FRUSTUM),
                "An instance whose whole AABB is outside the plane must be frustum-culled");
        }

        // REQ-021 — GPUIProfile.frustumOffset widens the sphere; the
        // production tree profile authors 0.1.
        [Test]
        public void FrustumCull_FrustumOffset_WidensTheSphere()
        {
            const float OFFSET = 0.1f;
            float centre = BOUND + HALF_DIAGONAL + (OFFSET * 0.5f);

            using (var widened = Scaled(centre, OFFSET))
            {
                widened.Run();
                Assert.AreEqual(1, widened.Stat(CullAndLODJob.STAT_DRAWN),
                    "With the authored frustum offset applied the instance stays in");
            }

            using (var plain = Scaled(centre, frustumOffset: 0f))
            {
                plain.Run();
                Assert.AreEqual(0, plain.Stat(CullAndLODJob.STAT_DRAWN),
                    "Without it the same instance falls outside");
            }
        }

        // The submitted inverse buffer has to carry the true inverse of the
        // matrix in the same bucket slot, or setupGPUI reconstructs the wrong
        // unity_WorldToObject.
        [Test]
        public void Buckets_CarryTheInverseOfTheMatrixInTheSameSlot()
        {
            using var fixture = Scaled(0f, frustumOffset: 0f);
            fixture.Run();

            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_DRAWN));
            Matrix4x4 matrix = fixture.MatrixAt(BucketLayout.MODE_COLOR_ONLY, BucketLayout.FADE_STEADY, 0, 0);
            float4x4 inverse = fixture.InverseAt(BucketLayout.MODE_COLOR_ONLY, BucketLayout.FADE_STEADY, 0, 0);
            float4x4 roundTrip = math.mul((float4x4)matrix, inverse);
            for (int col = 0; col < 4; col++)
            for (int row = 0; row < 4; row++)
                Assert.AreEqual(col == row ? 1f : 0f, roundTrip[col][row], 1e-4f,
                    $"matrix · inverse must be identity at [{col}][{row}]");
        }

        // GPUIProfile.isFrustumCulling = 0 keeps every instance regardless of
        // the planes.
        [Test]
        public void FrustumCullingOff_KeepsInstancesOutsideThePlanes()
        {
            using var fixture = Scaled(BOUND * 5f, frustumOffset: 0f);
            fixture.job.frustumCulling = false;
            fixture.Run();

            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_DRAWN));
            Assert.AreEqual(0, fixture.Stat(CullAndLODJob.STAT_CULLED_FRUSTUM));
        }

        // GPUIProfile.minCullingDistance hides instances nearer than it.
        [Test]
        public void DistanceCull_NearerThanMinCullingDistance_IsCulled()
        {
            using var fixture = new JobFixture(BOUND,
                Matrix4x4.TRS(new Vector3(2f, 0f, 0f), Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(new Vector3(8f, 0f, 0f), Quaternion.identity, Vector3.one));
            fixture.job.minCullDistance = 4f;
            fixture.Run();

            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_DRAWN), "only the instance beyond the near limit draws");
            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_CULLED_DISTANCE));
            Assert.AreEqual(8f, fixture.MatrixAt(BucketLayout.MODE_COLOR_ONLY, BucketLayout.FADE_STEADY, 0, 0).m03, 1e-4f);
        }

        // GPUIProfile.isDistanceCulling = 0 ignores both distance limits.
        [Test]
        public void DistanceCullingOff_IgnoresBothLimits()
        {
            using var fixture = new JobFixture(BOUND,
                Matrix4x4.TRS(new Vector3(2f, 0f, 0f), Quaternion.identity, Vector3.one));
            fixture.job.minCullDistance = 4f;
            fixture.job.cullDistance = 1f;
            fixture.job.distanceCulling = false;
            fixture.Run();

            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_DRAWN));
            Assert.AreEqual(0, fixture.Stat(CullAndLODJob.STAT_CULLED_DISTANCE));
        }

        // GPUIProfile.maximumLODLevel raises the floor on the LOD index the
        // way QualitySettings.maximumLODLevel does: 1 never draws LOD0.
        [Test]
        public void MaximumLODLevel_ClampsThePickedLevel()
        {
            using var fixture = new JobFixture(100f,
                Matrix4x4.TRS(new Vector3(0f, 0f, 5f), Quaternion.identity, Vector3.one));
            fixture.UseThreeLODs();
            fixture.job.maximumLODLevel = 1;
            fixture.Run();

            Assert.AreEqual(0, fixture.Count(BucketLayout.MODE_COLOR_ONLY, BucketLayout.FADE_STEADY, 0));
            Assert.AreEqual(1, fixture.Count(BucketLayout.MODE_COLOR_ONLY, BucketLayout.FADE_STEADY, 1));
            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_LOD_HISTOGRAM + 1));
        }

        // An orthographic camera sizes instances by the view's half height
        // rather than by distance.
        [Test]
        public void OrthographicCamera_PicksLODFromOrthographicSize()
        {
            using var fixture = new JobFixture(100f,
                Matrix4x4.TRS(new Vector3(0f, 0f, 60f), Quaternion.identity, Vector3.one));
            fixture.UseThreeLODs();

            fixture.job.orthoHalfHeight = 8f; // relativeHeight = 5 / 8 = 0.625 → LOD0 whatever the distance
            fixture.Run();
            Assert.AreEqual(1, fixture.Count(BucketLayout.MODE_COLOR_ONLY, BucketLayout.FADE_STEADY, 0));

            fixture.job.orthoHalfHeight = 40f; // 0.125 → LOD1
            fixture.Run();
            Assert.AreEqual(1, fixture.Count(BucketLayout.MODE_COLOR_ONLY, BucketLayout.FADE_STEADY, 1));
        }

        private static JobFixture Scaled(float centreX, float frustumOffset)
        {
            var fixture = new JobFixture(BOUND,
                Matrix4x4.TRS(new Vector3(centreX, 0f, 0f), Quaternion.identity, INSTANCE_SCALE));
            fixture.job.frustumOffset = frustumOffset;
            return fixture;
        }
    }
}
