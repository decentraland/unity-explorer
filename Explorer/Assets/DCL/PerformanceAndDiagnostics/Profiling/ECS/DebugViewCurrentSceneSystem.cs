using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System;
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

        private readonly StringBindings stringBindings;

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
            stringBindings = StringBindings.Create();

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
                         .AddCustomMarker("Real tick FPS:", stringBindings.RealFps)
                         .AddCustomMarker("Min FPS (last 256 ticks):", stringBindings.MinFps)
                         .AddCustomMarker("Max FPS (last 256 ticks):", stringBindings.MaxFps)
                         .AddCustomMarker("Hiccups (last 256 ticks):", stringBindings.Hiccups)
                         .AddControl(new DebugLineChartDef(fpsChart, "Tick FPS", new Color(0.18f, 0.80f, 0.44f), DebugLongMarkerDef.Unit.NoFormat), null)
                         .AddCustomMarker("Bytes from scene:", stringBindings.BytesFromTotal)
                         .AddCustomMarker("Bytes/s from scene:", stringBindings.BytesFromPerSec)
                         .AddControl(new DebugLineChartDef(bytesFromChart, "Bytes/tick from scene", new Color(0.20f, 0.60f, 0.86f), DebugLongMarkerDef.Unit.Bytes), null)
                         .AddCustomMarker("Msgs from scene:", stringBindings.MessagesFromTotal)
                         .AddCustomMarker("Msgs/s from scene:", stringBindings.MessagesFromPerSec)
                         .AddCustomMarker("Msgs/call min/max from scene:", stringBindings.MessagesFromMinMax)
                         .AddCustomMarker("Msg hiccups from scene:", stringBindings.MessagesFromHiccups)
                         .AddControl(new DebugLineChartDef(messagesFromChart, "Msgs/tick from scene", new Color(0.40f, 0.80f, 0.95f), DebugLongMarkerDef.Unit.NoFormat), null)
                         .AddCustomMarker("Bytes to scene:", stringBindings.BytesToTotal)
                         .AddCustomMarker("Bytes/s to scene:", stringBindings.BytesToPerSec)
                         .AddControl(new DebugLineChartDef(bytesToChart, "Bytes/tick to scene", new Color(0.91f, 0.30f, 0.55f), DebugLongMarkerDef.Unit.Bytes), null)
                         .AddCustomMarker("Msgs to scene:", stringBindings.MessagesToTotal)
                         .AddCustomMarker("Msgs/s to scene:", stringBindings.MessagesToPerSec)
                         .AddCustomMarker("Msgs/call min/max to scene:", stringBindings.MessagesToMinMax)
                         .AddCustomMarker("Msg hiccups to scene:", stringBindings.MessagesToHiccups)
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
            ComputeTickFps(longScratch, tickSampleCount, out float currentFpsValue, out float minFpsValue, out float maxFpsValue, out int hiccupCount);

            PushSample(fpsRing, ref fpsRingIndex, ref fpsRingCount, currentFpsValue);
            fpsChart.SetAndUpdate(new LineChartBuffer(fpsRing, fpsRingIndex, fpsRingCount, currentFpsValue));

            PopulatePerTickChart(metrics.BytesFromScene, bytesFromChart, bytesFromRing, longScratch);
            PopulatePerTickChart(metrics.BytesToScene, bytesToChart, bytesToRing, longScratch);
            PopulatePerTickChart(metrics.MessagesFromScene, messagesFromChart, messagesFromRing, longScratch);
            PopulatePerTickChart(metrics.MessagesToScene, messagesToChart, messagesToRing, longScratch);

            if (++framesSinceMetricsUpdate >= FRAME_STATS_COOLDOWN)
            {
                framesSinceMetricsUpdate = 0;
                UpdateStringBindings(in stringBindings, metrics, currentFpsValue, minFpsValue, maxFpsValue, hiccupCount,
                    deltaBytesFrom, deltaBytesTo, deltaMessagesFrom, deltaMessagesTo, dt);
            }
        }

        protected override void OnDispose()
        {
            if (onCurrentSceneChanged != null)
                scenesCache.CurrentScene.OnUpdate -= onCurrentSceneChanged;
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
    }
}
