// Drives the Editor from batch mode:
//   1. Open Assets/Scenes/Main.unity in Edit mode.
//   2. Enter Play mode.
//   3. Tick a few seconds (to let DCL boot up & stream a frame).
//   4. Capture the GameView via ScreenCapture.CaptureScreenshot.
//   5. Exit Play and quit.
//
// Invoked via:
//   Unity -projectPath ... -executeMethod \
//     GPUInstancerPro.Editor.PlayModeRunner.EnterPlayAndCapture
//
// Caveat: Unity batch + Play is fiddly — many internal subsystems opt out of
// running. If this fails we fall back to launching the editor interactively
// inside the sway session and capturing via grim from outside.

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GPUInstancerPro.Editor
{
    public static class PlayModeRunner
    {
        private const string SCENE_PATH = "Assets/Scenes/Main.unity";
        private const string OUTPUT_PNG = "/tmp/native-play.png";
        private const float SETTLE_SECONDS = 60f;

        private static double enteredAt;
        private static bool captured;

        [MenuItem("GPUInstancerPro/Enter Play + capture")]
        public static void EnterPlayAndCapture()
        {
            Debug.Log("[PlayModeRunner] start");
            if (!File.Exists(SCENE_PATH))
            {
                Debug.LogError($"[PlayModeRunner] scene missing: {SCENE_PATH}");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }
            EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.EnterPlaymode();
            Debug.Log("[PlayModeRunner] requested EnterPlaymode");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            Debug.Log($"[PlayModeRunner] play state -> {state}");
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                enteredAt = EditorApplication.timeSinceStartup;
                captured = false;
                EditorApplication.update += TickWhilePlaying;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= TickWhilePlaying;
                Debug.Log("[PlayModeRunner] back in edit mode, quitting");
                if (Application.isBatchMode) EditorApplication.Exit(captured ? 0 : 3);
            }
        }

        private static void TickWhilePlaying()
        {
            if (captured) return;
            double elapsed = EditorApplication.timeSinceStartup - enteredAt;
            if (elapsed < SETTLE_SECONDS) return;
            captured = true;
            Debug.Log($"[PlayModeRunner] settled after {elapsed:F1}s, capturing");
            ScreenCapture.CaptureScreenshot(OUTPUT_PNG);
            // Exit Play next tick — let the screenshot flush first.
            EditorApplication.delayCall += () => EditorApplication.ExitPlaymode();
        }
    }
}
