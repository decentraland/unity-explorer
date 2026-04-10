using LiveKit.Rooms.Streaming.Audio;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    /// Benchmarks the ILD equal-power panning cost of <c>LivekitAudioSource.ApplySpatialPanning</c>.
    ///
    /// Calls the REAL method via reflection on real instances configured through the public API.
    /// We use reflection because <c>OnAudioFilterRead</c> requires a live <c>AudioStream</c>
    /// from a LiveKit room connection to reach the panning code path.
    ///
    /// For real AudioThread profiling with panning enabled (via fake stream injection),
    /// use <see cref="ProximityAudioPerformanceManualTest"/> with the Unity Profiler —
    /// look for <c>LiveKit.Spatial.ILD.EqualPower</c> marker on the Audio Mixer Thread.
    /// </summary>
    [Category("Performance")]
    public class ProximityAudioSpatializationPerformanceTest
    {
        private static readonly MethodInfo APPLY_SPATIAL_PANNING = typeof(LivekitAudioSource)
            .GetMethod("ApplySpatialPanning", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly List<LivekitAudioSource> sources = new (128);

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Assert.That(APPLY_SPATIAL_PANNING, Is.Not.Null,
                "ApplySpatialPanning not found — LiveKit SDK API may have changed");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (LivekitAudioSource source in sources)
                if (source != null) Object.DestroyImmediate(source.gameObject);

            sources.Clear();
        }

        /// <summary>
        /// Calls the real <c>LivekitAudioSource.ApplySpatialPanning</c> on N real instances.
        /// Each instance is configured via public API (SetSpatialSettings / SetSpatialAngles).
        /// Buffer is pre-filled to simulate ReadAudio output.
        /// </summary>
        [Test]
        [Performance]
        [TestCase(1, 512, TestName = "Panning_1_Source_512")]
        [TestCase(1, 1024, TestName = "Panning_1_Source_1024")]
        [TestCase(10, 1024, TestName = "Panning_10_Sources_1024")]
        [TestCase(50, 1024, TestName = "Panning_50_Sources_1024")]
        [TestCase(100, 1024, TestName = "Panning_100_Sources_1024")]
        public void ApplySpatialPanningForNSources(int sourceCount, int bufferSize)
        {
            const int CHANNELS = 2;
            int totalSamples = bufferSize * CHANNELS;
            float[] audioBuffer = new float[totalSamples];

            SetupSources(sourceCount);

            var invokeArgs = new object[] { audioBuffer, CHANNELS };

            ProfilerRecorder gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");

            Measure
               .Method(() =>
                {
                    for (int s = 0; s < sourceCount; s++)
                    {
                        for (int i = 0; i < totalSamples; i++)
                            audioBuffer[i] = 0.5f;

                        APPLY_SPATIAL_PANNING.Invoke(sources[s], invokeArgs);
                    }
                })
               .WarmupCount(10)
               .MeasurementCount(50)
               .GC()
               .Run();

            long gcBytes = gcAlloc.LastValue;
            gcAlloc.Dispose();

            Debug.Log($"[ApplySpatialPanning] {sourceCount} sources × {bufferSize} samples — GC.Alloc last: {gcBytes} bytes");
            Assert.That(gcBytes, Is.EqualTo(0), $"ApplySpatialPanning must be allocation-free, but allocated {gcBytes} bytes with {sourceCount} sources × {bufferSize} samples");
        }

        private void SetupSources(int count)
        {
            Random.InitState(42);

            for (int i = 0; i < count; i++)
            {
                LivekitAudioSource source = LivekitAudioSource.New(isSpatial: true);
                source.SetSpatialSettings(true, 0.75f, false);
                source.SetSpatialAngles(
                    Random.Range(-Mathf.PI, Mathf.PI),
                    Random.Range(-Mathf.PI * 0.5f, Mathf.PI * 0.5f));
                sources.Add(source);
            }
        }
    }
}
