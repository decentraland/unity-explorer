using DCL.Diagnostics;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.WebRequests.Dumper.Editor
{
    public class WebRequestDumpRecorderWindow : EditorWindow
    {
        private const string FILTER_PREFS_KEY = "WebRequestDumper.Filter";

        private TextField filterField;
        private Button restartButton;
        private Button stopResumeButton;
        private Label counterLabel;
        private Button saveButton;

        [MenuItem("Decentraland/Web Requests Dumper")]
        public static void ShowWindow()
        {
            WebRequestDumpRecorderWindow window = GetWindow<WebRequestDumpRecorderWindow>("Web Requests Dumper");
            window.minSize = new Vector2(400, 250);
            window.Show();
        }

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

            // Counter field
            counterLabel = new Label("Recorded Requests: 0");
            counterLabel.style.marginBottom = 15;
            counterLabel.style.fontSize = 12;
            root.Add(counterLabel);

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

        private void OnEnable() =>
            EditorApplication.update += UpdateUI;

        private void OnDisable() =>
            EditorApplication.update -= UpdateUI;

        private void UpdateUI()
        {
            if (counterLabel == null || stopResumeButton == null) return;

            WebRequestsDumper dumper = WebRequestsDumper.Instance;

            // Update counter
            counterLabel.text = $"Recorded Requests: {dumper.Count}";

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
            Debug.Log("Web Request Dumper: Recording restarted");
        }

        private void OnStopResume()
        {
            WebRequestsDumper dumper = WebRequestsDumper.Instance;

            if (dumper.Enabled)
            {
                dumper.Stop();
                Debug.Log("Web Request Dumper: Recording stopped");
            }
            else
            {
                dumper.Resume();
                Debug.Log("Web Request Dumper: Recording resumed");
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
