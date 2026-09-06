using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// LOD cross-fade the production tree profile authors (isLODCrossFade,
    /// isAnimateCrossFade, lodCrossFadeTransitionWidth, lodCrossFadeAnimateSpeed).
    /// The LOD fading out is emitted at +fade and the LOD fading in at
    /// -fade, the sign convention URP's LODFadeCrossFade dithers by, so the
    /// two draws cover complementary pixels.
    ///
    /// Three LODs with thresholds 0.5 / 0.1 / 0.001 and relativeHeight ≈
    /// 8.66 / distance: the LOD0 band (width 0.1 of the 0.5..1.0 range) is
    /// 0.5 ≤ h < 0.55, i.e. 15.75 < d ≤ 17.32.
    /// </summary>
    [TestFixture]
    public class LODCrossFadeTests
    {
        private const float BOUND = 3000f;
        private const int COLOR = BucketLayout.MODE_COLOR_ONLY;
        private const int STEADY = BucketLayout.FADE_STEADY;
        private const int FADING = BucketLayout.FADE_CROSSFADING;

        // ---- Spatial (isAnimateCrossFade = 0) ----

        [Test]
        public void Spatial_InsideTheBand_EmitsFadeOutAndFadeInEntries()
        {
            using var fixture = Instance(16.5f, CullAndLODJob.CROSSFADE_SPATIAL);
            fixture.Run();

            Assert.AreEqual(1, fixture.Count(COLOR, FADING, 0), "LOD0 fades out");
            Assert.AreEqual(1, fixture.Count(COLOR, FADING, 1), "LOD1 fades in");
            Assert.AreEqual(0, fixture.Count(COLOR, STEADY, 0));
            Assert.AreEqual(0, fixture.Count(COLOR, STEADY, 1));
            float fadeOut = fixture.FadeAt(COLOR, FADING, 0, 0);
            float fadeIn = fixture.FadeAt(COLOR, FADING, 1, 0);
            Assert.AreEqual(0.5f, fadeOut, 0.02f, "half way through the band");
            Assert.AreEqual(-fadeOut, fadeIn, 1e-6f, "the incoming LOD carries the negated factor");
            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_CROSSFADING));
            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_DRAWN), "one instance however many draws it needs");
            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_LOD_HISTOGRAM + 0));
            Assert.AreEqual(2, fixture.TotalEntries);
        }

        [Test]
        public void Spatial_OutsideTheBand_IsSteady()
        {
            using var fixture = Instance(12f, CullAndLODJob.CROSSFADE_SPATIAL);
            fixture.Run();

            Assert.AreEqual(1, fixture.Count(COLOR, STEADY, 0));
            Assert.AreEqual(1, fixture.TotalEntries);
            Assert.AreEqual(1f, fixture.FadeAt(COLOR, STEADY, 0, 0));
            Assert.AreEqual(0, fixture.Stat(CullAndLODJob.STAT_CROSSFADING));
        }

        [Test]
        public void Spatial_FadeFactor_FollowsThePositionInTheBand()
        {
            using var near = Instance(15.9f, CullAndLODJob.CROSSFADE_SPATIAL);
            near.Run();
            using var far = Instance(17.2f, CullAndLODJob.CROSSFADE_SPATIAL);
            far.Run();

            float nearFade = near.FadeAt(COLOR, FADING, 0, 0);
            float farFade = far.FadeAt(COLOR, FADING, 0, 0);
            Assert.Greater(nearFade, farFade, "the outgoing LOD keeps more pixels closer to the camera");
            Assert.Greater(farFade, 0f);
            Assert.Less(nearFade, 1f);
        }

        [Test]
        public void Spatial_LastLOD_FadesToNothing()
        {
            // LOD2 band: 0.001 ≤ h < 0.0109, i.e. 794 < d ≤ 8660.
            using var fixture = Instance(1000f, CullAndLODJob.CROSSFADE_SPATIAL);
            fixture.Run();

            Assert.AreEqual(1, fixture.Count(COLOR, FADING, 2));
            Assert.AreEqual(1, fixture.TotalEntries, "nothing fades in behind the last LOD");
            float fade = fixture.FadeAt(COLOR, FADING, 2, 0);
            Assert.Greater(fade, 0f);
            Assert.Less(fade, 1f);
            Assert.AreEqual(InstanceCullResult.NONE, fixture.ResultAt(0).lodIn);
        }

        [Test]
        public void Spatial_WithShadows_FadesBothDraws()
        {
            using var fixture = Instance(16.5f, CullAndLODJob.CROSSFADE_SPATIAL);
            fixture.job.castShadows = true;
            fixture.Run();

            Assert.AreEqual(1, fixture.Count(BucketLayout.MODE_BOTH, FADING, 0));
            Assert.AreEqual(1, fixture.Count(BucketLayout.MODE_BOTH, FADING, 1));
            Assert.AreEqual(2, fixture.TotalEntries);
        }

        [Test]
        public void CrossFadeOff_IsBinary()
        {
            using var fixture = Instance(16.5f, CullAndLODJob.CROSSFADE_NONE);
            fixture.Run();

            Assert.AreEqual(1, fixture.Count(COLOR, STEADY, 0));
            Assert.AreEqual(1, fixture.TotalEntries);
        }

        // ---- Animated (isAnimateCrossFade = 1) ----

        [Test]
        public void Animated_FirstFrame_SnapsToThePickedLOD()
        {
            using var fixture = Instance(12f, CullAndLODJob.CROSSFADE_ANIMATED);
            fixture.Run(deltaTime: 0.1f);

            Assert.AreEqual(1, fixture.Count(COLOR, STEADY, 0));
            Assert.AreEqual(1, fixture.TotalEntries);
            LODFadeState state = fixture.FadeStateAt(0);
            Assert.AreEqual(0, state.from);
            Assert.AreEqual(0, state.to);
            Assert.AreEqual(1f, state.progress);
        }

        [Test]
        public void Animated_LODChange_DissolvesOverOneOverSpeedSeconds()
        {
            using var fixture = Instance(12f, CullAndLODJob.CROSSFADE_ANIMATED);
            fixture.job.crossFadeAnimateSpeed = 4f;
            fixture.Run(deltaTime: 0.1f);

            // d = 40 → h ≈ 0.2165 → LOD1.
            fixture.SetInstance(0, At(40f));

            fixture.Run(deltaTime: 0.1f);
            Assert.AreEqual(1, fixture.Count(COLOR, FADING, 0), "LOD0 fades out");
            Assert.AreEqual(1, fixture.Count(COLOR, FADING, 1), "LOD1 fades in");
            Assert.AreEqual(0.6f, fixture.FadeAt(COLOR, FADING, 0, 0), 1e-4f, "0.4 of the dissolve done");
            Assert.AreEqual(-0.6f, fixture.FadeAt(COLOR, FADING, 1, 0), 1e-4f);

            fixture.Run(deltaTime: 0.1f);
            Assert.AreEqual(0.2f, fixture.FadeAt(COLOR, FADING, 0, 0), 1e-4f, "0.8 of the dissolve done");

            fixture.Run(deltaTime: 0.1f);
            Assert.AreEqual(1, fixture.Count(COLOR, STEADY, 1), "settled on LOD1");
            Assert.AreEqual(1, fixture.TotalEntries);
            Assert.AreEqual(0, fixture.Stat(CullAndLODJob.STAT_CROSSFADING));
        }

        [Test]
        public void Animated_TargetRevertsMidDissolve_ReversesIt()
        {
            using var fixture = Instance(12f, CullAndLODJob.CROSSFADE_ANIMATED);
            fixture.job.crossFadeAnimateSpeed = 4f;
            fixture.Run(deltaTime: 0.1f);

            fixture.SetInstance(0, At(40f));
            fixture.Run(deltaTime: 0.1f); // LOD0 → LOD1, 0.4 done

            fixture.SetInstance(0, At(12f));
            fixture.Run(deltaTime: 0.05f); // reversed: LOD1 → LOD0 from 0.6, now 0.8

            Assert.AreEqual(1, fixture.Count(COLOR, FADING, 1), "LOD1 is the one fading out");
            Assert.AreEqual(1, fixture.Count(COLOR, FADING, 0), "LOD0 fades back in");
            Assert.AreEqual(0.2f, fixture.FadeAt(COLOR, FADING, 1, 0), 1e-4f);
            Assert.AreEqual(-0.2f, fixture.FadeAt(COLOR, FADING, 0, 0), 1e-4f);

            fixture.Run(deltaTime: 0.1f);
            Assert.AreEqual(1, fixture.Count(COLOR, STEADY, 0), "settled back on LOD0");
        }

        [Test]
        public void Animated_NewTargetMidDissolve_RestartsFromTheLODBeingEntered()
        {
            using var fixture = Instance(12f, CullAndLODJob.CROSSFADE_ANIMATED);
            fixture.job.crossFadeAnimateSpeed = 4f;
            fixture.Run(deltaTime: 0.1f);

            fixture.SetInstance(0, At(40f));
            fixture.Run(deltaTime: 0.1f); // LOD0 → LOD1

            fixture.SetInstance(0, At(400f)); // h ≈ 0.02 → LOD2
            fixture.Run(deltaTime: 0.1f);

            Assert.AreEqual(1, fixture.Count(COLOR, FADING, 1), "LOD1 fades out");
            Assert.AreEqual(1, fixture.Count(COLOR, FADING, 2), "LOD2 fades in");
            Assert.AreEqual(0.6f, fixture.FadeAt(COLOR, FADING, 1, 0), 1e-4f, "a fresh dissolve, 0.4 done");
        }

        [Test]
        public void Animated_ZeroDeltaTime_HoldsTheDissolve()
        {
            using var fixture = Instance(12f, CullAndLODJob.CROSSFADE_ANIMATED);
            fixture.job.crossFadeAnimateSpeed = 4f;
            fixture.Run(deltaTime: 0.1f);
            fixture.SetInstance(0, At(40f));
            fixture.Run(deltaTime: 0.1f);

            fixture.Run(deltaTime: 0f);
            Assert.AreEqual(0.6f, fixture.FadeAt(COLOR, FADING, 0, 0), 1e-4f);
        }

        [Test]
        public void Animated_CulledInstance_ForgetsItsState()
        {
            using var fixture = Instance(12f, CullAndLODJob.CROSSFADE_ANIMATED);
            fixture.job.crossFadeAnimateSpeed = 4f;
            fixture.job.cullDistance = 100f;
            fixture.Run(deltaTime: 0.1f);
            fixture.SetInstance(0, At(40f));
            fixture.Run(deltaTime: 0.1f);
            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_CROSSFADING));

            fixture.SetInstance(0, At(500f));
            fixture.Run(deltaTime: 0.1f);
            Assert.AreEqual(1, fixture.Stat(CullAndLODJob.STAT_CULLED_DISTANCE));
            Assert.AreEqual(LODFadeState.UNSET, fixture.FadeStateAt(0).from);

            fixture.SetInstance(0, At(40f));
            fixture.Run(deltaTime: 0.1f);
            Assert.AreEqual(1, fixture.Count(COLOR, STEADY, 1), "re-entering snaps instead of dissolving");
        }

        // ---- Service level ----

        [Test]
        public void Service_CrossFadingDraws_UseOneKeywordCloneMaterialPerSourceMaterial()
        {
            using var scene = new ServiceScene();
            scene.profile.isLODCrossFade = 1;
            scene.profile.isAnimateCrossFade = 0;
            scene.profile.lodCrossFadeTransitionWidth = 0.1f;
            int key = scene.Register();

            // Camera at z = -10: z = 6.5 is 16.5 away, inside the LOD0 band.
            scene.Upload(key, new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(0f, 0f, 6.5f), Quaternion.identity, Vector3.one),
            });
            GPUICoreAPI.TickForTests(scene.camera);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.AreEqual(1, stats.crossFadingInstances);
            Assert.AreEqual(2, stats.drawCallCount, "the outgoing and incoming LOD each draw once");
            Assert.AreEqual(2, GPUICoreAPI.FadeMaterialCountForTests, "LOD0 and LOD1 each carry their own material");

            GPUICoreAPI.TickForTests(scene.camera);
            Assert.AreEqual(2, GPUICoreAPI.FadeMaterialCountForTests, "clones are cached across frames");
        }

        [Test]
        public void Service_SteadyInstances_DrawWithoutFadeMaterials()
        {
            using var scene = new ServiceScene();
            scene.profile.isLODCrossFade = 1;
            scene.profile.isAnimateCrossFade = 0;
            int key = scene.Register();

            scene.Upload(key, new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(0f, 0f, 2f), Quaternion.identity, Vector3.one),
            });
            GPUICoreAPI.TickForTests(scene.camera);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.AreEqual(0, stats.crossFadingInstances);
            Assert.AreEqual(1, stats.drawCallCount);
            Assert.AreEqual(0, GPUICoreAPI.FadeMaterialCountForTests);
        }

        [Test]
        public void Service_Reset_ReleasesTheFadeMaterials()
        {
            using var scene = new ServiceScene();
            scene.profile.isLODCrossFade = 1;
            scene.profile.isAnimateCrossFade = 0;
            int key = scene.Register();
            scene.Upload(key, new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(0f, 0f, 6.5f), Quaternion.identity, Vector3.one),
            });
            GPUICoreAPI.TickForTests(scene.camera);
            Assert.AreEqual(2, GPUICoreAPI.FadeMaterialCountForTests);

            GPUICoreAPI.ResetForTests();

            Assert.AreEqual(0, GPUICoreAPI.FadeMaterialCountForTests);
        }

        [Test]
        public void Service_AnimatedFade_ReuploadedTransformsSnap()
        {
            using var scene = new ServiceScene();
            scene.profile.isLODCrossFade = 1;
            scene.profile.isAnimateCrossFade = 1;
            scene.profile.lodCrossFadeAnimateSpeed = 4f;
            int key = scene.Register();

            scene.Upload(key, new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(0f, 0f, 2f), Quaternion.identity, Vector3.one),
            });
            GPUICoreAPI.TickForTests(scene.camera, 0.1f);
            Assert.AreEqual(1, GPUICoreAPI.GetLastFrameStatsForTests().lodHistogram[0]);

            scene.Upload(key, new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(0f, 0f, 30f), Quaternion.identity, Vector3.one),
            });
            GPUICoreAPI.TickForTests(scene.camera, 0.1f);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.AreEqual(0, stats.crossFadingInstances, "a re-uploaded slot is a new instance");
            Assert.AreEqual(1, stats.lodHistogram[1]);
            Assert.AreEqual(1, stats.drawCallCount);
        }

        [Test]
        public void Service_AnimatedFade_MovedInstanceDissolves()
        {
            using var scene = new ServiceScene();
            scene.profile.isLODCrossFade = 1;
            scene.profile.isAnimateCrossFade = 1;
            scene.profile.lodCrossFadeAnimateSpeed = 4f;
            int key = scene.Register();

            scene.Upload(key, new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(0f, 0f, 2f), Quaternion.identity, Vector3.one),
            });
            GPUICoreAPI.TickForTests(scene.camera, 0.1f);

            scene.camera.transform.position = new Vector3(0f, 0f, -38f);
            GPUICoreAPI.TickForTests(scene.camera, 0.1f);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.AreEqual(1, stats.crossFadingInstances, "a camera move dissolves the LOD change");
            Assert.AreEqual(2, stats.drawCallCount);
        }

        private static Matrix4x4 At(float distance) =>
            Matrix4x4.TRS(new Vector3(0f, 0f, distance), Quaternion.identity, Vector3.one);

        private static JobFixture Instance(float distance, int crossFadeMode)
        {
            var fixture = new JobFixture(BOUND, At(distance));
            fixture.UseThreeLODs();
            fixture.job.crossFadeMode = crossFadeMode;
            fixture.job.crossFadeTransitionWidth = 0.1f;
            fixture.job.crossFadeAnimateSpeed = 4f;
            return fixture;
        }
    }
}
