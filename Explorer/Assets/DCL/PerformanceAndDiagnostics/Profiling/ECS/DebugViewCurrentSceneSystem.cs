using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Utilities;
using ECS;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System;
using System.Globalization;
using UnityEngine;

namespace DCL.Profiling.ECS
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class DebugViewCurrentSceneSystem : BaseUnityLoopSystem
    {
        private const int FPS_CHART_CAPACITY = 120;
        private const int TICK_CHART_CAPACITY = SampledCounter.BUFFER_CAPACITY;
        private const int FRAME_STATS_COOLDOWN = 30;
        private const int RECENT_TICK_WINDOW = 32;
        private const long HICCUP_THRESHOLD_NS = 50_000_000L; // 50 ms ~ 20 FPS
        private const float MESSAGE_HICCUP_MEAN_MULTIPLIER = 2f; // a tick that produces > 2x avg messages is a spike

        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;

        private readonly bool widgetEnabled;

        private readonly DebugWidgetVisibilityBinding visibility;

        private readonly ElementBinding<string> realFps;
        private readonly ElementBinding<string> minFps;
        private readonly ElementBinding<string> maxFps;
        private readonly ElementBinding<string> hiccupsBinding;

        private readonly ElementBinding<string> bytesFromTotal;
        private readonly ElementBinding<string> bytesToTotal;
        private readonly ElementBinding<string> bytesFromPerSec;
        private readonly ElementBinding<string> bytesToPerSec;

        private readonly ElementBinding<string> messagesFromTotal;
        private readonly ElementBinding<string> messagesToTotal;
        private readonly ElementBinding<string> messagesFromPerSec;
        private readonly ElementBinding<string> messagesToPerSec;
        private readonly ElementBinding<string> messagesFromMinMax;
        private readonly ElementBinding<string> messagesToMinMax;
        private readonly ElementBinding<string> messagesFromHiccups;
        private readonly ElementBinding<string> messagesToHiccups;

        private readonly ElementBinding<LineChartBuffer> fpsChart;
        private readonly ElementBinding<LineChartBuffer> bytesFromChart;
        private readonly ElementBinding<LineChartBuffer> bytesToChart;
        private readonly ElementBinding<LineChartBuffer> messagesFromChart;
        private readonly ElementBinding<LineChartBuffer> messagesToChart;

        private readonly long[] longScratch = new long[SampledCounter.BUFFER_CAPACITY];
        private readonly float[] fpsRing = new float[FPS_CHART_CAPACITY];
        private readonly float[] bytesFromRing = new float[TICK_CHART_CAPACITY];
        private readonly float[] bytesToRing = new float[TICK_CHART_CAPACITY];
        private readonly float[] messagesFromRing = new float[TICK_CHART_CAPACITY];
        private readonly float[] messagesToRing = new float[TICK_CHART_CAPACITY];

        private int fpsRingIndex;
        private int fpsRingCount;

        private readonly Action<ISceneFacade?>? onCurrentSceneChanged;

        private ISceneFacade? currentScene;
        private long lastBytesFromScene;
        private long lastBytesToScene;
        private long lastMessagesFromScene;
        private long lastMessagesToScene;
        private float lastSampleTime;
        private int framesSinceMetricsUpdate;

        internal DebugViewCurrentSceneSystem(World world, IDebugContainerBuilder debugBuilder, IScenesCache scenesCache, IRealmData realmData) : base(world)
        {
            this.realmData = realmData;
            this.scenesCache = scenesCache;

            visibility = new DebugWidgetVisibilityBinding(true);
            realFps = new ElementBinding<string>(string.Empty);
            minFps = new ElementBinding<string>(string.Empty);
            maxFps = new ElementBinding<string>(string.Empty);
            hiccupsBinding = new ElementBinding<string>(string.Empty);

            bytesFromTotal = new ElementBinding<string>(string.Empty);
            bytesToTotal = new ElementBinding<string>(string.Empty);
            bytesFromPerSec = new ElementBinding<string>(string.Empty);
            bytesToPerSec = new ElementBinding<string>(string.Empty);

            messagesFromTotal = new ElementBinding<string>(string.Empty);
            messagesToTotal = new ElementBinding<string>(string.Empty);
            messagesFromPerSec = new ElementBinding<string>(string.Empty);
            messagesToPerSec = new ElementBinding<string>(string.Empty);
            messagesFromMinMax = new ElementBinding<string>(string.Empty);
            messagesToMinMax = new ElementBinding<string>(string.Empty);
            messagesFromHiccups = new ElementBinding<string>(string.Empty);
            messagesToHiccups = new ElementBinding<string>(string.Empty);

            fpsChart = new ElementBinding<LineChartBuffer>(new LineChartBuffer(fpsRing, 0, 0, 0));
            bytesFromChart = new ElementBinding<LineChartBuffer>(new LineChartBuffer(bytesFromRing, 0, 0, 0));
            bytesToChart = new ElementBinding<LineChartBuffer>(new LineChartBuffer(bytesToRing, 0, 0, 0));
            messagesFromChart = new ElementBinding<LineChartBuffer>(new LineChartBuffer(messagesFromRing, 0, 0, 0));
            messagesToChart = new ElementBinding<LineChartBuffer>(new LineChartBuffer(messagesToRing, 0, 0, 0));

            DebugWidgetBuilder? widgetBuilder = debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.CURRENT_SCENE);

            if (widgetBuilder == null)
                return;

            widgetEnabled = true;

            onCurrentSceneChanged = OnCurrentSceneChanged;
            scenesCache.CurrentScene.OnUpdate += onCurrentSceneChanged;
            OnCurrentSceneChanged(scenesCache.CurrentScene.Value);

            widgetBuilder.SetVisibilityBinding(visibility)
                         .AddCustomMarker("Real tick FPS:", realFps)
                         .AddCustomMarker("Min FPS (last 256 ticks):", minFps)
                         .AddCustomMarker("Max FPS (last 256 ticks):", maxFps)
                         .AddCustomMarker("Hiccups (last 256 ticks):", hiccupsBinding)
                         .AddControl(new DebugLineChartDef(fpsChart, "Tick FPS", new Color(0.18f, 0.80f, 0.44f), DebugLongMarkerDef.Unit.NoFormat), null)
                         .AddCustomMarker("Bytes from scene:", bytesFromTotal)
                         .AddCustomMarker("Bytes/s from scene:", bytesFromPerSec)
                         .AddControl(new DebugLineChartDef(bytesFromChart, "Bytes/tick from scene", new Color(0.20f, 0.60f, 0.86f), DebugLongMarkerDef.Unit.Bytes), null)
                         .AddCustomMarker("Msgs from scene:", messagesFromTotal)
                         .AddCustomMarker("Msgs/s from scene:", messagesFromPerSec)
                         .AddCustomMarker("Msgs/call min/max from scene:", messagesFromMinMax)
                         .AddCustomMarker("Msg hiccups from scene:", messagesFromHiccups)
                         .AddControl(new DebugLineChartDef(messagesFromChart, "Msgs/tick from scene", new Color(0.40f, 0.80f, 0.95f), DebugLongMarkerDef.Unit.NoFormat), null)
                         .AddCustomMarker("Bytes to scene:", bytesToTotal)
                         .AddCustomMarker("Bytes/s to scene:", bytesToPerSec)
                         .AddControl(new DebugLineChartDef(bytesToChart, "Bytes/tick to scene", new Color(0.91f, 0.30f, 0.55f), DebugLongMarkerDef.Unit.Bytes), null)
                         .AddCustomMarker("Msgs to scene:", messagesToTotal)
                         .AddCustomMarker("Msgs/s to scene:", messagesToPerSec)
                         .AddCustomMarker("Msgs/call min/max to scene:", messagesToMinMax)
                         .AddCustomMarker("Msg hiccups to scene:", messagesToHiccups)
                         .AddControl(new DebugLineChartDef(messagesToChart, "Msgs/tick to scene", new Color(0.98f, 0.55f, 0.75f), DebugLongMarkerDef.Unit.NoFormat), null);
        }

        protected override void Update(float t)
        {
            if (!widgetEnabled) return;
            if (!realmData.Configured) return;
            if (!visibility.IsConnectedAndExpanded) return;
            if (currentScene == null) return;

            SceneRuntimeMetrics metrics = currentScene.RuntimeMetrics;

            long bytesFrom = metrics.BytesFromScene.Total;
            long bytesTo = metrics.BytesToScene.Total;
            long messagesFrom = metrics.MessagesFromScene.Total;
            long messagesTo = metrics.MessagesToScene.Total;

            float now = UnityEngine.Time.unscaledTime;
            float dt = Mathf.Max(1e-3f, now - lastSampleTime);

            long deltaBytesFrom = bytesFrom - lastBytesFromScene;
            long deltaBytesTo = bytesTo - lastBytesToScene;
            long deltaMessagesFrom = messagesFrom - lastMessagesFromScene;
            long deltaMessagesTo = messagesTo - lastMessagesToScene;

            lastBytesFromScene = bytesFrom;
            lastBytesToScene = bytesTo;
            lastMessagesFromScene = messagesFrom;
            lastMessagesToScene = messagesTo;
            lastSampleTime = now;

            int tickSampleCount = metrics.TickTimesNs.CopySnapshot(longScratch);
            ComputeTickFps(tickSampleCount, out float currentFpsValue, out float minFpsValue, out float maxFpsValue, out int hiccupCount);

            PushSample(fpsRing, ref fpsRingIndex, ref fpsRingCount, currentFpsValue);
            fpsChart.SetAndUpdate(new LineChartBuffer(fpsRing, fpsRingIndex, fpsRingCount, currentFpsValue));

            PopulatePerTickChart(metrics.BytesFromScene, bytesFromChart, bytesFromRing);
            PopulatePerTickChart(metrics.BytesToScene, bytesToChart, bytesToRing);
            PopulatePerTickChart(metrics.MessagesFromScene, messagesFromChart, messagesFromRing);
            PopulatePerTickChart(metrics.MessagesToScene, messagesToChart, messagesToRing);

            if (++framesSinceMetricsUpdate >= FRAME_STATS_COOLDOWN)
            {
                framesSinceMetricsUpdate = 0;
                UpdateStringBindings(metrics, currentFpsValue, minFpsValue, maxFpsValue, hiccupCount,
                    deltaBytesFrom, deltaBytesTo, deltaMessagesFrom, deltaMessagesTo, dt);
            }
        }

        protected override void OnDispose()
        {
            if (onCurrentSceneChanged != null)
                scenesCache.CurrentScene.OnUpdate -= onCurrentSceneChanged;
        }

        private static void PushSample(float[] ring, ref int writeIndex, ref int count, float value)
        {
            ring[writeIndex] = value;
            writeIndex = (writeIndex + 1) % ring.Length;
            if (count < ring.Length) count++;
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

        private void OnCurrentSceneChanged(ISceneFacade? scene)
        {
            currentScene = scene;
            ResetLocalState();

            if (scene != null)
            {
                SceneRuntimeMetrics initial = scene.RuntimeMetrics;
                lastBytesFromScene = initial.BytesFromScene.Total;
                lastBytesToScene = initial.BytesToScene.Total;
                lastMessagesFromScene = initial.MessagesFromScene.Total;
                lastMessagesToScene = initial.MessagesToScene.Total;
            }

            lastSampleTime = UnityEngine.Time.unscaledTime;
        }

        private void ResetLocalState()
        {
            fpsRingIndex = fpsRingCount = 0;
            lastBytesFromScene = lastBytesToScene = lastMessagesFromScene = lastMessagesToScene = 0;
            framesSinceMetricsUpdate = 0;

            // Stage the cleared buffer; flush happens via SetAndUpdate in Update once the binding is connected.
            fpsChart.Value = new LineChartBuffer(fpsRing, 0, 0, 0);
            bytesFromChart.Value = new LineChartBuffer(bytesFromRing, 0, 0, 0);
            bytesToChart.Value = new LineChartBuffer(bytesToRing, 0, 0, 0);
            messagesFromChart.Value = new LineChartBuffer(messagesFromRing, 0, 0, 0);
            messagesToChart.Value = new LineChartBuffer(messagesToRing, 0, 0, 0);
        }

        private void PopulatePerTickChart(SampledCounter counter, ElementBinding<LineChartBuffer> chart, float[] ring)
        {
            int count = counter.CopySnapshot(longScratch);

            for (var i = 0; i < count; i++)
                ring[i] = longScratch[i];

            float displayValue = count > 0 ? ring[count - 1] : 0f;
            chart.SetAndUpdate(new LineChartBuffer(ring, 0, count, displayValue));
        }

        private void ComputeTickFps(int sampleCount, out float currentFps, out float minFpsValue, out float maxFpsValue, out int hiccupCount)
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
                long ns = longScratch[i];
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

        private void UpdateStringBindings(SceneRuntimeMetrics metrics, float currentFpsValue, float minFpsValue, float maxFpsValue, int hiccupCount,
            long deltaBytesFrom, long deltaBytesTo, long deltaMessagesFrom, long deltaMessagesTo, float dt)
        {
            int target = metrics.TargetFps;
            string color = target > 0 && currentFpsValue + 1f < target ? "yellow" : "green";
            if (currentFpsValue is > 0f and < 15f) color = "red";

            realFps.Value = target > 0
                ? $"<color={color}>{currentFpsValue:F1} fps (target {target})</color>"
                : $"{currentFpsValue:F1} fps";

            minFps.Value = minFpsValue > 0 ? $"{minFpsValue:F1} fps" : "—";
            maxFps.Value = maxFpsValue > 0 ? $"{maxFpsValue:F1} fps" : "—";

            string hiccupColor = hiccupCount switch
                                 {
                                     < 1 => "green",
                                     < 5 => "yellow",
                                     _ => "red",
                                 };

            hiccupsBinding.Value = $"<color={hiccupColor}>{hiccupCount}</color>";

            bytesFromTotal.Value = BytesFormatter.Normalize((ulong)metrics.BytesFromScene.Total, false);
            bytesToTotal.Value = BytesFormatter.Normalize((ulong)metrics.BytesToScene.Total, false);

            bytesFromPerSec.Value = BytesFormatter.Normalize((ulong)Mathf.Max(0f, deltaBytesFrom / dt), false) + "/s";
            bytesToPerSec.Value = BytesFormatter.Normalize((ulong)Mathf.Max(0f, deltaBytesTo / dt), false) + "/s";

            messagesFromTotal.Value = metrics.MessagesFromScene.Total.ToString("N0", CultureInfo.InvariantCulture);
            messagesToTotal.Value = metrics.MessagesToScene.Total.ToString("N0", CultureInfo.InvariantCulture);
            messagesFromPerSec.Value = (deltaMessagesFrom / dt).ToString("F1", CultureInfo.InvariantCulture);
            messagesToPerSec.Value = (deltaMessagesTo / dt).ToString("F1", CultureInfo.InvariantCulture);

            SampledCounter.Stats messagesFromStats = metrics.MessagesFromScene.ComputeDynamicStats(MESSAGE_HICCUP_MEAN_MULTIPLIER);
            messagesFromMinMax.Value = messagesFromStats.Count > 0
                ? $"{messagesFromStats.Min} / {messagesFromStats.Max}"
                : "—";
            messagesFromHiccups.Value = FormatMessageHiccups(messagesFromStats.Hiccups);

            SampledCounter.Stats messagesToStats = metrics.MessagesToScene.ComputeDynamicStats(MESSAGE_HICCUP_MEAN_MULTIPLIER);
            messagesToMinMax.Value = messagesToStats.Count > 0
                ? $"{messagesToStats.Min} / {messagesToStats.Max}"
                : "—";
            messagesToHiccups.Value = FormatMessageHiccups(messagesToStats.Hiccups);
        }
    }
}
