using Preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Keeps the renderer's runtime overlay (zoom, switcher, emote controls, loader, debug panel)
    /// permanently hidden while the dedicated Outfit Studio scene is active — edit AND play mode,
    /// window open or not.
    ///
    /// The UI GameObject itself must stay alive: PreviewController.Reload() dereferences the
    /// presenter unconditionally, and mouse-drag rotation runs through the same UIDocument's
    /// Controls element — so only the visual widgets are display:None'd, on a cadence (the
    /// renderer re-shows some of them after every reload).
    /// </summary>
    [InitializeOnLoad]
    public static class StudioSceneOverlayHider
    {
        private static readonly string[] HIDDEN_ELEMENTS =
        {
            "DebugPanel", "ZoomControls", "Switcher", "EmoteControls", "Loader"
        };

        private static double _nextCheck;

        static StudioSceneOverlayHider()
        {
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 0.5;

            if (SceneManager.GetActiveScene().path != OutfitStudioWindow.STUDIO_SCENE_PATH) return;

            var presenter = Object.FindAnyObjectByType<PreviewUIPresenter>();
            if (presenter == null) return;

            var root = presenter.GetComponent<UIDocument>()?.rootVisualElement;
            if (root == null) return;

            foreach (var name in HIDDEN_ELEMENTS)
            {
                var element = root.Q(name);
                if (element != null) element.style.display = DisplayStyle.None;
            }
        }
    }
}
