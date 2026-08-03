using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using System.Globalization;
using UnityEngine;

namespace DCL.Profiling.ECS
{
    public partial class DebugViewCurrentSceneSystem
    {
        // Hardcoded scene limits derived from the parcel count (n), matching the documented
        // Decentraland scene limitations; colliders and external content have no documented
        // limit, so they use project-chosen caps.
        private const int MAX_TRIANGLES_PER_PARCEL = 10_000;
        private const int MAX_ENTITIES_PER_PARCEL = 200;
        private const int MAX_BODIES_PER_PARCEL = 300;
        private const int MAX_COLLIDERS_PER_PARCEL = 300;
        private const int MAX_MATERIALS_LOG2_MULTIPLIER = 20;
        private const int MAX_TEXTURES_LOG2_MULTIPLIER = 10;
        private const int MAX_GEOMETRIES_LOG2_MULTIPLIER = 200;
        private const int MAX_EXTERNAL_CONTENT = 10;

        private const float CAP_WARNING_PERCENT = 80f;

        private readonly struct SceneContentCaps
        {
            public readonly int Entities;
            public readonly long Triangles;
            public readonly int Bodies;
            public readonly int Geometries;
            public readonly int Materials;
            public readonly int Textures;
            public readonly int Colliders;
            public readonly int ExternalContent;

            private SceneContentCaps(int entities, long triangles, int bodies, int geometries, int materials, int textures, int colliders, int externalContent)
            {
                Entities = entities;
                Triangles = triangles;
                Bodies = bodies;
                Geometries = geometries;
                Materials = materials;
                Textures = textures;
                Colliders = colliders;
                ExternalContent = externalContent;
            }

            public static SceneContentCaps ForParcelCount(int parcelCount)
            {
                float log2 = Mathf.Log(parcelCount + 1, 2f);

                return new SceneContentCaps(
                    entities: parcelCount * MAX_ENTITIES_PER_PARCEL,
                    triangles: (long)parcelCount * MAX_TRIANGLES_PER_PARCEL,
                    bodies: parcelCount * MAX_BODIES_PER_PARCEL,
                    geometries: Mathf.FloorToInt(log2 * MAX_GEOMETRIES_LOG2_MULTIPLIER),
                    materials: Mathf.FloorToInt(log2 * MAX_MATERIALS_LOG2_MULTIPLIER),
                    textures: Mathf.FloorToInt(log2 * MAX_TEXTURES_LOG2_MULTIPLIER),
                    colliders: parcelCount * MAX_COLLIDERS_PER_PARCEL,
                    externalContent: MAX_EXTERNAL_CONTENT);
            }
        }

        private readonly struct ContentStatsBindings
        {
            public readonly ElementBinding<string> Entities;
            public readonly ElementBinding<string> Triangles;
            public readonly ElementBinding<string> Bodies;
            public readonly ElementBinding<string> Geometries;
            public readonly ElementBinding<string> Materials;
            public readonly ElementBinding<string> Textures;
            public readonly ElementBinding<string> Colliders;
            public readonly ElementBinding<string> ExternalContent;

            private ContentStatsBindings(
                ElementBinding<string> entities,
                ElementBinding<string> triangles,
                ElementBinding<string> bodies,
                ElementBinding<string> geometries,
                ElementBinding<string> materials,
                ElementBinding<string> textures,
                ElementBinding<string> colliders,
                ElementBinding<string> externalContent)
            {
                Entities = entities;
                Triangles = triangles;
                Bodies = bodies;
                Geometries = geometries;
                Materials = materials;
                Textures = textures;
                Colliders = colliders;
                ExternalContent = externalContent;
            }

            public static ContentStatsBindings Create() =>
                new (
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty));
        }

        private static void UpdateContentStatsBindings(in ContentStatsBindings bindings, SceneContentStats stats, in SceneContentCaps caps)
        {
            if (!stats.HasData)
            {
                bindings.Entities.Value = "—";
                bindings.Triangles.Value = "—";
                bindings.Bodies.Value = "—";
                bindings.Geometries.Value = "—";
                bindings.Materials.Value = "—";
                bindings.Textures.Value = "—";
                bindings.Colliders.Value = "—";
                bindings.ExternalContent.Value = "—";
                return;
            }

            bindings.Entities.Value = FormatCapped(stats.Entities, caps.Entities);
            bindings.Triangles.Value = FormatCapped(stats.Triangles, caps.Triangles);
            bindings.Bodies.Value = FormatCapped(stats.Bodies, caps.Bodies);
            bindings.Geometries.Value = FormatCapped(stats.Geometries, caps.Geometries);
            bindings.Materials.Value = FormatCapped(stats.Materials, caps.Materials);
            bindings.Textures.Value = FormatCapped(stats.Textures, caps.Textures);
            bindings.Colliders.Value = FormatCapped(stats.Colliders, caps.Colliders);
            bindings.ExternalContent.Value = FormatCapped(stats.ExternalContent, caps.ExternalContent);
        }

