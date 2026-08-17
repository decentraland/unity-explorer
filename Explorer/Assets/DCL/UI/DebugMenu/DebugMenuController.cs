using DCL.Input;
using DCL.DebugUtilities;
using DCL.Profiling;
using DCL.UI.DebugMenu.LogHistory;
using ECS.SceneLifeCycle;
using ECS.StreamableLoading.AssetBundles;
using SceneRunner.Scene;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DCL.UI.DebugMenu
{
    [RequireComponent(typeof(UIDocument))]
    public class DebugMenuController : MonoBehaviour
    {
        private const string USS_SIDEBAR_BUTTON_SELECTED = "sidebar__button--selected";
        private const string USS_SIDEBAR_BUTTON_ATTENTION = "sidebar__button--attention";
        private const int METRICS_REFRESH_COOLDOWN_FRAMES = 30;
        private const float AB_PANEL_AUTO_CLOSE_LINGER_SECONDS = 3f;

        private readonly DebugMenuConsoleLogHistory logsHistory = new ();

        // Views and sidebar buttons are created in OnEnable, which Unity runs before Update,
        // OnDisable and any UI callback: they are only null before the first enable.
        private ConsolePanelView? consolePanelView;
        private AbConversionPanelView? abConversionPanelView;
        private MetricsPanelView? metricsPanelView;

        private DebugPanelView? visiblePanel;

        private IInputBlock? inputBlock;

        private Button? consoleButton;
        private Button? abConversionButton;
        private Button? metricsButton;
        private Button? debugPanelButton;

        private bool shouldRefreshConsole;
        private bool shouldHideDebugPanelOwnToggle;

        private IDebugContainerBuilder? debugContainerBuilder;
        private IScenesCache? scenesCache;

        private ISceneFacade? metricsScene;
        private SceneContentCaps metricsCaps;
        private int seenFailedConversions;
        private bool seenWarmUpFailure;
        private bool abPanelAutoOpened;
        private float abPanelAutoCloseAt = -1f;
        private int framesSinceMetricsRefresh = METRICS_REFRESH_COOLDOWN_FRAMES;
        private long lastMetricsCollectionCount = -1;

        private void OnEnable()
        {
            logsHistory.LogsUpdated += OnLogsUpdated;

            var root = GetComponent<UIDocument>().rootVisualElement;

            // Sidebar
            consoleButton = root.Q<Button>("ConsoleButton");
            abConversionButton = root.Q<Button>("AbConversionButton");
            metricsButton = root.Q<Button>("MetricsButton");
            debugPanelButton = root.Q<Button>("DebugPanelButton");

            consoleButton.clicked += OnConsoleButtonClicked;
            abConversionButton.clicked += OnAbConversionButtonClicked;
            metricsButton.clicked += OnMetricsButtonClicked;

            // debugPanelButton is wired and shown only while a builder exists: SetDebugContainerBuilder
            // covers the builder arriving after enable, this covers re-enables once the builder is set.
            if (debugContainerBuilder != null)
            {
                debugPanelButton.clicked += OnDebugPanelButtonClicked;
                debugPanelButton.style.display = DisplayStyle.Flex;
            }

            // Views
            consolePanelView = new ConsolePanelView(root.Q("ConsolePanel"), consoleButton, OnConsoleButtonClicked, logsHistory);

            // Null until Initialize runs; the view receives it in SetInputBlock then
            if (inputBlock != null)
                consolePanelView.SetInputBlock(inputBlock);

            abConversionPanelView = new AbConversionPanelView(root.Q("AbConversionPanel"), abConversionButton, OnAbConversionButtonClicked);
            metricsPanelView = new MetricsPanelView(root.Q("MetricsPanel"), metricsButton, OnMetricsButtonClicked);

            // Shortcuts
            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed += OnToggleConsoleShortcutPerformed;

            // Live reload
            if (visiblePanel != null)
                switch (visiblePanel)
                {
                    // We will add other debug panel views here
                    case ConsolePanelView:
                        consolePanelView.Toggle();
                        visiblePanel = consolePanelView;
                        break;
                    case AbConversionPanelView:
                        abConversionPanelView.Toggle();
                        visiblePanel = abConversionPanelView;
                        break;
                    case MetricsPanelView:
                        metricsPanelView.Toggle();
                        visiblePanel = metricsPanelView;
                        break;
                }
        }

        public void Initialize(IInputBlock newInputBlock, IDebugContainerBuilder newBuilder, IScenesCache newScenesCache)
        {
            SetInputBlock(newInputBlock);
            SetDebugContainerBuilder(newBuilder);
            scenesCache = newScenesCache;
        }

        private void SetDebugContainerBuilder(IDebugContainerBuilder builder)
        {
            debugContainerBuilder = builder;

            if (debugPanelButton != null)
            {
                // Panel handled at DebugContainerBuilder
                debugPanelButton.clicked -= OnDebugPanelButtonClicked;
                debugPanelButton.clicked += OnDebugPanelButtonClicked;
                debugPanelButton.style.display = DisplayStyle.Flex;
            }

            // DebugPanel has its own separate toggle button (that must still be used when the
            // DebugMenu is not enabled), so we must hide that one.
            shouldHideDebugPanelOwnToggle = true;
        }

        private void SetInputBlock(IInputBlock block)
        {
            this.inputBlock = block;
            consolePanelView?.SetInputBlock(block);
        }

        private void OnDisable()
        {
            logsHistory.LogsUpdated -= OnLogsUpdated;

            if (consoleButton != null)
                consoleButton.clicked -= OnConsoleButtonClicked;

            if (abConversionButton != null)
                abConversionButton.clicked -= OnAbConversionButtonClicked;

            if (metricsButton != null)
                metricsButton.clicked -= OnMetricsButtonClicked;

            if (debugPanelButton != null)
                debugPanelButton.clicked -= OnDebugPanelButtonClicked;

            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed -= OnToggleConsoleShortcutPerformed;

            if (metricsScene != null)
            {
                metricsScene.RuntimeMetrics.ContentStats.RequestedByMetricsPanel = false;
                metricsScene = null;
            }
        }

        private void Update()
        {
            if (shouldHideDebugPanelOwnToggle)
            {
                // Hide DebugPanel own toggle button when DebugMenu is available
                // Cannot be done at SetDebugContainerBuilder() due to Container being built
                // only AFTER all the Plugins are initialized...
                HideDebugPanelOwnToggle();
            }

            // Logs pushed from other threads since last frame become visible here; a resulting
            // LogsUpdated sets shouldRefreshConsole synchronously, so they render this same frame.
            logsHistory.DrainPendingLogs();

            if (shouldRefreshConsole)
            {
                shouldRefreshConsole = false;
                consolePanelView?.Refresh();
            }

            // Long-running abgen work (binary download, cold conversion) asks to be brought on screen.
            if (AbgenConversionMetrics.INSTANCE.TryConsumePanelOpenRequest() && abConversionPanelView is { Visible: false })
            {
                TogglePanel(abConversionPanelView);
                abPanelAutoOpened = true;
                abPanelAutoCloseAt = -1f;
            }

            // Cheap when nothing changed: a version check against the conversion metrics.
            abConversionPanelView?.Refresh();

            UpdateAbConversionAttention();
            UpdateMetricsPanel();
        }

        /// <summary>
        ///     A panel this controller opened on its own also closes on its own: once the warm-up ends
        ///     Ready with zero failed conversions, it lingers briefly (so the READY row is seen) and
        ///     closes. A failure — warm-up or any per-file one — keeps it open, and a manual toggle
        ///     hands the panel back to the user (see <see cref="OnAbConversionButtonClicked" />).
        /// </summary>
        private void UpdateAbPanelAutoClose(AbgenConversionMetrics.WarmUpStage warmUpStage, int failedConversions)
        {
            if (!abPanelAutoOpened) return;

            if (abConversionPanelView is not { Visible: true })
            {
                abPanelAutoOpened = false;
                return;
            }

            if (warmUpStage != AbgenConversionMetrics.WarmUpStage.Ready || failedConversions > 0)
            {
                abPanelAutoCloseAt = -1f;
                return;
            }

            if (abPanelAutoCloseAt < 0f)
                abPanelAutoCloseAt = UnityEngine.Time.unscaledTime + AB_PANEL_AUTO_CLOSE_LINGER_SECONDS;
            else if (UnityEngine.Time.unscaledTime >= abPanelAutoCloseAt)
            {
                TogglePanel(abConversionPanelView);
                abPanelAutoOpened = false;
            }
        }

        /// <summary>
        ///     Flashes the AB sidebar button while any abgen conversion is running (sidecar whole-scene
        ///     warm-up) and holds it lit after a failure until the panel is opened.
        ///     The class toggle pulses smoothly because sidebar__button transitions background-color.
        /// </summary>
        private void UpdateAbConversionAttention()
        {
            if (abConversionPanelView == null || abConversionButton == null)
                return;

            AbgenConversionMetrics metrics = AbgenConversionMetrics.INSTANCE;

            AbgenConversionMetrics.WarmUpStage warmUpStage = metrics.WarmUp;
            int failedConversions = metrics.Failed;
            bool warmUpFailed = warmUpStage == AbgenConversionMetrics.WarmUpStage.Failed;

            if (abConversionPanelView.Visible)
            {
                seenFailedConversions = failedConversions;
                seenWarmUpFailure = warmUpFailed;
            }
            else if (warmUpStage == AbgenConversionMetrics.WarmUpStage.Converting)
                seenWarmUpFailure = false; // a new warm-up can fail anew

            bool converting = warmUpStage == AbgenConversionMetrics.WarmUpStage.Converting || metrics.InFlight > 0;
            bool unseenFailure = failedConversions > seenFailedConversions || (warmUpFailed && !seenWarmUpFailure);

            bool attention = converting
                ? (int)(UnityEngine.Time.unscaledTime * 2f) % 2 == 0
                : unseenFailure;

            abConversionButton.EnableInClassList(USS_SIDEBAR_BUTTON_ATTENTION, attention);

            UpdateAbPanelAutoClose(warmUpStage, failedConversions);
        }

        private void UpdateMetricsPanel()
        {
            if (metricsPanelView == null)
                return;

            ISceneFacade? currentScene = scenesCache?.CurrentScene.Value;

            if (currentScene != metricsScene)
            {
                if (metricsScene != null)
                    metricsScene.RuntimeMetrics.ContentStats.RequestedByMetricsPanel = false;

                metricsScene = currentScene;

                if (currentScene != null)
                {
                    int parcelCount = currentScene.SceneData.Parcels.Count;
                    metricsCaps = SceneContentCaps.ForParcelCount(parcelCount);
                }
                else
                    metricsCaps = default(SceneContentCaps);

                // Prime the counter so the new scene's values show on the very next refresh check
                framesSinceMetricsRefresh = METRICS_REFRESH_COOLDOWN_FRAMES;
                lastMetricsCollectionCount = -1;

                SceneContentStatsFormatter.FormatEmpty(out SceneContentStatsText emptyText);
                metricsPanelView.UpdateValues(in emptyText);
            }

            if (currentScene == null) return;

            SceneContentStats stats = currentScene.RuntimeMetrics.ContentStats;
            stats.RequestedByMetricsPanel = metricsPanelView.Visible;

            if (!metricsPanelView.Visible) return;
            if (++framesSinceMetricsRefresh < METRICS_REFRESH_COOLDOWN_FRAMES) return;

            framesSinceMetricsRefresh = 0;

            if (stats.CollectionCount == lastMetricsCollectionCount) return;

            lastMetricsCollectionCount = stats.CollectionCount;
            SceneContentStatsFormatter.Format(stats, in metricsCaps, out SceneContentStatsText text);
            metricsPanelView.UpdateValues(in text);
        }

        private void HideDebugPanelOwnToggle()
        {
            try
            {
                // DebugContainerBuilder may throw InvalidOperationException during initialization
                debugContainerBuilder?.Container.HideToggleButton();
                shouldHideDebugPanelOwnToggle = false;
            }
            catch (Exception)
            {
                // If Container hasn't been built yet, it will be retried on the next frame because
                // shouldHideDebugPanelOwnToggle doesn't get reset when that happens
            }
        }

        public void PushLog(DebugMenuConsoleLogEntry logEntry)
        {
            logsHistory.AddLogMessage(logEntry);
        }

        private void OnConsoleButtonClicked() =>
            TogglePanel(consolePanelView);

        private void OnAbConversionButtonClicked()
        {
            // A manual toggle hands the panel back to the user: no pending auto-close survives it.
            abPanelAutoOpened = false;
            abPanelAutoCloseAt = -1f;
            TogglePanel(abConversionPanelView);
        }

        private void OnMetricsButtonClicked() =>
            TogglePanel(metricsPanelView);

        private void OnDebugPanelButtonClicked()
        {
            if (debugContainerBuilder == null) return;

            debugContainerBuilder.Container.TogglePanelVisibility();

            debugPanelButton?.EnableInClassList(USS_SIDEBAR_BUTTON_SELECTED, debugContainerBuilder.Container.IsPanelVisible());
        }

        private void TogglePanel(DebugPanelView? panelView)
        {
            if (panelView == null) return;

            if (panelView.Visible)
            {
                panelView.Toggle();
                visiblePanel = null;
            }
            else
            {
                visiblePanel?.Toggle();
                panelView.Toggle();
                visiblePanel = panelView;
            }
        }

        private void OnToggleConsoleShortcutPerformed(InputAction.CallbackContext obj) =>
            TogglePanel(consolePanelView);

        private void OnLogsUpdated()
        {
            if (consolePanelView is not { Visible: true }) return;
            shouldRefreshConsole = true;
        }
    }
}
