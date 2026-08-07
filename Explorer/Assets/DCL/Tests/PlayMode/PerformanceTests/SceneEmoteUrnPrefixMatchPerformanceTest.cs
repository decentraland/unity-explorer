using DCL.AvatarRendering.Emotes.Play;
using NUnit.Framework;
using System;
using Unity.PerformanceTesting;
using Unity.Profiling;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    /// Verifies TryMatchSceneEmotePayload matches OldMatch's allocation-based prefix scan exactly (match result and
    /// parsed hash) across matching, near-miss, and boundary payloads, and that repeated non-matching lookups are
    /// allocation-free.
    /// </summary>
    [Category("Performance")]
    public class SceneEmoteUrnPrefixMatchPerformanceTest
    {
        private static bool OldMatch(ReadOnlySpan<char> payload, string candidateName, out string hash)
        {
            ReadOnlySpan<char> prefix = (candidateName + "-").AsSpan();

            if (payload.StartsWith(prefix, StringComparison.Ordinal))
            {
                hash = payload.Slice(prefix.Length).ToString();
                return true;
            }

            hash = string.Empty;
            return false;
        }

        private static readonly (string payload, string name)[] FIXTURE =
        {
            ("scene-QmHash", "scene"),
            ("my-scene-QmHash", "my-scene"),
            ("scene-", "scene"),
            ("scenex-QmHash", "scene"),
            ("sce", "scene"),
            ("scene", "scene"),
            ("Scene-QmHash", "scene"),
            ("scene-Qm-very-long-hash-value", "scene"),
        };

        [Test]
        [Performance]
        public void PrefixScan_ZeroAlloc_SemanticsUnchanged()
        {
            foreach ((string payload, string name) in FIXTURE)
            {
                bool oldResult = OldMatch(payload.AsSpan(), name, out string oldHash);
                bool newResult = CharacterEmoteSystem.TryMatchSceneEmotePayload(payload.AsSpan(), name, out string newHash);

                Assert.AreEqual(oldResult, newResult, $"match result diverged for payload='{payload}' name='{name}'");
                Assert.AreEqual(oldHash, newHash, $"parsed hash diverged for payload='{payload}' name='{name}'");
            }

            string[] nonMatchingNames = new string[20];
            for (int i = 0; i < nonMatchingNames.Length; i++)
                nonMatchingNames[i] = "candidate_scene_name_" + i;

            const string probePayload = "unrelated-payload-that-matches-nothing";

            for (int i = 0; i < nonMatchingNames.Length; i++)
                CharacterEmoteSystem.TryMatchSceneEmotePayload(probePayload.AsSpan(), nonMatchingNames[i], out _);

            ProfilerRecorder gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
            Measure.Method(() =>
                    {
                        for (int iter = 0; iter < 5000; iter++)
                        for (int i = 0; i < nonMatchingNames.Length; i++)
                            CharacterEmoteSystem.TryMatchSceneEmotePayload(probePayload.AsSpan(), nonMatchingNames[i], out _);
                    })
                   .WarmupCount(3).MeasurementCount(10).GC().Run();
            long gcBytes = gcAlloc.LastValue;
            gcAlloc.Dispose();

            Assert.AreEqual(0, gcBytes, $"non-matching prefix scan must be allocation-free, allocated {gcBytes} bytes");
        }
    }
}
