using DCL.DebugUtilities.Views;
using DCL.Diagnostics;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.Analytics.Metrics;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.WebRequests.Dumper.Editor
{
    public class WebRequestDumpRecorderWindow : EditorWindow
    {
        private const string FILTER_PREFS_KEY = "WebRequestDumper.Filter";

        private readonly RequestMetricRecorder[] activeMetrics = new RequestMetricRecorder[MetricsRegistry.TYPES.Length];

        private TextField filterField;
        private ListView metricsView;
        private Button restartButton;
        private Button saveButton;
        private Button stopResumeButton;

        private void OnEnable() =>
            EditorApplication.update += UpdateUI;

        private void OnDisable() =>
            EditorApplication.update -= UpdateUI;

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;

            // Title
            var title = new Label("Web Request Dump Recorder");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 15;
            root.Add(title);

            // Filter field
            filterField = new TextField("URL Filter (Regex)");
            filterField.style.marginBottom = 10;

            // Load filter from EditorPrefs
            string savedFilter = EditorPrefs.GetString(FILTER_PREFS_KEY, string.Empty);
            filterField.value = savedFilter;
            WebRequestsDumper.Instance.Filter = savedFilter;

            filterField.RegisterValueChangedCallback(evt =>
            {
                WebRequestsDumper.Instance.Filter = evt.newValue;
                EditorPrefs.SetString(FILTER_PREFS_KEY, evt.newValue);
            });

            root.Add(filterField);

            // Metrics List view
            const int METRIC_ELEMENT_HEIGHT = 20;

            metricsView = new ListView();
            metricsView.style.paddingLeft = 20;
            metricsView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            metricsView.fixedItemHeight = METRIC_ELEMENT_HEIGHT;
            metricsView.itemsSource = activeMetrics;
            metricsView.style.flexGrow = 0;
            metricsView.style.flexShrink = 0;
            metricsView.style.height = activeMetrics.Length * METRIC_ELEMENT_HEIGHT;

            metricsView.makeItem = () =>
            {
                var container = new VisualElement();
                container.style.flexDirection = FlexDirection.Row;

                var metricName = new Label("Metric Name");
                metricName.name = "MetricName";
                container.Add(metricName);

                var metricValue = new Label("Value");
                metricValue.name = "Value";
                container.Add(metricValue);

                return container;
            };

            metricsView.bindItem = (element, i) =>
            {
                Label metricName = element.Q<Label>("MetricName");
                Label metricValue = element.Q<Label>("Value");

                // Do aggregation
                metricName.text = MetricsRegistry.TYPES[i].Name;

                RequestMetricRecorder metric = activeMetrics[i];

                if (metric == null)
                {
                    metricValue.text = "N/A";
                    return;
                }

                metricValue.text = DebugLongMarkerElement.FormatValue(metric.GetMetric(), metric.GetUnit());
            };

            root.Add(metricsView);

            // Buttons container
            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            buttonsContainer.style.marginBottom = 10;
            root.Add(buttonsContainer);

            // Restart button
            restartButton = new Button(OnRestart);
            restartButton.tooltip = "Restart recording";
            GUIContent restartIcon = EditorGUIUtility.IconContent("Refresh");

            if (restartIcon?.image != null)
                restartButton.style.backgroundImage = new StyleBackground((Texture2D)restartIcon.image);

            restartButton.style.width = 30;
            restartButton.style.height = 30;
            restartButton.style.marginRight = 5;
            buttonsContainer.Add(restartButton);

            // Stop/Resume button
            stopResumeButton = new Button(OnStopResume);
            stopResumeButton.tooltip = "Stop recording";
            stopResumeButton.style.width = 30;
            stopResumeButton.style.height = 30;
            stopResumeButton.style.marginRight = 5;
            buttonsContainer.Add(stopResumeButton);

            // Save button
            saveButton = new Button(OnSave);
            saveButton.tooltip = "Save dump to file";
            GUIContent saveIcon = EditorGUIUtility.IconContent("SaveAs");

            if (saveIcon?.image != null)
                saveButton.style.backgroundImage = new StyleBackground((Texture2D)saveIcon.image);

            saveButton.style.width = 30;
            saveButton.style.height = 30;
            root.Add(saveButton);

            // Initial UI update
            UpdateUI();
        }

        [MenuItem("Decentraland/Web Requests Dumper")]
        public static void ShowWindow()
        {
            WebRequestDumpRecorderWindow window = GetWindow<WebRequestDumpRecorderWindow>("Web Requests Dumper");
            window.minSize = new Vector2(400, 250);
            window.Show();
        }

        private static Func<IReadOnlyList<RequestMetricBase>, ulong> AggregateMetrics(Type metricType)
        {
            if (metricType == typeof(ServeTimePerMBAverage) ||
                metricType == typeof(ServeTimeSmallFileAverage) ||
                metricType == typeof(TimeToFirstByteAverage))
                return metrics =>

                    // average
                    (ulong)metrics.Average(static m => (double)m.GetMetric());

            // Sum
            return metrics => (ulong)metrics.Sum(static m => (double)m.GetMetric());
        }

        private void UpdateUI()
        {
            if (metricsView == null || stopResumeButton == null) return;

            WebRequestsDumper dumper = WebRequestsDumper.Instance;

            // Update Metrics
            metricsView.RefreshItems();

            // Update stop/resume button icon and tooltip
            if (dumper.Enabled)
            {
                GUIContent stopIcon = EditorGUIUtility.IconContent("PreMatQuad");

                if (stopIcon?.image != null)
                    stopResumeButton.style.backgroundImage = new StyleBackground((Texture2D)stopIcon.image);

                stopResumeButton.tooltip = "Stop recording";
            }
            else
            {
                GUIContent resumeIcon = EditorGUIUtility.IconContent("Animation.Play");

                if (resumeIcon?.image != null)
                    stopResumeButton.style.backgroundImage = new StyleBackground((Texture2D)resumeIcon.image);

                stopResumeButton.tooltip = "Resume recording";
            }
        }

        private void OnRestart()
        {
            WebRequestsDumper dumper = WebRequestsDumper.Instance;
            dumper.Filter = filterField.value;
            dumper.Restart();

            if (dumper.AnalyticsContainer != null)
            {
                // Remove tracked metrics
                foreach (RequestMetricRecorder requestMetric in activeMetrics)
                {
                    if (requestMetric == null) continue;
                    dumper.AnalyticsContainer.RemoveFlatMetric(requestMetric);
                }
            }

            Array.Clear(activeMetrics, 0, activeMetrics.Length);

            // Recreate metrics to start over
            if (dumper.AnalyticsContainer != null) { CreateAnalytics(); }

            ReportHub.Log(ReportCategory.GENERIC_WEB_REQUEST, "Web Request Dumper: Recording restarted");
        }

        private void CreateAnalytics()
        {
            IDictionary<Type, Func<RequestMetricBase>> trackedMetrics = WebRequestsDumper.Instance.AnalyticsContainer!.GetTrackedMetrics();

            foreach ((Type type, Func<RequestMetricBase> ctor) in trackedMetrics)
            {
                var recorder = new RequestMetricRecorder(ctor());
                activeMetrics[MetricsRegistry.INDICES[type]] = recorder;
                WebRequestsDumper.Instance.AnalyticsContainer.AddFlatMetric(recorder);
            }
        }

        private void OnStopResume()
        {
            WebRequestsDumper dumper = WebRequestsDumper.Instance;

            if (dumper.Enabled)
            {
                dumper.Stop();

                ReportHub.Log(ReportCategory.GENERIC_WEB_REQUEST, "Web Request Dumper: Recording stopped");
            }
            else
            {
                dumper.Resume();

                if (activeMetrics.All(a => a == null))
                {
                    if (WebRequestsDumper.Instance.AnalyticsContainer != null)
                        CreateAnalytics();
                }

                ReportHub.Log(ReportCategory.GENERIC_WEB_REQUEST, "Web Request Dumper: Recording resumed");
            }
        }

        private void OnSave()
        {
            WebRequestsDumper dumper = WebRequestsDumper.Instance;

            string path = EditorUtility.SaveFilePanel(
                "Save Web Requests Dump",
                Application.dataPath,
                "web_requests_dump.json",
                "json"
            );

            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string serialized = dumper.Serialize();
                File.WriteAllText(path, serialized);
                ReportHub.Log(ReportCategory.GENERIC_WEB_REQUEST, $"Web Request Dumper: Dump saved to {path}");
                EditorUtility.DisplayDialog("Success", $"Dump saved successfully to:\n{path}", "OK");
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.GENERIC_WEB_REQUEST, $"Web Request Dumper: Failed to save dump - {ex}");
                EditorUtility.DisplayDialog("Error", $"Failed to save dump:\n{ex.Message}", "OK");
            }
        }
    }
}
