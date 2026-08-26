using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// While the Outfit Studio scene is active, overrides the render pipeline with
    /// URP_Asset_Studio (per-pixel additional lights, higher per-object limit) so multi-light
    /// cel-shaded "studio lighting" works. Restores the original pipeline when leaving the
    /// scene — the shipping URP_Asset and the WebGL build are never affected.
    ///
    /// Note: QualitySettings.renderPipeline is asset-backed; if the project is saved while the
    /// override is active, ProjectSettings/QualitySettings.asset shows a diff that disappears
    /// after leaving the studio scene and saving again — don't commit it.
    /// </summary>
    [InitializeOnLoad]
    public static class StudioRenderPipelineSwitcher
    {
        private const string STUDIO_PIPELINE_PATH = "Assets/OutfitStudio/Settings/URP_Asset_Studio.asset";

        private static RenderPipelineAsset _originalPipeline;
        private static bool _overridden;
        private static double _nextCheck;

        static StudioRenderPipelineSwitcher()
        {
            EditorApplication.update += Update;
            EditorApplication.quitting += Restore;
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 0.5;

            var inStudioScene = SceneManager.GetActiveScene().path == OutfitStudioWindow.STUDIO_SCENE_PATH;

            if (inStudioScene && !_overridden)
            {
                var studioPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(STUDIO_PIPELINE_PATH);
                if (studioPipeline == null) return;

                // After a domain reload mid-override the current pipeline may already BE the
                // studio asset — never cache it as the "original" or restore would leak it
                var current = QualitySettings.renderPipeline;
                _originalPipeline = current == studioPipeline ? null : current;

                QualitySettings.renderPipeline = studioPipeline;
                _overridden = true;
            }
            else if (!inStudioScene && _overridden)
            {
                Restore();
            }
        }

        private static void Restore()
        {
            if (!_overridden) return;

            QualitySettings.renderPipeline = _originalPipeline;
            _overridden = false;
        }
    }
}
