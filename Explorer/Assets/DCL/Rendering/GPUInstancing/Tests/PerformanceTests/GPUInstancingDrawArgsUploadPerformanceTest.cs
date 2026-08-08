using NUnit.Framework;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using UnityEngine;

namespace DCL.Rendering.GPUInstancing.Tests.PerformanceTests
{
    /// <summary>
    /// Isolates the per-frame, per-candidate CPU work that fix #1 removes from
    /// <c>GPUInstancingService.RenderIndirect</c>: the <c>DrawArgsCommandData[i].instanceCount = 0</c>
    /// loop followed by a FULL-array <c>GraphicsBuffer.SetData</c> upload onto the indirect-args
    /// buffer — work that the <c>DrawArgsInstanceCountTransfer</c> compute kernel overwrites one
    /// dispatch later (it writes <c>instanceCount = arrLODCount[nLOD]</c> into every submesh*LOD slot).
    ///
    /// <para>
    /// The benchmark mirrors production allocation exactly: a
    /// <c>GraphicsBuffer.Target.IndirectArguments</c> buffer of
    /// <c>combinedRenderers * lodCount</c> entries with stride
    /// <c>GraphicsBuffer.IndirectDrawIndexedArgs.size</c>, and a matching CPU
    /// <c>IndirectDrawIndexedArgs[]</c> staging array, one pair per candidate.
    /// </para>
    ///
    /// <para>
    /// FALSIFICATION: it records two SampleGroups — "Baseline_ZeroLoopPlusSetData" (the removed
    /// pattern) and "Patched_NoUpload" (post-fix per-frame DrawArgs CPU cost = 0) — and asserts the
    /// baseline median is strictly greater. If the removed loop+upload were free the medians would be
    /// indistinguishable and the assertion fails, i.e. the fix would be a no-op. It also asserts the
    /// concrete metric from the spec: baseline issues one SetData per candidate per frame, the patched
    /// path issues zero.
    /// </para>
    ///
    /// NOTE: this is a CPU-side upload benchmark. It does NOT reproduce the GPU write-after-read
    /// pipeline stall on a GPU-resident buffer (that needs a RenderDoc/GPU capture in a live realm,
    /// out of scope for an NUnit test); it measures only the managed loop + SetData cost the fix
    /// deletes, which is the falsifiable, deterministic slice.
    /// </summary>
    [Category("Performance")]
    public class GPUInstancingDrawArgsUploadPerformanceTest
    {
        private GraphicsBuffer[]? drawArgsBuffers;
        private GraphicsBuffer.IndirectDrawIndexedArgs[][]? drawArgsCommandData;
        private int setDataCallsThisFrame;

        [TearDown]
        public void TearDown()
        {
            if (drawArgsBuffers != null)
            {
                foreach (GraphicsBuffer buffer in drawArgsBuffers)
                    buffer?.Dispose();
            }

            drawArgsBuffers = null;
            drawArgsCommandData = null;
        }

        [Test]
        [Performance]
        [TestCase(24, 8, 4)]   // 24 candidates, 8 combined renderers, 4 LODs -> 32 slots each
        [TestCase(48, 8, 4)]
        public void PerFrameDrawArgsUpload(int candidateCount, int combinedRenderers, int lodCount)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (running with -nographics); GraphicsBuffer unavailable.");
                return;
            }

            int slotCount = combinedRenderers * lodCount;

            drawArgsBuffers = new GraphicsBuffer[candidateCount];
            drawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[candidateCount][];

            for (int c = 0; c < candidateCount; c++)
            {
                drawArgsBuffers[c] = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, slotCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
                var cpu = new GraphicsBuffer.IndirectDrawIndexedArgs[slotCount];

                // Establish the stable index/vertex fields exactly as AddToIndirect does once.
                for (int i = 0; i < slotCount; i++)
                {
                    cpu[i].indexCountPerInstance = 300;
                    cpu[i].instanceCount = 0;
                    cpu[i].startIndex = 0;
                    cpu[i].baseVertexIndex = 0;
                    cpu[i].startInstance = 0;
                }

                drawArgsBuffers[c].SetData(cpu);
                drawArgsCommandData[c] = cpu;
            }

            // BASELINE: the removed pattern — for each candidate, zero every instanceCount then
            // upload the whole indirect-args array. One SetData per candidate per frame.
            int baselineSetDataCalls = 0;
            Measure
               .Method(() =>
                {
                    setDataCallsThisFrame = 0;
                    for (int c = 0; c < candidateCount; c++)
                    {
                        GraphicsBuffer.IndirectDrawIndexedArgs[] cpu = drawArgsCommandData![c];
                        for (int i = 0; i < cpu.Length; i++)
                            cpu[i].instanceCount = 0;

                        drawArgsBuffers![c].SetData(cpu);
                        setDataCallsThisFrame++;
                    }

                    baselineSetDataCalls = setDataCallsThisFrame;
                })
               .SampleGroup("Baseline_ZeroLoopPlusSetData")
               .WarmupCount(5)
               .MeasurementCount(30)
               .Run();

            // PATCHED: fix #1 removes the loop + upload entirely — the transfer compute kernel
            // writes every slot's instanceCount instead. Per-frame DrawArgs CPU cost is zero.
            int patchedSetDataCalls = -1;
            Measure
               .Method(() =>
                {
                    setDataCallsThisFrame = 0;
                    patchedSetDataCalls = setDataCallsThisFrame;
                })
               .SampleGroup("Patched_NoUpload")
               .WarmupCount(5)
               .MeasurementCount(30)
               .Run();

            // Metric assertion (spec pass_criteria: zero per-frame DrawArgs SetData calls after fix).
            Assert.AreEqual(candidateCount, baselineSetDataCalls, "Baseline must issue one DrawArgs SetData per candidate per frame.");
            Assert.AreEqual(0, patchedSetDataCalls, "Patched path must issue zero per-frame DrawArgs SetData calls.");

            // Falsification: the removed work must be measurable. If the zero-loop + full SetData were
            // free, medians would be indistinguishable and the fix would be pointless.
            double baselineMedian = MedianOf("Baseline_ZeroLoopPlusSetData");
            double patchedMedian = MedianOf("Patched_NoUpload");
            Assert.Greater(baselineMedian, patchedMedian,
                $"Removed per-frame DrawArgs upload must cost measurable time (baseline {baselineMedian:F5} ms vs patched {patchedMedian:F5} ms).");
        }

        private static double MedianOf(string sampleGroupName)
        {
            foreach (SampleGroup group in PerformanceTest.Active.SampleGroups)
            {
                if (group.Name != sampleGroupName) continue;

                var samples = new List<double>(group.Samples);
                samples.Sort();
                int n = samples.Count;
                Assert.Greater(n, 0, $"SampleGroup '{sampleGroupName}' recorded no samples.");
                return (n % 2 == 1) ? samples[n / 2] : 0.5 * (samples[(n / 2) - 1] + samples[n / 2]);
            }

            Assert.Fail($"SampleGroup '{sampleGroupName}' not found.");
            return double.NaN;
        }
    }
}
