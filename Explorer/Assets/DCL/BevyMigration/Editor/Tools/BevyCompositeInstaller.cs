using DCL.BevyMigration.Composite;
using DCL.Diagnostics;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DCL.BevyMigration.Editor
{
    /// <summary>
    ///     Adds <see cref="BevyCompositeFeature" /> to the project's URP renderer assets (BevyMigration/docs/README.md
    ///     § 4: "Hooks are the URP renderer asset ... and one bootstrap plugin"). Idempotent; mirrors what the URP
    ///     renderer inspector does when a feature is added by hand.
    ///     Batch: <c>Unity -batchmode -executeMethod DCL.BevyMigration.Editor.BevyCompositeInstaller.InstallBatch -quit</c>
    /// </summary>
    public static class BevyCompositeInstaller
    {
        private const string MENU = "Decentraland/Bevy Migration/Install Composite Feature";
        /// <summary>The game camera's renderer; the map renderer and the package default stay untouched.</summary>
        private static readonly string[] RENDERER_ASSETS = { "Assets/Rendering/ForwardRenderer - High.asset" };
        private const string FEATURES_PROPERTY = "m_RendererFeatures";
        private const string FEATURE_MAP_PROPERTY = "m_RendererFeatureMap";

        private static readonly ReportData REPORT_DATA = new (ReportCategory.ALWAYS);

        [MenuItem(MENU)]
        public static void Install()
        {
            var installed = 0;

            foreach (string path in RENDERER_ASSETS)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null) continue;

                if (InstallOn(data, path)) installed++;
            }

            AssetDatabase.SaveAssets();
            ReportHub.Log(REPORT_DATA, $"BevyCompositeFeature installed on {installed} renderer asset(s)");
        }

        public static void InstallBatch()
        {
            Install();
            EditorApplication.Exit(0);
        }

        private static bool InstallOn(UniversalRendererData data, string path)
        {
            List<ScriptableRendererFeature> features = data.rendererFeatures;

            foreach (ScriptableRendererFeature feature in features)
                if (feature is BevyCompositeFeature)
                    return false;

            var composite = ScriptableObject.CreateInstance<BevyCompositeFeature>();
            composite.name = nameof(BevyCompositeFeature);
            AssetDatabase.AddObjectToAsset(composite, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(composite, out string _, out long localId);

            var serialized = new SerializedObject(data);
            SerializedProperty list = serialized.FindProperty(FEATURES_PROPERTY);
            SerializedProperty map = serialized.FindProperty(FEATURE_MAP_PROPERTY);
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = composite;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            ReportHub.Log(REPORT_DATA, $"BevyCompositeFeature added to {path}");
            return true;
        }
    }
}