        private static string FormatCapped(long current, long cap)
        {
            if (cap <= 0)
                return current.ToString("N0", CultureInfo.InvariantCulture);

            float percent = current * 100f / cap;
            return $"<color={CapColor(percent)}>{current.ToString("N0", CultureInfo.InvariantCulture)} / {cap.ToString("N0", CultureInfo.InvariantCulture)} ({percent:F0}%)</color>";
        }

        private static string CapColor(float percent) =>
            percent switch
            {
                >= 100f => "red",
                >= CAP_WARNING_PERCENT => "yellow",
                _ => "green",
            };

        private readonly struct StringBindings
        {
            public readonly ElementBinding<string> RealFps;
            public readonly ElementBinding<string> MinFps;
            public readonly ElementBinding<string> MaxFps;
            public readonly ElementBinding<string> Hiccups;

            public readonly ElementBinding<string> BytesFromTotal;
            public readonly ElementBinding<string> BytesToTotal;
            public readonly ElementBinding<string> BytesFromPerSec;
            public readonly ElementBinding<string> BytesToPerSec;

            public readonly ElementBinding<string> MessagesFromTotal;
            public readonly ElementBinding<string> MessagesToTotal;
            public readonly ElementBinding<string> MessagesFromPerSec;
            public readonly ElementBinding<string> MessagesToPerSec;
            public readonly ElementBinding<string> MessagesFromMinMax;
            public readonly ElementBinding<string> MessagesToMinMax;
            public readonly ElementBinding<string> MessagesFromHiccups;
            public readonly ElementBinding<string> MessagesToHiccups;

            private StringBindings(
                ElementBinding<string> realFps,
                ElementBinding<string> minFps,
                ElementBinding<string> maxFps,
                ElementBinding<string> hiccups,
                ElementBinding<string> bytesFromTotal,
                ElementBinding<string> bytesToTotal,
                ElementBinding<string> bytesFromPerSec,
                ElementBinding<string> bytesToPerSec,
                ElementBinding<string> messagesFromTotal,
                ElementBinding<string> messagesToTotal,
                ElementBinding<string> messagesFromPerSec,
                ElementBinding<string> messagesToPerSec,
                ElementBinding<string> messagesFromMinMax,
                ElementBinding<string> messagesToMinMax,
                ElementBinding<string> messagesFromHiccups,
                ElementBinding<string> messagesToHiccups)
            {
                RealFps = realFps;
                MinFps = minFps;
                MaxFps = maxFps;
                Hiccups = hiccups;
                BytesFromTotal = bytesFromTotal;
                BytesToTotal = bytesToTotal;
                BytesFromPerSec = bytesFromPerSec;
                BytesToPerSec = bytesToPerSec;
                MessagesFromTotal = messagesFromTotal;
                MessagesToTotal = messagesToTotal;
                MessagesFromPerSec = messagesFromPerSec;
                MessagesToPerSec = messagesToPerSec;
                MessagesFromMinMax = messagesFromMinMax;
                MessagesToMinMax = messagesToMinMax;
                MessagesFromHiccups = messagesFromHiccups;
                MessagesToHiccups = messagesToHiccups;
            }

            public static StringBindings Create() =>
                new (
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty),
                    new ElementBinding<string>(string.Empty));
        }

        private static void PushSample(float[] ring, ref int writeIndex, ref int count, float value)
        {
            ring[writeIndex] = value;
            writeIndex = (writeIndex + 1) % ring.Length;
            if (count < ring.Length) count++;
        }

        private static void PopulatePerTickChart(SampledCounter counter, ElementBinding<LineChartBuffer> chart, float[] ring, long[] scratch)
        {
            int count = counter.CopySnapshot(scratch);

            for (var i = 0; i < count; i++)
                ring[i] = scratch[i];

            float displayValue = count > 0 ? ring[count - 1] : 0f;
            chart.SetAndUpdate(new LineChartBuffer(ring, 0, count, displayValue));
        }

        private static void ComputeTickFps(long[] scratch, int sampleCount, out float currentFps, out float minFpsValue, out float maxFpsValue, out int hiccupCount)
        {
            currentFps = 0f;
            minFpsValue = 0f;
            maxFpsValue = 0f;
            hiccupCount = 0;

            if (sampleCount == 0) return;

            long minNs = long.MaxValue;
            long maxNs = long.MinValue;
            long recentSumNs = 0;
            int recentCount = 0;
            int recentStart = sampleCount > RECENT_TICK_WINDOW ? sampleCount - RECENT_TICK_WINDOW : 0;

            for (var i = 0; i < sampleCount; i++)
            {
                long ns = scratch[i];
                if (ns <= 0) continue;
                if (ns < minNs) minNs = ns;
                if (ns > maxNs) maxNs = ns;
                if (ns > HICCUP_THRESHOLD_NS) hiccupCount++;

                if (i >= recentStart)
                {
                    recentSumNs += ns;
                    recentCount++;
                }
            }

            if (recentCount > 0)
                currentFps = 1e9f / ((float)recentSumNs / recentCount);

            // Shortest tick = highest FPS (Max FPS); longest tick = lowest FPS (Min FPS).
            if (minNs != long.MaxValue) maxFpsValue = 1e9f / minNs;
            if (maxNs != long.MinValue) minFpsValue = 1e9f / maxNs;
        }

        private static void UpdateStringBindings(in StringBindings bindings, SceneRuntimeMetrics metrics,
            float currentFpsValue, float minFpsValue, float maxFpsValue, int hiccupCount,
            long deltaBytesFrom, long deltaBytesTo, long deltaMessagesFrom, long deltaMessagesTo, float dt)
        {
            int target = metrics.TargetFps;
            string color = target > 0 && currentFpsValue + 1f < target ? "yellow" : "green";
            if (currentFpsValue is > 0f and < 15f) color = "red";

            bindings.RealFps.Value = target > 0
                ? $"<color={color}>{currentFpsValue:F1} fps (target {target})</color>"
                : $"{currentFpsValue:F1} fps";

            bindings.MinFps.Value = minFpsValue > 0 ? $"{minFpsValue:F1} fps" : "—";
            bindings.MaxFps.Value = maxFpsValue > 0 ? $"{maxFpsValue:F1} fps" : "—";

            bindings.Hiccups.Value = FormatMessageHiccups(hiccupCount);

            bindings.BytesFromTotal.Value = BytesFormatter.Normalize((ulong)Mathf.Max(0L, metrics.BytesFromScene.Total), false);
            bindings.BytesToTotal.Value = BytesFormatter.Normalize((ulong)Mathf.Max(0L, metrics.BytesToScene.Total), false);

            bindings.BytesFromPerSec.Value = BytesFormatter.Normalize((ulong)Mathf.Max(0f, deltaBytesFrom / dt), false) + "/s";
            bindings.BytesToPerSec.Value = BytesFormatter.Normalize((ulong)Mathf.Max(0f, deltaBytesTo / dt), false) + "/s";

            bindings.MessagesFromTotal.Value = metrics.MessagesFromScene.Total.ToString("N0", CultureInfo.InvariantCulture);
            bindings.MessagesToTotal.Value = metrics.MessagesToScene.Total.ToString("N0", CultureInfo.InvariantCulture);
            bindings.MessagesFromPerSec.Value = Mathf.Max(0f, deltaMessagesFrom / dt).ToString("F1", CultureInfo.InvariantCulture);
            bindings.MessagesToPerSec.Value = Mathf.Max(0f, deltaMessagesTo / dt).ToString("F1", CultureInfo.InvariantCulture);

            SampledCounter.Stats messagesFromStats = metrics.MessagesFromScene.ComputeDynamicStats(MESSAGE_HICCUP_MEAN_MULTIPLIER);
            bindings.MessagesFromMinMax.Value = messagesFromStats.Count > 0
                ? $"{messagesFromStats.Min} / {messagesFromStats.Max}"
                : "—";
            bindings.MessagesFromHiccups.Value = FormatMessageHiccups(messagesFromStats.Hiccups);

            SampledCounter.Stats messagesToStats = metrics.MessagesToScene.ComputeDynamicStats(MESSAGE_HICCUP_MEAN_MULTIPLIER);
            bindings.MessagesToMinMax.Value = messagesToStats.Count > 0
                ? $"{messagesToStats.Min} / {messagesToStats.Max}"
                : "—";
            bindings.MessagesToHiccups.Value = FormatMessageHiccups(messagesToStats.Hiccups);
        }

        private static string FormatMessageHiccups(int hiccupCount)
        {
            string color = hiccupCount switch
                           {
                               < 1 => "green",
                               < 5 => "yellow",
                               _ => "red",
                           };

            return $"<color={color}>{hiccupCount}</color>";
        }
    }
}
