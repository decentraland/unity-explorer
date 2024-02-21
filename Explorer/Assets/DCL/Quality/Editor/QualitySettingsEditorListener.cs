using DCL.Quality.Runtime;
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;

namespace DCL.Quality
{
    [InitializeOnLoad]
    public static class QualitySettingsEditorListener
    {
        private static readonly IRendererFeaturesCache CACHE = new RendererFeaturesCache();

        private static IQualityLevelController qualityLevelController;

        static QualitySettingsEditorListener()
        {
            EditorApplication.playModeStateChanged += EditorApplicationPlayModeStateChanged;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EditorApplication.delayCall += TryStart;

            CompilationPipeline.compilationStarted += OnCompilationStarted;
        }

        private static void OnCompilationStarted(object obj)
        {
            Stop();
        }

        private static void TryStart()
        {
            string guid = AssetDatabase.FindAssets($"t: {nameof(QualitySettingsAsset)}").FirstOrDefault();

            if (guid == null)
                return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            QualitySettingsAsset asset = AssetDatabase.LoadAssetAtPath<QualitySettingsAsset>(path);

            if (asset == null)
                return;

            if (!asset.updateInEditor)
                return;

            Start(asset);
        }

        private static void EditorApplicationPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    Stop();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    TryStart();
                    break;
            }
        }

        internal static void Stop()
        {
            qualityLevelController?.Dispose();
            qualityLevelController = null;
        }

        internal static void Start(QualitySettingsAsset asset)
        {
            if (qualityLevelController != null)
                return;

            qualityLevelController = QualityRuntimeFactory.Create(CACHE, asset);
        }
    }
}
