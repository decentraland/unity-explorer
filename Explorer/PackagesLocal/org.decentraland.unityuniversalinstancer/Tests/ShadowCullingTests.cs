using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// Shadow-caster classification the production profile authors:
    /// isShadowDistanceCulling + minShadowCullingDistance stop casters at
    /// the shadow distance while keeping near off-screen casters alive,
    /// isShadowFrustumCulling / shadowFrustumOffset gate the far ones, and
    /// shadowLODMap picks the caster's LOD.
    /// </summary>
    [TestFixture]
    public class ShadowCullingTests
    {
        private const float BOUND = 10f;
        private const int BOTH = BucketLayout.MODE_BOTH;
        private const int COLOR = BucketLayout.MODE_COLOR_ONLY;
        private const int SHADOW = BucketLayout.MODE_SHADOW_ONLY;
        private const int STEADY = BucketLayout.FADE_STEADY;

        // ---- Job level ----

        [Test]
        public void ShadowDistance_InstanceBeyondIt_DrawsColourOnly()
        {
            using var fixture = Caster(new Vector3(0f, 0f, 8f));
            fixture.job.shadowDistanceCulling = true;
            fixture.job.shadowDistance = 5f;
            fixture.Run();

            Assert.AreEqual(1, fixture.Count(COLOR, STEADY, 0), "beyond the shadow distance the instance is colour only");
            Assert.AreEqual(0, fixture.Count(BOTH, STEADY, 0));
            Assert.AreEqual(0, fixture.Count(SHADOW, STEADY, 0));
            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_DRAWN));
        }

        [Test]
        public void ShadowDistance_InstanceWithinIt_DrawsColourAndCastsShadows()
        {
            using var fixture = Caster(new Vector3(0f, 0f, 3f));
            fixture.job.shadowDistanceCulling = true;
            fixture.job.shadowDistance = 5f;
            fixture.Run();

            Assert.AreEqual(1, fixture.Count(BOTH, STEADY, 0));
            Assert.AreEqual(0, fixture.Count(COLOR, STEADY, 0));
            Assert.AreEqual(1, fixture.TotalEntries, "one draw covers colour and shadow");
        }

        [Test]
        public void ShadowDistanceCullingOff_CastsOutToTheCullDistance()
        {
            using var fixture = Caster(new Vector3(0f, 0f, 8f));
            fixture.job.shadowDistanceCulling = false;
            fixture.job.shadowDistance = 5f;
            fixture.Run();

            Assert.AreEqual(1, fixture.Count(BOTH, STEADY, 0));
        }

        [Test]
        public void OffFrustumCaster_WithinMinShadowCullingDistance_IsShadowOnly()
        {
            using var fixture = Caster(new Vector3(30f, 0f, 0f));
            fixture.job.shadowDistanceCulling = true;
            fixture.job.shadowDistance = 100f;
            fixture.job.shadowFrustumCulling = true;
            fixture.job.minShadowCullingDistance = 40f;
            fixture.Run();

            Assert.AreEqual(1, fixture.Count(SHADOW, STEADY, 0), "a near caster outside the frustum still casts into it");
            Assert.AreEqual(0, fixture.Stat(CullAndLODJob.STAT_DRAWN));
            Assert.AreEqual(0, fixture.Stat(CullAndLODJob.STAT_CULLED_FRUSTUM));
            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_SHADOW_ONLY));
            Assert.AreEqual(30f, fixture.MatrixAt(SHADOW, STEADY, 0, 0).m03, 1e-4f);
        }

        [Test]
        public void OffFrustumCaster_BeyondMinDistance_ShadowFrustumCullingOn_IsCulled()
        {
            using var fixture = Caster(new Vector3(30f, 0f, 0f));
            fixture.job.shadowDistanceCulling = true;
            fixture.job.shadowDistance = 100f;
            fixture.job.shadowFrustumCulling = true;
            fixture.job.minShadowCullingDistance = 10f;
            fixture.job.shadowFrustumOffset = 0f;
            fixture.Run();

            Assert.AreEqual(0, fixture.TotalEntries);
            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_CULLED_FRUSTUM));
        }

        [Test]
        public void OffFrustumCaster_ShadowFrustumOffset_RescuesIt()
        {
            using var fixture = Caster(new Vector3(30f, 0f, 0f));
            fixture.job.shadowDistanceCulling = true;
            fixture.job.shadowDistance = 100f;
            fixture.job.shadowFrustumCulling = true;
            fixture.job.minShadowCullingDistance = 10f;
            fixture.job.shadowFrustumOffset = 30f;
            fixture.Run();

            Assert.AreEqual(1, fixture.Count(SHADOW, STEADY, 0), "the widened shadow frustum reaches the caster");
            Assert.AreEqual(0, fixture.Stat(CullAndLODJob.STAT_DRAWN), "the colour draw stays frustum-culled");
        }

        [Test]
        public void OffFrustumCaster_ShadowFrustumCullingOff_KeepsEveryCasterInShadowDistance()
        {
            using var fixture = Caster(new Vector3(30f, 0f, 0f));
            fixture.job.shadowDistanceCulling = true;
            fixture.job.shadowDistance = 100f;
            fixture.job.shadowFrustumCulling = false;
            fixture.job.minShadowCullingDistance = 0f;
            fixture.Run();

            Assert.AreEqual(1, fixture.Count(SHADOW, STEADY, 0));
        }

        [Test]
        public void OffFrustumCaster_BeyondShadowDistance_IsCulled()
        {
            using var fixture = Caster(new Vector3(30f, 0f, 0f));
            fixture.job.shadowDistanceCulling = true;
            fixture.job.shadowDistance = 20f;
            fixture.job.shadowFrustumCulling = false;
            fixture.Run();

            Assert.AreEqual(0, fixture.TotalEntries);
            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_CULLED_FRUSTUM));
        }

        [Test]
        public void ShadowLODMap_RemapsTheCasterLevel()
        {
            using var fixture = Caster(new Vector3(0f, 0f, 5f));
            fixture.UseThreeLODs();
            fixture.job.shadowLODMap = new int4(1, 1, 2, 3);
            fixture.Run();

            Assert.AreEqual(1, fixture.Count(COLOR, STEADY, 0), "colour draws at the picked LOD");
            Assert.AreEqual(1, fixture.Count(SHADOW, STEADY, 1), "the shadow draws at the mapped LOD");
            Assert.AreEqual(0, fixture.Count(BOTH, STEADY, 0));
            Assert.AreEqual(2, fixture.TotalEntries);
        }

        [Test]
        public void ShadowLODMap_BeyondLODCount_ClampsToTheLastLevel()
        {
            using var fixture = Caster(new Vector3(0f, 0f, 5f));
            fixture.UseThreeLODs();
            fixture.job.shadowLODMap = new int4(7, 7, 7, 7);
            fixture.Run();

            Assert.AreEqual(1, fixture.Count(SHADOW, STEADY, 2));
        }

        [Test]
        public void CastShadowsOff_NeverEmitsCasters()
        {
            using var fixture = Caster(new Vector3(0f, 0f, 3f));
            fixture.job.castShadows = false;
            fixture.Run();

            Assert.AreEqual(1, fixture.Count(COLOR, STEADY, 0));
            Assert.AreEqual(0, fixture.Count(BOTH, STEADY, 0));
        }

        [Test]
        public void CastShadowsOff_OffFrustumInstance_IsFrustumCulled()
        {
            using var fixture = Caster(new Vector3(30f, 0f, 0f));
            fixture.job.castShadows = false;
            fixture.job.minShadowCullingDistance = 100f;
            fixture.Run();

            Assert.AreEqual(0, fixture.TotalEntries);
            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_CULLED_FRUSTUM));
        }

        // ---- Service level ----

        [Test]
        public void Service_ShadowDistance_SplitsOneLODIntoCasterAndColourOnlyDraws()
        {
            using var scene = new ServiceScene();
            scene.profile.isShadowCasting = 1;
            scene.profile.isShadowDistanceCulling = 1;
            scene.profile.customShadowDistance = 15f;
            scene.profile.isLODCrossFade = 0;
            int key = scene.Register();

            // Camera at z = -10: distances 12 and 17, both LOD0.
            scene.Upload(key, new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(0f, 0f, 2f), Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(new Vector3(0f, 0f, 7f), Quaternion.identity, Vector3.one),
            });
            GPUICoreAPI.TickForTests(scene.camera);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.AreEqual(2, stats.totalInstancesDrawn);
            Assert.AreEqual(2, stats.lodHistogram[0]);
            Assert.AreEqual(2, stats.drawCallCount, "the same LOD slot draws once with shadows and once without");
            Assert.AreEqual(2, stats.bucketEntries);
        }

        [Test]
        public void Service_NearCasterBehindTheCamera_ProducesAShadowOnlyDraw()
        {
            using var scene = new ServiceScene();
            scene.profile.isShadowCasting = 1;
            scene.profile.isShadowDistanceCulling = 1;
            scene.profile.customShadowDistance = 50f;
            scene.profile.minShadowCullingDistance = 40f;
            scene.profile.isShadowFrustumCulling = 1;
            scene.profile.isLODCrossFade = 0;
            int key = scene.Register();

            // 30 units behind the camera: the 7.07 bounding sphere is fully
            // past the near plane, so the colour draw is frustum-culled.
            scene.Upload(key, new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(0f, 0f, -40f), Quaternion.identity, Vector3.one),
            });
            GPUICoreAPI.TickForTests(scene.camera);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.AreEqual(0, stats.totalInstancesDrawn);
            Assert.AreEqual(0, stats.culledByFrustum);
            Assert.AreEqual(1, stats.shadowOnlyInstances);
            Assert.AreEqual(1, stats.drawCallCount);
        }

        [Test]
        public void Service_ProfileFlush_PropagatesShadowAndCrossFadeFields()
        {
            using var scene = new ServiceScene();
            scene.profile.isShadowCasting = 1;
            scene.profile.isLODCrossFade = 1;
            scene.profile.isAnimateCrossFade = 1;
            int key = scene.Register();

            scene.profile.isShadowCasting = 0;
            scene.profile.isLODCrossFade = 0;
            scene.profile.customShadowDistance = 33f;

            ProfileSnapshot before = GPUICoreAPI.GetSettingsForTests(key);
            Assert.IsTrue(before.castShadows, "mutation without a flush must not propagate");
            Assert.AreEqual(CullAndLODJob.CROSSFADE_ANIMATED, before.crossFadeMode);

            scene.profile.SetParameterBufferData();

            ProfileSnapshot after = GPUICoreAPI.GetSettingsForTests(key);
            Assert.IsFalse(after.castShadows);
            Assert.AreEqual(CullAndLODJob.CROSSFADE_NONE, after.crossFadeMode);
            Assert.AreEqual(33f, after.customShadowDistance);
        }

        private static JobFixture Caster(Vector3 position)
        {
            var fixture = new JobFixture(BOUND, Matrix4x4.TRS(position, Quaternion.identity, Vector3.one));
            fixture.job.castShadows = true;
            return fixture;
        }
    }
}
