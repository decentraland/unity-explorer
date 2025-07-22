using Global.Dynamic;
using SceneRuntime.Factory.JsSceneSourceCode;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SceneRunner.Scene;
using UnityEditor;
using UnityEngine;
using Utility;

namespace Global.Editor
{
    [CustomPropertyDrawer(typeof(RealmLaunchSettings))]
    public class RealmLaunchSettingsDrawer : PropertyDrawer
    {
        private static readonly Dictionary<string, Vector2Int> SCENES = new ()
        {
            { "Cube Spawner", new Vector2Int(0, 0) },
            { "Cube Wave 32x32", new Vector2Int(0, 2) },
            { "Scene bounds check", new Vector2Int(5, 90) },
            { "Testing Gallery", new Vector2Int(52, -52) },
            { "Testing 3D Models", new Vector2Int(54, -55) },
            { "UI Backgrounds", new Vector2Int(70, -9) },
            { "dbmonster", new Vector2Int(73, -2) },
            { "Animator Tests Shark", new Vector2Int(73, -8) },
            { "UI Canvas Information", new Vector2Int(76, -10) },
            { "Raycast unit tests", new Vector2Int(77, -1) },
            { "Tweens", new Vector2Int(77, -5) },
            { "Portable Experience", new Vector2Int(8, 8) },
            { "Portable Experience hide UI", new Vector2Int(8, 7) },
            { "Portable Experience disabled", new Vector2Int(8, 9) },
            { "Main crdt", new Vector2Int(80, -2) },
            { "UI", new Vector2Int(80, -3) },
            { "Restricted Actions", new Vector2Int(80, -4) },
        };

        private static readonly InitialRealm[] TEST_REALMS = { InitialRealm.SDK, InitialRealm.Goerli, InitialRealm.StreamingWorld, InitialRealm.TestScenes };

        private static float singleLineHeight => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        private static Rect DrawInitialRealm(Rect position, SerializedProperty property)
        {
            EditorGUI.PropertyField(position, property);

            position.y += singleLineHeight;
            return position;
        }

