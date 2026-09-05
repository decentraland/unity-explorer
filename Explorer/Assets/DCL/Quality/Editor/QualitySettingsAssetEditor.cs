using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.Universal;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace DCL.Quality
{
    [CustomEditor(typeof(QualitySettingsAsset))]
    public partial class QualitySettingsAssetEditor : UnityEditor.Editor
    {
        private readonly HashSet<ScriptableRendererFeature> tempFeatures = new ();
        private SerializedProperty allRendererFeatures;
        private GUIStyle boldLabel;

        private VisualElement currentLevelContainer;
        private SerializedProperty customSettings;
        private GUIStyle popup;
        private UnityEditor.Editor qualitySettingsEditor;

        private SerializedObject qualitySettingSerializedObject;
        private UnityEditor.Editor rendererDataEditor;
        private new QualitySettingsAsset target;

        private void OnEnable()
        {
            target = (QualitySettingsAsset)base.target;
            customSettings = serializedObject.FindProperty(nameof(QualitySettingsAsset.customSettings));
            allRendererFeatures = serializedObject.FindProperty(nameof(QualitySettingsAsset.allRendererFeatures));

            qualitySettingsEditor = CreateEditor(QualitySettings.GetQualitySettings());
        }

        private void OnDestroy()
        {
            DestroyImmediate(qualitySettingsEditor);

            if (rendererDataEditor)
                DestroyImmediate(rendererDataEditor);
        }

        public override VisualElement CreateInspectorGUI()
        {
            var container = new VisualElement();

            VisualElement defaultQualitySettingsFoldout = CreateHeaderFoldout("Default Quality Settings");
            defaultQualitySettingsFoldout.Add(new IMGUIContainer(qualitySettingsEditor.OnInspectorGUI));

            container.Add(defaultQualitySettingsFoldout);

            VisualElement customSettingsFoldout = CreateHeaderFoldout("Custom Settings");

            currentLevelContainer = new VisualElement();
            currentLevelContainer.style.paddingLeft = 20;
            currentLevelContainer.style.paddingTop = 10;
            customSettingsFoldout.Add(new IMGUIContainer(DrawQualityLevel));
            customSettingsFoldout.Add(currentLevelContainer);

            container.Add(customSettingsFoldout);

            container.Add(new IMGUIContainer(DrawAllRendererFeatures));

            if (EditorUtility.IsPersistent(target))
            {
                EnsureCustomSettingsSize(QualitySettings.count);
                DrawQualitySelector();
            }

            return container;
        }

        private VisualElement CreateHeaderFoldout(string label)
        {
            var foldout = new Foldout();
            foldout.viewDataKey = $"{nameof(QualitySettingsAssetEditor)}_{label}";
            foldout.text = label;

            foldout.style.paddingBottom = 7;
            foldout.style.paddingTop = 7;

            Label foldOutLabel = foldout.Q<Label>();

            foldOutLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldOutLabel.style.fontSize = 14;

            return foldout;
        }

        private void DrawQualityLevel()
        {
            InitStyles();

            if (!EditorUtility.IsPersistent(target))
                return;

            string[] qualityLevels = QualitySettings.names;

            // Align the number of fields with the quality settings number
            // I don't know to implement it with UI Toolkit
            EnsureCustomSettingsSize(qualityLevels.Length);

            int prev = QualitySettings.GetQualityLevel();
            int selectedQuality = EditorGUILayout.Popup("Select Quality Level", prev, qualityLevels, popup);

            if (prev != selectedQuality)
            {
                QualitySettings.SetQualityLevel(selectedQuality);
                DrawQualitySelector();
            }
        }

        private void EnsureCustomSettingsSize(int newSize)
        {
            serializedObject.Update();

            int oldSize = customSettings.arraySize;

            if (oldSize < newSize)
                customSettings.arraySize = newSize;

            ValidateVolumeProfileAttached();
            ValidateLensFlareAssetsAttached();

            if (serializedObject.ApplyModifiedProperties())
            {
                // Force save
                if (EditorUtility.IsPersistent(base.target))
                {
                    EditorUtility.SetDirty(base.target);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        private void DrawQualitySelector()
        {
            int selectedQuality = QualitySettings.GetQualityLevel();

            currentLevelContainer.Clear();

            var field = new PropertyField(customSettings.GetArrayElementAtIndex(selectedQuality));
            field.Bind(serializedObject);
            field.style.marginBottom = 5;

            // Draw the renderer features directly
            if (rendererDataEditor)
                DestroyImmediate(rendererDataEditor);

            rendererDataEditor = DrawRendererFeatures(selectedQuality);

            currentLevelContainer.Add(field);
            currentLevelContainer.Add(new IMGUIContainer(rendererDataEditor.OnInspectorGUI));
        }

        /// <summary>
        ///     Draws renderer features directly for the selected quality level
        /// </summary>
        /// <param name="qualityIndex"></param>
        private UnityEditor.Editor DrawRendererFeatures(int qualityIndex)
        {
            var asset = (UniversalRenderPipelineAsset)QualitySettings.GetRenderPipelineAssetAt(qualityIndex);
            int defaultDataIndex = URPReflection.GetDefaultRendererIndex(asset);
            ScriptableRendererData[] data = URPReflection.GetRendererDataList(asset);

            ScriptableRendererData rendererData = data[defaultDataIndex];

            // Use ScriptableRendererDataEditor
            return CreateEditor(rendererData, typeof(ScriptableRendererDataEditor));
        }

        private void DrawAllRendererFeatures()
        {
            tempFeatures.Clear();

            QualitySettings.GetRenderPipelineAssetsForPlatform
                (BuildTargetGroup.Standalone.ToString(), out HashSet<UniversalRenderPipelineAsset> allAssets);

            foreach (UniversalRenderPipelineAsset asset in allAssets)
            {
                ScriptableRendererData[] data = URPReflection.GetRendererDataList(asset);

                foreach (ScriptableRendererData rendererData in data)
                foreach (ScriptableRendererFeature rendererFeature in rendererData.rendererFeatures)
                    tempFeatures.Add(rendererFeature);
            }

            // Change the property if the value has changed
            List<ScriptableRendererFeature> serializedAssets = target.allRendererFeatures;

            if (!tempFeatures.SetEquals(serializedAssets))
            {
                allRendererFeatures.arraySize = tempFeatures.Count;

                var i = 0;

                foreach (ScriptableRendererFeature feature in tempFeatures)
                {
                    allRendererFeatures.GetArrayElementAtIndex(i).objectReferenceValue = feature;
                    i++;
                }

                serializedObject.ApplyModifiedProperties();
            }

            GUI.enabled = false;
            EditorGUILayout.PropertyField(allRendererFeatures);
            GUI.enabled = true;
        }

        private void InitStyles()
        {
            boldLabel ??= new GUIStyle("BoldLabel");
            popup ??= new GUIStyle("ExposablePopupMenu");
        }
    }
}
