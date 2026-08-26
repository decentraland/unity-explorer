using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Adds/removes <see cref="StudioFlyCamera"/> on the studio's live camera. Poll-based and
    /// studio-scene-gated like <see cref="StudioCardFrame"/> / StudioAvatarShaderSwitcher — the poll
    /// only needs to notice on/off + scene/play-mode transitions (cheap, coarse cadence is fine); the
    /// actual per-frame fly movement runs inside StudioFlyCamera's own Update(), driven by Unity's
    /// normal player loop so it stays smooth and keeps correct ordering against CinemachineBrain.
    /// Off by default — RMB is otherwise unclaimed in this tool, but this still opts in explicitly
    /// since it hijacks the camera's transform out from under Cinemachine while active.
    /// </summary>
    [InitializeOnLoad]
    public static class StudioFlyCameraController
    {
        private const string K_ENABLED = "OutfitStudio.FlyCamera.Enabled";
        private const string K_MOVE_SPEED = "OutfitStudio.FlyCamera.MoveSpeed";
        private const string K_LOOK_SPEED = "OutfitStudio.FlyCamera.LookSpeed";

        private static double _nextCheck;

        static StudioFlyCameraController() => EditorApplication.update += Update;

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(K_ENABLED, false);
            set { EditorPrefs.SetBool(K_ENABLED, value); Apply(); }
        }

        public static float MoveSpeed
        {
            get => EditorPrefs.GetFloat(K_MOVE_SPEED, 5f);
            set { EditorPrefs.SetFloat(K_MOVE_SPEED, value); Apply(); }
        }

        public static float LookSpeed
        {
            get => EditorPrefs.GetFloat(K_LOOK_SPEED, 0.15f);
            set { EditorPrefs.SetFloat(K_LOOK_SPEED, value); Apply(); }
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 0.5;
            Apply();
        }

        private static void Apply()
        {
            var inStudio = SceneManager.GetActiveScene().path == OutfitStudioWindow.STUDIO_SCENE_PATH;
            if (!inStudio || !Application.isPlaying || !Enabled)
            {
                Teardown();
                return;
            }

            var cam = StudioCardFrame.FindCamera();
            if (cam == null) return;

            var fly = cam.GetComponent<StudioFlyCamera>();
            if (fly == null) fly = cam.gameObject.AddComponent<StudioFlyCamera>();
            fly.MoveSpeed = MoveSpeed;
            fly.LookSpeed = LookSpeed;
        }

        private static void Teardown()
        {
            var cam = StudioCardFrame.FindCamera();
            var fly = cam != null ? cam.GetComponent<StudioFlyCamera>() : null;
            if (fly != null) Object.DestroyImmediate(fly);
        }

        /// <summary>Hands camera framing back to Cinemachine without disabling fly mode — wired to
        /// the Debug tab's "Reset View" button.</summary>
        public static void ResetView()
        {
            var cam = StudioCardFrame.FindCamera();
            var fly = cam != null ? cam.GetComponent<StudioFlyCamera>() : null;
            fly?.ReleaseToCinemachine();
        }
    }
}
