using LiveKit.Rooms.Streaming.Audio;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.PerformanceTesting;
using UnityEngine;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Benchmarks the ILD equal-power panning cost of <c>LivekitAudioSource.ApplySpatialPanning</c>.
    ///
    /// Uses a cached open-instance delegate to eliminate reflection overhead from measurements.
    /// Buffer reset is performed in SetUp (excluded from timing).
    ///
    /// For real AudioThread profiling with panning enabled (via fake stream injection),
    /// use <see cref="NearbyAudioPerformanceManualTest"/> with the Unity Profiler —
    /// look for <c>LiveKit.Spatial.ILD.EqualPower</c> marker on the Audio Mixer Thread.
    /// </summary>
    [Category("Performance")]
    public class NearbyAudioSpatializationPerformanceTest
    {
        private delegate void ApplySpatialPanningDelegate(LivekitAudioSource instance, float[] data, int channels);

        private static readonly MethodInfo APPLY_SPATIAL_PANNING_METHOD = typeof(LivekitAudioSource)
            .GetMethod("ApplySpatialPanning", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly ApplySpatialPanningDelegate APPLY_SPATIAL_PANNING =
            (ApplySpatialPanningDelegate)Delegate.CreateDelegate(
                typeof(ApplySpatialPanningDelegate), APPLY_SPATIAL_PANNING_METHOD);

        private readonly List<LivekitAudioSource> sources = new (128);

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Assert.That(APPLY_SPATIAL_PANNING_METHOD, Is.Not.Null,
                "ApplySpatialPanning not found — LiveKit SDK API may have changed");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (LivekitAudioSource source in sources)
                if (source != null) UnityEngine.Object.DestroyImmediate(source.gameObject);

            sources.Clear();
        }

        /// <summary>
        /// Measures only the panning math for N sources.
        /// Buffer fill is in SetUp (not measured); delegate call eliminates reflection overhead.
        /// </summary>
        [Test]
        [Performance]
        [TestCase(1, 512, TestName = "Panning_1_Source_512")]
        [TestCase(1, 1024, TestName = "Panning_1_Source_1024")]
        [TestCase(10, 512, TestName = "Panning_10_Sources_512")]
        [TestCase(10, 1024, TestName = "Panning_10_Sources_1024")]
        [TestCase(30, 512,  false, TestName = "Panning_30_Sources_512")]
        [TestCase(30, 1024,  false, TestName = "Panning_30_Sources_1024")]
        [TestCase(30, 1024,  true, TestName = "Panning_30_Sources_1024_SmoothPanning")]
        [TestCase(50, 1024,  false, TestName = "Panning_50_Sources_1024")]
        [TestCase(100, 1024,  false, TestName = "Panning_100_Sources_1024")]
        public void ApplySpatialPanningForNSources(int sourceCount, int bufferSize, bool smoothPanning = false)
        {
            const int CHANNELS = 2;
            int totalSamples = bufferSize * CHANNELS;
            float[] audioBuffer = new float[totalSamples];

            SetupSources(sourceCount, smoothPanning);

            Measure
               .Method(() =>
                {
                    for (int s = 0; s < sourceCount; s++)
                        APPLY_SPATIAL_PANNING(sources[s], audioBuffer, CHANNELS);
                })
               .SetUp(() => Array.Fill(audioBuffer, 0.5f))
               .WarmupCount(10)
               .MeasurementCount(50)
               .GC()
               .Run();
        }

        private void SetupSources(int count, bool smoothPanning = false)
        {
            UnityEngine.Random.InitState(42);

            for (int i = 0; i < count; i++)
            {
                LivekitAudioSource source = LivekitAudioSource.New(isSpatial: true);
                source.SetSpatialSettings(true, 0.75f);
                source.SetSpatialAngles(
                    UnityEngine.Random.Range(-Mathf.PI, Mathf.PI),
                    UnityEngine.Random.Range(-Mathf.PI * 0.5f, Mathf.PI * 0.5f));
                sources.Add(source);
            }
        }
    }
}
