using Global.Dynamic;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Utility;

namespace Global.Editor
{
    [CustomPropertyDrawer(typeof(RealmLaunchSettings))]
    public class RealmLaunchSettingsDrawer : PropertyDrawer
    {
        private const string INITIAL_REALM_FIELD_NAME = "initialRealm";
        private const string TARGET_SCENE_FIELD_NAME = "targetScene";
        private const string TARGET_WORLD_FIELD_NAME = "targetWorld";
        private const string CUSTOM_REALM_FIELD_NAME = "customRealm";
        private const string REMOTE_SCENE_ID_FIELD_NAME = "remoteSceneID";
        private const string REMOTE_SCENE_CONTENT_SERVER_FIELD_NAME = "remoteSceneContentServer";
        private const string PREDEFINED_SCENES_FIELD_NAME = "predefinedScenes";
        private const string IS_SOLO_SCENE_LOADING_FIELD_NAME = "isSoloSceneLoading";

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
            Rect fieldPosition = propertyPosition;
            SerializedProperty property = parent.FindPropertyRelative(TARGET_SCENE_FIELD_NAME);

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
                SerializedProperty property = parent.FindPropertyRelative(TARGET_WORLD_FIELD_NAME);

                EditorGUI.PropertyField(position, property);
                position.y += singleLineHeight;
            }

            return position;
        }

        private static Rect DrawCustomRealm(Rect position, SerializedProperty parent, InitialRealm initialRealm)
        {
            if (initialRealm == InitialRealm.Custom)
            {
                SerializedProperty property = parent.FindPropertyRelative(CUSTOM_REALM_FIELD_NAME);

                EditorGUI.PropertyField(position, property);
                position.y += singleLineHeight;
            }

            return position;
        }

        private static Rect DrawLocalhost(Rect position, SerializedProperty parent, InitialRealm initialRealm)
        {
            if (initialRealm == InitialRealm.Localhost)
            {
                SerializedProperty remoteSceneIDProperty = parent.FindPropertyRelative(REMOTE_SCENE_ID_FIELD_NAME);
                SerializedProperty remoteSceneContentServerProperty = parent.FindPropertyRelative(REMOTE_SCENE_CONTENT_SERVER_FIELD_NAME);

                EditorGUI.PropertyField(position, remoteSceneIDProperty);
                position.y += singleLineHeight;

                EditorGUI.PropertyField(position, remoteSceneContentServerProperty);
                position.y += singleLineHeight;
            }

            return position;
        }

        private static Rect DrawPredefinedScenes(Rect position, SerializedProperty parent)
        {
            SerializedProperty predefinedList = parent.FindPropertyRelative(PREDEFINED_SCENES_FIELD_NAME);
            SerializedProperty enabled = predefinedList.FindPropertyRelative("enabled");

            EditorGUI.PropertyField(position, enabled, new GUIContent("Use Predefined Parcels"), true);
            position.y += singleLineHeight;

            if (predefinedList.FindPropertyRelative("enabled").boolValue)
            {
                var parcels = predefinedList.FindPropertyRelative("parcels");

                EditorGUI.PropertyField(position, parcels, true);
                position.y += EditorGUI.GetPropertyHeight(parcels, true);
            }

            return position;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty initialRealmField = property.FindPropertyRelative(INITIAL_REALM_FIELD_NAME);
            InitialRealm initialRealmValue = EnumUtils.Values<InitialRealm>()[initialRealmField.enumValueIndex];

            position.height = singleLineHeight;

            position = DrawInitialRealm(position, initialRealmField);
            position = DrawTargetScene(position, property, initialRealmValue);
            position = DrawTargetWorld(position, property, initialRealmValue);
            position = DrawCustomRealm(position, property, initialRealmValue);
            position = DrawLocalhost(position, property, initialRealmValue);
            position = DrawPredefinedScenes(position, property);
            position = DrawIsSoloSceneLoading(position, property);

            EditorGUI.EndProperty();
        }

        private static Rect DrawIsSoloSceneLoading(Rect position, SerializedProperty parent)
        {
            SerializedProperty property = parent.FindPropertyRelative(IS_SOLO_SCENE_LOADING_FIELD_NAME);
            EditorGUI.PropertyField(position, property);
            position.y += singleLineHeight;
            return position;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var fieldsCount = 3;

            SerializedProperty initialRealmField = property.FindPropertyRelative(INITIAL_REALM_FIELD_NAME);
            InitialRealm initialRealmValue = EnumUtils.Values<InitialRealm>()[initialRealmField.enumValueIndex];

            switch (initialRealmValue)
            {
                case InitialRealm.Localhost:
                    fieldsCount += 2;
                    break;
                case InitialRealm.World:
                case InitialRealm.Custom:
                    fieldsCount += 1;
                    break;
            }

            float height = (fieldsCount * singleLineHeight) + singleLineHeight;

            SerializedProperty predefinedList = property.FindPropertyRelative(PREDEFINED_SCENES_FIELD_NAME);

            if (predefinedList.FindPropertyRelative("enabled").boolValue) { height += EditorGUI.GetPropertyHeight(predefinedList.FindPropertyRelative("parcels"), true); }

            return height;
        }
    }
}
