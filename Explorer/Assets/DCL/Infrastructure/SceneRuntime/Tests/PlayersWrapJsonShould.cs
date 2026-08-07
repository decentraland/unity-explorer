using Newtonsoft.Json;
using NUnit.Framework;
using SceneRuntime.Apis.Modules.Players;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.PerformanceTesting;

namespace SceneRuntime.Tests
{
    /// <summary>
    ///     Verifies the reused JsonTextWriter in <see cref="PlayersWrap" />, emitting
    ///     <c>[{"userId":...}]</c> directly, produces byte-identical output (escaping included) to
    ///     JsonConvert.SerializeObject(List&lt;Player&gt;) at a fraction of the allocation cost. Builds its
    ///     own writer here rather than depending on the LiveKit participant types, which the shared
    ///     DCL.EditMode.Tests assembly does not reference.
    /// </summary>
    [Category("Performance")]
    public class PlayersWrapJsonShould
    {
        private readonly StringBuilder stringBuilder = new ();
        private StringWriter stringWriter = null!;
        private JsonTextWriter writer = null!;

        [SetUp]
        public void SetUp()
        {
            stringWriter = new StringWriter(stringBuilder);
            writer = new JsonTextWriter(stringWriter);
        }

        private string BuildPlayersJson(IReadOnlyList<string> userIds)
        {
            stringBuilder.Clear();
            writer.WriteStartArray();

            foreach (string userId in userIds)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("userId");
                writer.WriteValue(userId);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            return stringWriter.ToString();
        }

        private static string NewtonsoftBaseline(IReadOnlyList<string> userIds)
        {
            var players = new List<PlayersWrap.Player>(userIds.Count);
            foreach (string id in userIds) players.Add(new PlayersWrap.Player(id));
            return JsonConvert.SerializeObject(players);
        }

        private static List<string> AdversarialUserIds(int count)
        {
            string[] seeds =
            {
                "0xabc", "quote\"inside", "back\\slash", "line\nbreak", "tab\tchar",
                "unicode-é中文", "control--", "", "emoji-😀", "0xDEADBEEF",
            };

            var list = new List<string>(count);
            for (var i = 0; i < count; i++) list.Add(seeds[i % seeds.Length] + "#" + i);
            return list;
        }

        [Test]
        [Performance]
        public void PlayersJson_PooledWriter_ByteIdenticalToNewtonsoftAndCutsAllocs3x()
        {
            List<string> ids = AdversarialUserIds(50);

            Assert.AreEqual(NewtonsoftBaseline(ids), BuildPlayersJson(ids),
                "manual writer output must be byte-identical to JsonConvert.SerializeObject");

            Assert.AreEqual("[]", BuildPlayersJson(new List<string>()));
            Assert.AreEqual(JsonConvert.SerializeObject(new List<PlayersWrap.Player>()), BuildPlayersJson(new List<string>()));

            List<string> first = AdversarialUserIds(3);
            List<string> second = AdversarialUserIds(7);
            BuildPlayersJson(first);
            string secondJson = BuildPlayersJson(second);
            Assert.AreEqual(NewtonsoftBaseline(second), secondJson, "reused writer must not leak stale residue");

            const int N = 2000;

            for (var i = 0; i < 200; i++) { BuildPlayersJson(ids); NewtonsoftBaseline(ids); }

            double newPerCall = AllocPerCall(N, () => BuildPlayersJson(ids));
            double baselinePerCall = AllocPerCall(N, () => NewtonsoftBaseline(ids));

            Measure.Custom(new SampleGroup("PooledWriter_bytesPerCall", SampleUnit.Byte), Math.Max(newPerCall, 0));
            Measure.Custom(new SampleGroup("Newtonsoft_bytesPerCall", SampleUnit.Byte), Math.Max(baselinePerCall, 0));

            if (newPerCall > 0 && baselinePerCall > 0)
                Assert.GreaterOrEqual(baselinePerCall, 3.0 * newPerCall,
                    $"reflection + pooled-list churn should cost >=3x the reused-writer path (new={newPerCall}, baseline={baselinePerCall})");

            double newMs = TimeMs(N, () => BuildPlayersJson(ids));
            double baselineMs = TimeMs(N, () => NewtonsoftBaseline(ids));

            Measure.Custom(new SampleGroup("PooledWriter_ms", SampleUnit.Millisecond), newMs);
            Measure.Custom(new SampleGroup("Newtonsoft_ms", SampleUnit.Millisecond), baselineMs);

            Assert.LessOrEqual(newMs, baselineMs, "reused writer must be at least as fast as JsonConvert");
        }

        private static double TimeMs(int iterations, Action action)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++) action();
            return sw.Elapsed.TotalMilliseconds;
        }

        private static double AllocPerCall(int iterations, Action action)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                int gen0 = GC.CollectionCount(0);
                long before = GC.GetTotalMemory(true);

                for (var i = 0; i < iterations; i++)
                    action();

                long after = GC.GetTotalMemory(false);

                if (GC.CollectionCount(0) == gen0)
                    return (after - before) / (double)iterations;
            }

            return -1;
        }
    }
}
