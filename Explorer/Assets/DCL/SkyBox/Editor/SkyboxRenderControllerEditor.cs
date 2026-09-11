using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DCL.SkyBox
{
    /// <summary>
    ///     Adds a time-of-day scrubber and a four-phase screenshot capture to the skybox controller inspector.
    ///     Both controls appear only while the authoring scene is playing with the controller in edit mode,
    ///     so the sky can be reviewed without launching the client.
    /// </summary>
    [CustomEditor(typeof(SkyboxRenderController))]
    public class SkyboxRenderControllerEditor : UnityEditor.Editor
    {
        private const string CAPTURE_FOLDER_PREF = "DCL.Skybox.Authoring.CaptureFolder";
        private const string CAPTURE_LABEL_PREF = "DCL.Skybox.Authoring.CaptureLabel";
        private const string CAPTURE_FREEZE_PREF = "DCL.Skybox.Authoring.CaptureFreezesShaderTime";
        private const string DEFAULT_CAPTURE_LABEL = "baseline";

        // The reflection cubemap is regenerated every 8 frames in 4 slices, so wait for a full cycle plus margin.
        private const int FRAMES_TO_SETTLE = 24;
        private const int FRAMES_BETWEEN_CAPTURES = 4;

        private static readonly (string Name, float TimeOfDay)[] PHASES =
        {
            ("day", 0.5f),
            ("sunset", 0.77f),
            ("night", 0f),
            ("sunrise", 0.23f),
        };

        private float timeOfDay = 0.5f;
        private bool capturing;
        private bool shaderTimeFrozen;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var controller = (SkyboxRenderController)target;

            if (!Application.isPlaying || !controller.editMode)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Authoring", EditorStyles.boldLabel);

            float newTimeOfDay = EditorGUILayout.Slider($"Time of day ({FormatTime(timeOfDay)})", timeOfDay, 0f, 1f);

            if (!Mathf.Approximately(newTimeOfDay, timeOfDay))
            {
                timeOfDay = newTimeOfDay;
                controller.UpdateSkybox(timeOfDay);
            }

            EditorGUILayout.LabelField("Phase (palette position)", controller.Preset.EvaluatePhase(timeOfDay).ToString("0.000"));

            EditorGUILayout.BeginHorizontal();

            for (var i = 0; i < PHASES.Length; i++)
            {
                if (GUILayout.Button(PHASES[i].Name))
                {
                    timeOfDay = PHASES[i].TimeOfDay;
                    controller.UpdateSkybox(timeOfDay);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            string label = EditorGUILayout.TextField("Capture label", EditorPrefs.GetString(CAPTURE_LABEL_PREF, DEFAULT_CAPTURE_LABEL));
            EditorPrefs.SetString(CAPTURE_LABEL_PREF, label);

            string folder = EditorPrefs.GetString(CAPTURE_FOLDER_PREF, string.Empty);
            EditorGUILayout.LabelField("Capture folder", string.IsNullOrEmpty(folder) ? "(asked on first capture)" : folder);

            bool freeze = EditorGUILayout.Toggle(
                new GUIContent("Freeze shader time", "Stops cloud and star rotation before capturing so captures are pixel-comparable. Stays frozen until Play is restarted."),
                EditorPrefs.GetBool(CAPTURE_FREEZE_PREF, true));

            EditorPrefs.SetBool(CAPTURE_FREEZE_PREF, freeze);

            if (shaderTimeFrozen)
                EditorGUILayout.HelpBox("Shader time is frozen for this Play session.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(capturing))
            {
                if (GUILayout.Button("Capture 4 phases"))
                {
                    if (freeze && !shaderTimeFrozen)
                    {
                        controller.DisableSkyboxTime();
                        shaderTimeFrozen = true;
                    }

                    CaptureAllPhasesAsync(controller, label).Forget();
                }
            }

            if (GUILayout.Button("Change folder", GUILayout.Width(110)))
                PickCaptureFolder();

            EditorGUILayout.EndHorizontal();
        }

        private async UniTaskVoid CaptureAllPhasesAsync(SkyboxRenderController controller, string label)
        {
            string folder = EditorPrefs.GetString(CAPTURE_FOLDER_PREF, string.Empty);

            if (string.IsNullOrEmpty(folder) && !PickCaptureFolder())
                return;

            folder = EditorPrefs.GetString(CAPTURE_FOLDER_PREF, string.Empty);
            capturing = true;

            try
            {
                for (var i = 0; i < PHASES.Length; i++)
                {
                    // Leaving play mode destroys the controller; stop instead of touching a dead object.
                    if (controller == null)
                        return;

                    timeOfDay = PHASES[i].TimeOfDay;
                    controller.UpdateSkybox(timeOfDay);
                    Repaint();

                    await UniTask.DelayFrame(FRAMES_TO_SETTLE);

                    string path = Path.Combine(folder, $"{label}_{PHASES[i].Name}.png");
                    ScreenCapture.CaptureScreenshot(path);

                    await UniTask.DelayFrame(FRAMES_BETWEEN_CAPTURES);
                }

                ReportHub.Log(ReportCategory.SKYBOX, $"Skybox authoring: captured {PHASES.Length} phases as \"{label}_*.png\" in {folder}");
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.SKYBOX); }
            finally
            {
                capturing = false;
                Repaint();
            }
        }

        private static bool PickCaptureFolder()
        {
            string current = EditorPrefs.GetString(CAPTURE_FOLDER_PREF, string.Empty);
            string picked = EditorUtility.OpenFolderPanel("Skybox capture folder", current, string.Empty);

            if (string.IsNullOrEmpty(picked))
                return false;

            EditorPrefs.SetString(CAPTURE_FOLDER_PREF, picked);
            return true;
        }

        private static string FormatTime(float normalizedTime)
        {
            int totalMinutes = Mathf.RoundToInt(normalizedTime * 24f * 60f) % (24 * 60);
            return $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";
        }
    }
}
