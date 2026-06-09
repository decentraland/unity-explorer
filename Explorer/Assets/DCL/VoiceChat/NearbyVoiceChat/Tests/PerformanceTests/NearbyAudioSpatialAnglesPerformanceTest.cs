using NUnit.Framework;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEngine;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Isolates the per-source spatial-angles math from <c>NearbyAudioPositionSystem.CalculateSpatialAngles</c>:
    /// listener-space reprojection via <c>Transform.InverseTransformPoint</c> + two
    /// <c>math.atan2</c> calls + one <c>math.sqrt</c>. Subtracting this benchmark from
    /// <see cref="NearbyAudioPositionSystemPerformanceTest"/> reveals how much of per-frame cost is
    /// pure math vs. ECS/Unity-API overhead — informs whether a Burst-job rewrite of the math is
    /// justified.
    /// <para>
    /// Test cases mirror the system benchmark (10/50/100) for direct comparison. Listener-Transform
    /// is real (so <c>InverseTransformPoint</c> hits the same managed path as production); source
    /// positions are pre-generated with a fixed seed for run-to-run determinism.
    /// </para>
    /// </summary>
    [Category("Performance")]
    public class NearbyAudioSpatialAnglesPerformanceTest
    {
        private GameObject? listenerGo;
        private Transform? listenerTransform;
        private Vector3[]? sourcePositions;

        [TearDown]
        public void TearDown()
        {
            if (listenerGo != null)
                Object.DestroyImmediate(listenerGo);

            sourcePositions = null;
        }

        [Test]
        [Performance]
        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        [TestCase(1000)]
        public void CalculateSpatialAnglesForNSources(int sourceCount)
        {
            // Listener with a non-trivial rotation — keeps InverseTransformPoint from short-circuiting
            // on an identity worldToLocalMatrix and exercises the full 3x4 matrix multiply.
            listenerGo = new GameObject("PerfListener");
            listenerGo.transform.SetPositionAndRotation(
                new Vector3(0f, 1.6f, 0f),
                Quaternion.Euler(15f, 30f, 0f));
            listenerTransform = listenerGo.transform;

            UnityEngine.Random.InitState(42);
            sourcePositions = new Vector3[sourceCount];
            for (int i = 0; i < sourceCount; i++)
                sourcePositions[i] = UnityEngine.Random.insideUnitSphere * 15f;

            // Allocation-free output sink — prevents the JIT from optimizing the loop body away
            // and avoids tuple allocation per iteration that would skew GC measurement.
            float azimuthSink = 0f;
            float elevationSink = 0f;

            Measure
               .Method(() =>
                {
                    for (int i = 0; i < sourcePositions!.Length; i++)
                    {
                        Vector3 local = listenerTransform!.InverseTransformPoint(sourcePositions[i]);

                        float horizontalDist = math.sqrt((local.x * local.x) + (local.z * local.z));
                        elevationSink = math.atan2(local.y, horizontalDist);
                        azimuthSink = math.atan2(local.x, local.z);
                    }
                })
               .WarmupCount(10)
               .MeasurementCount(50)
               .GC()
               .Run();

            // Reference the sinks so the loop body is observably side-effecting and cannot be
            // dead-code-eliminated by the JIT under aggressive optimization.
            Assert.That(float.IsFinite(azimuthSink) && float.IsFinite(elevationSink));
        }
    }
}