        private static Rect DrawTargetScene(Rect propertyPosition, SerializedProperty parent, InitialRealm initialRealm)
        {
            SerializedProperty editorSceneStartPosition = parent.FindPropertyRelative(nameof(RealmLaunchSettings.EditorSceneStartPosition));
            EditorGUI.PropertyField(propertyPosition, editorSceneStartPosition, new GUIContent("Editor Start Position", "If this is on, the feature flag position will not be set"), true);
            propertyPosition.y += singleLineHeight;

            Rect fieldPosition = propertyPosition;
            SerializedProperty property = parent.FindPropertyRelative(nameof(RealmLaunchSettings.targetScene));
            const float BUTTON_WIDTH = 80f;

            if (TEST_REALMS.Contains(initialRealm))
            {
                const float SPACE = 5f;
                var buttonPosition = new Rect(propertyPosition.x, propertyPosition.y, BUTTON_WIDTH, singleLineHeight);
                fieldPosition = new Rect(SPACE + propertyPosition.x + BUTTON_WIDTH, propertyPosition.y, propertyPosition.width - BUTTON_WIDTH - SPACE, propertyPosition.height);

                if (GUI.Button(buttonPosition, "Select"))
                {
                    var menu = new GenericMenu();

                    foreach ((string key, Vector2Int value) in SCENES)
                    {
                        string valueName = key;
                        Vector2Int vector = value;

                        menu.AddItem(new GUIContent($"{valueName} {value}"), false, () =>
                        {
                            property.vector2IntValue = vector;
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }

                    menu.ShowAsContext();
                }
            }

            EditorGUI.PropertyField(fieldPosition, property, GUIContent.none);

            propertyPosition.y += singleLineHeight;
            return propertyPosition;
        }

        private static Rect DrawTargetWorld(Rect position, SerializedProperty parent, InitialRealm initialRealm)
        {
            if (initialRealm == InitialRealm.World)
            {
                SerializedProperty property = parent.FindPropertyRelative("targetWorld");

                EditorGUI.PropertyField(position, property);
                position.y += singleLineHeight;
            }

            return position;
        }

        private static Rect DrawCustomRealm(Rect position, SerializedProperty parent, InitialRealm initialRealm)
        {
            if (initialRealm == InitialRealm.Custom)
            {
                SerializedProperty property = parent.FindPropertyRelative(nameof(RealmLaunchSettings.customRealm));

                EditorGUI.PropertyField(position, property);
                position.y += singleLineHeight;
            }

            return position;
        }

        private static Rect DrawIsolateSceneCommunication(Rect position, SerializedProperty parent, InitialRealm initialRealm)
        {
            if (initialRealm is InitialRealm.World or InitialRealm.Goerli or InitialRealm.StreamingWorld or InitialRealm.TestScenes)
            {
                SerializedProperty property = parent.FindPropertyRelative(nameof(RealmLaunchSettings.isolateSceneCommunication));

                EditorGUI.PropertyField(position, property);
                position.y += singleLineHeight;
            }

            return position;
        }

        private static Rect DrawLocalhost(Rect position, SerializedProperty parent, InitialRealm initialRealm)
        {
            if (initialRealm == InitialRealm.Localhost)
            {
                SerializedProperty remoteWorldContentServerProperty = parent.FindPropertyRelative(nameof(RealmLaunchSettings.remoteHibridWorld));
                SerializedProperty remoteSceneContentServerProperty = parent.FindPropertyRelative(nameof(RealmLaunchSettings.remoteHybridSceneContentServer));
                SerializedProperty useHibridAssets = parent.FindPropertyRelative(nameof(RealmLaunchSettings.useRemoteAssetsBundles));

                EditorGUI.PropertyField(position, useHibridAssets);
                position.y += singleLineHeight;

                if (useHibridAssets.boolValue)
                {
                    EditorGUI.LabelField(position, "Set content server in the dropdown below to fetch asset bundles");
                    position.y += singleLineHeight;

                    EditorGUI.PropertyField(position, remoteSceneContentServerProperty);
                    position.y += singleLineHeight;

                    if (remoteSceneContentServerProperty.enumValueIndex == (int)HybridSceneContentServer.World)
                    {
                        EditorGUI.LabelField(position, "Write down the remote world from where to get the content");
                        position.y += singleLineHeight;

                        EditorGUI.PropertyField(position, remoteWorldContentServerProperty);
                        position.y += singleLineHeight;
                    }
                }
            }

            return position;
        }

        private static Rect DrawPredefinedScenes(Rect position, SerializedProperty parent)
        {
            SerializedProperty predefinedList = parent.FindPropertyRelative(nameof(RealmLaunchSettings.predefinedScenes));
            SerializedProperty enabled = predefinedList.FindPropertyRelative("enabled");

            EditorGUI.PropertyField(position, enabled, new GUIContent("Use Predefined Parcels"), true);
            position.y += singleLineHeight;

            if (predefinedList.FindPropertyRelative("enabled").boolValue)
            {
                SerializedProperty parcels = predefinedList.FindPropertyRelative("parcels");

                EditorGUI.PropertyField(position, parcels, true);
                position.y += EditorGUI.GetPropertyHeight(parcels, true);
            }

            return position;
        }

        private static Rect DrawOverridenScenes(Rect position)
        {
            IReadOnlyList<(string coordinate, string assetDatabasePath)> overridenScenes = GetOverridenScenes();

            if (overridenScenes.Count > 0)
            {
                EditorGUI.LabelField(position, "Overriden scenes:", EditorStyles.boldLabel);

                position.y += singleLineHeight;

                EditorGUI.BeginDisabledGroup(true);

                foreach ((string coordinate, string assetDatabasePath) scene in overridenScenes)
                {
                    Object asset = AssetDatabase.LoadAssetAtPath(scene.assetDatabasePath, typeof(DefaultAsset));

                    EditorGUI.ObjectField(position, new GUIContent(scene.coordinate), asset, typeof(DefaultAsset), false);
                    position.y += singleLineHeight;
                }

                EditorGUI.EndDisabledGroup();
            }

            return position;
        }

        private static IReadOnlyList<(string coordinate, string assetDatabasePath)> GetOverridenScenes()
        {
            string directory = StreamingAssetsJsSceneLocalSourceCode.DIRECTORY_PATH;

            var overridenScenes =
                Directory.GetFiles(directory)
                         .Select(file => (GetDatabaseNormalizedPath(file), StreamingAssetsJsSceneLocalSourceCode.SCENE_NAME_PATTERN.Match(Path.GetFileName(file))))
                         .Where(data => data.Item2.Success)
                         .Select(data => ($"{data.Item2.Groups["x"].Value}, {data.Item2.Groups["y"].Value}", data.Item1))
                         .ToList();

            return overridenScenes;
        }

        private static string GetDatabaseNormalizedPath(string fileAbsolutePath)
        {
            fileAbsolutePath = fileAbsolutePath.Replace("\\", "/");
            return fileAbsolutePath.Replace(Application.dataPath, "Assets");
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty initialRealmField = property.FindPropertyRelative(nameof(RealmLaunchSettings.initialRealm));
            InitialRealm initialRealmValue = EnumUtils.Values<InitialRealm>()[initialRealmField.enumValueIndex];

            position.height = singleLineHeight;

            position = DrawInitialRealm(position, initialRealmField);
            position = DrawTargetScene(position, property, initialRealmValue);
            position = DrawTargetWorld(position, property, initialRealmValue);
            position = DrawCustomRealm(position, property, initialRealmValue);
            position = DrawLocalhost(position, property, initialRealmValue);
            position = DrawIsolateSceneCommunication(position, property, initialRealmValue);
            position = DrawPredefinedScenes(position, property);
            position = DrawOverridenScenes(position);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var fieldsCount = 3;

            SerializedProperty initialRealmField = property.FindPropertyRelative(nameof(RealmLaunchSettings.initialRealm));
            InitialRealm initialRealmValue = EnumUtils.Values<InitialRealm>()[initialRealmField.enumValueIndex];

            switch (initialRealmValue)
            {
                case InitialRealm.Localhost:
                    var fieldToAdd = 1;

                    if (property.FindPropertyRelative(nameof(RealmLaunchSettings.useRemoteAssetsBundles)).boolValue)
                    {
                        fieldToAdd += 2;
                        SerializedProperty remoteSceneContentServerProperty = property.FindPropertyRelative(nameof(RealmLaunchSettings.remoteHybridSceneContentServer));

                        if (remoteSceneContentServerProperty.enumValueIndex == (int)HybridSceneContentServer.World)
                            fieldToAdd += 2;
                    }

                    fieldsCount += fieldToAdd;
                    break;
                case InitialRealm.World:
                case InitialRealm.Custom:
                    fieldsCount += 1;
                    break;
            }

            float height = fieldsCount * singleLineHeight;

            SerializedProperty predefinedList = property.FindPropertyRelative(nameof(RealmLaunchSettings.predefinedScenes));

            if (predefinedList.FindPropertyRelative("enabled").boolValue) { height += EditorGUI.GetPropertyHeight(predefinedList.FindPropertyRelative("parcels"), true); }

            IReadOnlyList<(string coordinate, string assetDatabasePath)> overridenScenes = GetOverridenScenes();

            if (overridenScenes.Count > 0)
                height += singleLineHeight * (overridenScenes.Count + 1);

            return height;
        }
    }
}
