using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Utilities;
using SceneRunner.Scene;
using System.Globalization;
using UnityEngine;

namespace DCL.Profiling.ECS
{
    public partial class DebugViewCurrentSceneSystem
    {
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
