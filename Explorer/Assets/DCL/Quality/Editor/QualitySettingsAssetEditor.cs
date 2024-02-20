using System;
using System.Linq.Expressions;
using UnityEditor;
using UnityEditor.Rendering.Universal;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace DCL.Quality
{
    [CustomEditor(typeof(QualitySettingsAsset))]
    public class QualitySettingsAssetEditor : UnityEditor.Editor
    {
        private GUIStyle boldLabel;

        private VisualElement currentLevelContainer;
        private SerializedProperty customSettings;
        private GUIStyle popup;
        private UnityEditor.Editor qualitySettingsEditor;
        private UnityEditor.Editor rendererDataEditor;

        private SerializedObject qualitySettingSerializedObject;
        private int selectedQuality;
        private new QualitySettingsAsset target;

        private void OnEnable()
        {
            target = (QualitySettingsAsset)base.target;
            customSettings = serializedObject.FindProperty("customSettings");

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
            customSettingsFoldout.Add(new IMGUIContainer(DrawQualityLevel));
            customSettingsFoldout.Add(currentLevelContainer);

            container.Add(customSettingsFoldout);

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
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldout.style.paddingBottom = 7;
            foldout.style.paddingTop = 7;
            foldout.style.fontSize = 14;
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

            int prev = selectedQuality;
            selectedQuality = EditorGUILayout.Popup("Select Quality Level", selectedQuality, qualityLevels, popup);

            if (prev != selectedQuality)
                DrawQualitySelector();
        }

        private void EnsureCustomSettingsSize(int newSize)
        {
            VolumeProfile CreateNewAsset(int index)
            {
                VolumeProfile profile = CreateInstance<VolumeProfile>();

                //profile.hideFlags = HideFlags.HideInInspector;
                profile.name = $"Custom Volume Profile {index}";
                return profile;
            }

            serializedObject.Update();

            int oldSize = customSettings.arraySize;

            if (oldSize < newSize)
            {
                customSettings.arraySize = newSize;

                for (int i = oldSize; i < newSize; i++)
                {
                    // Create a profile as a subasset
                    VolumeProfile newProfile = CreateNewAsset(i);

                    // Store this new effect as a subasset so we can reference it safely afterwards
                    AssetDatabase.AddObjectToAsset(newProfile, base.target);

                    SerializedProperty element = customSettings.GetArrayElementAtIndex(i)
                                                               .FindPropertyRelative(nameof(QualitySettingsAsset.QualityCustomLevel.volumeProfile));

                    element.objectReferenceValue = newProfile;
                }

                serializedObject.ApplyModifiedProperties();

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

        private void InitStyles()
        {
            boldLabel ??= new GUIStyle("BoldLabel");
            popup ??= new GUIStyle("ExposablePopupMenu");
        }
    }
}
