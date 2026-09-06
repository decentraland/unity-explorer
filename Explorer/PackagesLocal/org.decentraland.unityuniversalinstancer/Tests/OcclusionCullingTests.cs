using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// Hi-Z occlusion culling: the pyramid sizing, mip selection and depth
    /// conventions the compute shader mirrors, the visible-buffer layout the
    /// compaction pass writes into, the indirect-args addressing, the
    /// production profile's occlusion fields, the packaged compute shader
    /// and the fallback to direct draws where the pass cannot run.
    /// </summary>
    [TestFixture]
    public class OcclusionCullingTests
    {
        private const string PROFILE_PATH = "Assets/DCL/Landscape/Assets/GPUI/Profiles/Decentraland_TreeProfile.asset";

        // ---- Pyramid geometry ----

        [Test]
        public void PyramidSize_IsTheLargestPowerOfTwoAtOrBelowTheRenderSize_Capped()
        {
            Assert.AreEqual(new int2(1024, 1024), OcclusionMath.PyramidSize(1920, 1080));
            Assert.AreEqual(new int2(512, 512), OcclusionMath.PyramidSize(800, 600));
            Assert.AreEqual(new int2(1024, 1024), OcclusionMath.PyramidSize(3840, 2160));
            Assert.AreEqual(new int2(256, 128), OcclusionMath.PyramidSize(300, 200));
            Assert.AreEqual(new int2(1, 1), OcclusionMath.PyramidSize(1, 1));
        }

        [Test]
        public void MipCount_ReachesOneTexel()
        {
            Assert.AreEqual(11, OcclusionMath.MipCount(new int2(1024, 1024)));
            Assert.AreEqual(11, OcclusionMath.MipCount(new int2(1024, 512)));
            Assert.AreEqual(1, OcclusionMath.MipCount(new int2(1, 1)));
        }

        [Test]
        public void Footprint_DoublesPerAccuracyStep_WithinBounds()
        {
            Assert.AreEqual(2, OcclusionMath.Footprint(1));
            Assert.AreEqual(4, OcclusionMath.Footprint(2));
            Assert.AreEqual(8, OcclusionMath.Footprint(3), "the production profile authors accuracy 3");
            Assert.AreEqual(2, OcclusionMath.Footprint(0));
            Assert.AreEqual(16, OcclusionMath.Footprint(9));
        }

        [Test]
        public void SelectMip_PicksTheCoarsestLevelWhereTheRectFitsTheFootprint()
        {
            Assert.AreEqual(0, OcclusionMath.SelectMip(8f, 8, 10));
            Assert.AreEqual(1, OcclusionMath.SelectMip(9f, 8, 10));
            Assert.AreEqual(3, OcclusionMath.SelectMip(64f, 8, 10));
            Assert.AreEqual(10, OcclusionMath.SelectMip(1e6f, 8, 10), "clamped to the pyramid's last mip");
            Assert.AreEqual(0, OcclusionMath.SelectMip(0f, 8, 10));
        }

        // ---- Depth conventions ----

        [Test]
        public void DepthParams_MapNearToZeroAndFarToOne_OnEveryConvention()
        {
            float2 reversed = OcclusionMath.DepthParams(reversedZ: true, openGLClipSpace: false);
            Assert.AreEqual(0f, (1f * reversed.x) + reversed.y, 1e-6f, "reversed-Z near plane");
            Assert.AreEqual(1f, (0f * reversed.x) + reversed.y, 1e-6f, "reversed-Z far plane");

            float2 d3d = OcclusionMath.DepthParams(reversedZ: false, openGLClipSpace: false);
            Assert.AreEqual(0f, (0f * d3d.x) + d3d.y, 1e-6f);
            Assert.AreEqual(1f, (1f * d3d.x) + d3d.y, 1e-6f);

            float2 gl = OcclusionMath.DepthParams(reversedZ: false, openGLClipSpace: true);
            Assert.AreEqual(0f, (-1f * gl.x) + gl.y, 1e-6f, "OpenGL clip z runs -1..1");
            Assert.AreEqual(1f, (1f * gl.x) + gl.y, 1e-6f);
        }

        [Test]
        public void NormalizeBufferDepth_FlipsReversedZ()
        {
            Assert.AreEqual(0.25f, OcclusionMath.NormalizeBufferDepth(0.75f, reversedZ: true), 1e-6f);
            Assert.AreEqual(0.75f, OcclusionMath.NormalizeBufferDepth(0.75f, reversedZ: false), 1e-6f);
        }

        [Test]
        public void IsOccluded_NeedsTheNearestPointBeyondTheFarthestOccluder_ByMoreThanTheBias()
        {
            Assert.IsTrue(OcclusionMath.IsOccluded(nearDepth: 0.6f, farthestOccluderDepth: 0.5f, bias: 0.0001f));
            Assert.IsFalse(OcclusionMath.IsOccluded(nearDepth: 0.5f, farthestOccluderDepth: 0.5f, bias: 0.0001f), "touching is visible");
            Assert.IsFalse(OcclusionMath.IsOccluded(nearDepth: 0.50005f, farthestOccluderDepth: 0.5f, bias: 0.0001f), "inside the bias is visible");
            Assert.IsFalse(OcclusionMath.IsOccluded(nearDepth: 0.4f, farthestOccluderDepth: 0.5f, bias: 0.0001f));
        }

        // ---- Buffer layouts ----

        [Test]
        public void VisibleLayout_ReservesDemotionRoomInShadowOnlyBuckets()
        {
            var counts = new NativeArray<int>(BucketLayout.COUNT, Allocator.Temp);
            counts[BucketLayout.Index(BucketLayout.MODE_BOTH, BucketLayout.FADE_STEADY, 0)] = 5;
            counts[BucketLayout.Index(BucketLayout.MODE_SHADOW_ONLY, BucketLayout.FADE_STEADY, 0)] = 2;
            counts[BucketLayout.Index(BucketLayout.MODE_COLOR_ONLY, BucketLayout.FADE_STEADY, 1)] = 3;
            var offsets = new int[BucketLayout.COUNT];
            var capacities = new int[BucketLayout.COUNT];

            int withDemotion = OcclusionMath.ComputeVisibleLayout(counts, true, offsets, capacities);
            Assert.AreEqual(15, withDemotion, "5 both + (2 + 5 demoted) shadow-only + 3 colour-only");
            Assert.AreEqual(7, capacities[BucketLayout.Index(BucketLayout.MODE_SHADOW_ONLY, BucketLayout.FADE_STEADY, 0)]);
            int running = 0;
            for (int b = 0; b < BucketLayout.COUNT; b++)
            {
                Assert.AreEqual(running, offsets[b], $"bucket {b} starts where the previous one ends");
                running += capacities[b];
            }

            int withoutDemotion = OcclusionMath.ComputeVisibleLayout(counts, false, offsets, capacities);
            Assert.AreEqual(10, withoutDemotion);
            counts.Dispose();
        }

        [Test]
        public void BucketLayout_IndexRoundTrips_AndArgEntriesAreUnique()
        {
            var seen = new HashSet<int>();
            for (int mode = 0; mode < BucketLayout.MODES; mode++)
            for (int fadeState = 0; fadeState < BucketLayout.FADE_STATES; fadeState++)
            for (int level = 0; level < BucketLayout.LEVELS; level++)
            {
                int bucket = BucketLayout.Index(mode, fadeState, level);
                Assert.IsTrue(seen.Add(bucket));
                Assert.Less(bucket, BucketLayout.COUNT);
                Assert.AreEqual(mode, BucketLayout.Mode(bucket));
                Assert.AreEqual(fadeState, BucketLayout.FadeState(bucket));
                Assert.AreEqual(level, BucketLayout.Level(bucket));
            }
            Assert.AreEqual(BucketLayout.COUNT, seen.Count);

            var entries = new HashSet<int>();
            const int SLOTS = 5;
            for (int slot = 0; slot < SLOTS; slot++)
            for (int mode = 0; mode < BucketLayout.MODES; mode++)
            for (int fadeState = 0; fadeState < BucketLayout.FADE_STATES; fadeState++)
            {
                int entry = BucketLayout.ArgEntry(slot, mode, fadeState);
                Assert.IsTrue(entries.Add(entry));
                Assert.Less(entry, SLOTS * BucketLayout.ARG_ENTRIES_PER_SLOT);
            }
        }

        // ---- Profile and packaging ----

        [Test]
        public void ProfileSnapshot_ResolvesTheProductionProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<GPUIProfile>(PROFILE_PATH);
            Assert.IsNotNull(profile);

            ProfileSnapshot settings = ProfileSnapshot.From(profile);
            Assert.IsTrue(settings.occlusionCulling);
            Assert.AreEqual(3, settings.occlusionAccuracy);
            Assert.AreEqual(0.0001f, settings.occlusionOffset, 1e-7f);
            Assert.IsFalse(settings.shadowOcclusionCulling);
            Assert.IsTrue(settings.castShadows);
            Assert.IsTrue(settings.shadowDistanceCulling);
            Assert.AreEqual(20f, settings.minShadowCullingDistance);
            Assert.IsFalse(settings.shadowFrustumCulling);
            Assert.AreEqual(10f, settings.shadowFrustumOffset);
            Assert.AreEqual(new int4(0, 1, 2, 3), settings.shadowLODMap);
            Assert.AreEqual(CullAndLODJob.CROSSFADE_ANIMATED, settings.crossFadeMode);
            Assert.AreEqual(0.1f, settings.crossFadeTransitionWidth, 1e-6f);
            Assert.AreEqual(4f, settings.crossFadeAnimateSpeed);
            Assert.AreEqual(1000f, settings.cullDistance);
            Assert.AreEqual(0.1f, settings.frustumOffset, 1e-6f);
        }

        [Test]
        public void ProfileFlush_PropagatesTheOcclusionFields()
        {
            using var scene = new ServiceScene();
            scene.profile.isOcclusionCulling = 1;
            scene.profile.occlusionAccuracy = 3;
            int key = scene.Register();

            scene.profile.isOcclusionCulling = 0;
            scene.profile.occlusionAccuracy = 1;
            scene.profile.isShadowOcclusionCulling = 1;
            Assert.IsTrue(GPUICoreAPI.GetSettingsForTests(key).occlusionCulling, "mutation without a flush must not propagate");

            scene.profile.SetParameterBufferData();

            ProfileSnapshot after = GPUICoreAPI.GetSettingsForTests(key);
            Assert.IsFalse(after.occlusionCulling);
            Assert.AreEqual(1, after.occlusionAccuracy);
            Assert.IsTrue(after.shadowOcclusionCulling);
        }

        [Test]
        public void ProfileSnapshot_WithoutAProfile_UsesEngineDefaults()
        {
            ProfileSnapshot settings = ProfileSnapshot.From(null);
            Assert.IsFalse(settings.castShadows);
            Assert.IsFalse(settings.occlusionCulling);
            Assert.AreEqual(CullAndLODJob.CROSSFADE_NONE, settings.crossFadeMode);
            Assert.IsTrue(settings.frustumCulling);
            Assert.AreEqual(1f, settings.lodBias);
        }

        // Kernel tables only exist once a graphics device compiled the
        // shader, so the packaged source and the importer's diagnostics are
        // what a headless run can check.
        [Test]
        public void OcclusionComputeShader_IsPackagedWithEveryKernel_AndCompilesClean()
        {
            var shader = Resources.Load<ComputeShader>(OcclusionCulling.COMPUTE_RESOURCE);
            Assert.IsNotNull(shader, "GPUIOcclusionCulling.compute must ship under Runtime/Resources");

            string path = AssetDatabase.GetAssetPath(shader);
            StringAssert.Contains("/Resources/", path);
            string source = System.IO.File.ReadAllText(path);
            foreach (string kernel in new[] { "CopyDepth", "ReduceDepth", "CullInstances", "WriteArgs" })
                StringAssert.Contains($"#pragma kernel {kernel}", source);

            foreach (ShaderMessage message in ShaderUtil.GetComputeShaderMessages(shader))
                Assert.AreNotEqual(ShaderCompilerMessageSeverity.Error, message.severity,
                    $"{message.platform} {message.file}({message.line}): {message.message}");
        }

        // ---- Fallback ----

        [Test]
        public void FirstFrame_OrNoComputeDevice_DrawsDirectlyWithoutOcclusion()
        {
            using var scene = new ServiceScene();
            scene.profile.isOcclusionCulling = 1;
            scene.profile.isLODCrossFade = 0;
            int key = scene.Register();

            scene.Upload(key, new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(0f, 0f, 2f), Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(new Vector3(1f, 0f, 2f), Quaternion.identity, Vector3.one),
            });
            GPUICoreAPI.TickForTests(scene.camera);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.IsFalse(stats.occlusionCullingActive, "no depth pyramid exists before a driven frame renders");
            Assert.AreEqual(2, stats.totalInstancesDrawn);
            Assert.AreEqual(1, stats.drawCallCount);
            Assert.AreEqual(0, stats.culledByOcclusion);
        }
    }
}
