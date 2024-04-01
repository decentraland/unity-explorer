using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "AvatarAudioSettings", menuName = "SO/Audio/AvatarAudioSettings")]
    public class AvatarAudioSettings : ScriptableObject
    {
        //This threshold indicates at what point in the movement blend we stop producing sounds. This avoids unwanted sounds from "ghost" steps produced by the animation blending.
        [SerializeField] private float movementBlendThreshold = 0.05f;
        [SerializeField] private List<AvatarAudioClipKeyValuePair> audioClipList = new List<AvatarAudioClipKeyValuePair>();
        [SerializeField] private AudioClip defaultAudioClip;
        [SerializeField] private float avatarAudioVolume = 1;
        [SerializeField] private int avatarAudioPriority = 125;

        public float MovementBlendThreshold => movementBlendThreshold;
        public float AvatarAudioVolume => avatarAudioVolume;
        public int AvatarAudioPriority => avatarAudioPriority;


        [Serializable]
        public class AvatarAudioClipKeyValuePair
        {
            public AvatarAudioSourceManager.AvatarAudioClipTypes key;
            public AudioClip value;
        }

        [CustomPropertyDrawer(typeof(AvatarAudioClipKeyValuePair))]
        public class AudioClipKeyValuePairDrawer : KeyValuePairDrawer
        { }

        public class KeyValuePairDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                label = EditorGUI.BeginProperty(position, label, property);
                label.text = label.text[^1].ToString();

                EditorGUIUtility.labelWidth = 14f;
                Rect contentPosition = EditorGUI.PrefixLabel(position, label);
                contentPosition.width *= 0.5f;
                EditorGUI.indentLevel = 0;
                EditorGUI.PropertyField(contentPosition, property.FindPropertyRelative("key"), GUIContent.none);
                contentPosition.x += contentPosition.width;
                EditorGUI.PropertyField(contentPosition, property.FindPropertyRelative("value"), GUIContent.none);
                EditorGUI.EndProperty();
            }
        }

        public AudioClip GetAudioClipForType(AvatarAudioSourceManager.AvatarAudioClipTypes type)
        {
            foreach (var pair in audioClipList)
            {
                if (pair.key == type) { return pair.value; }
            }

            ReportHub.Log(ReportCategory.AUDIO, $"Audio Clip for type {type} not found, returning default audio clip {defaultAudioClip}");
            return defaultAudioClip;
        }
    }

    // [CustomEditor(typeof(AvatarAudioSettings))]
    public class AvatarAudioSettingsEditor : Editor
    {
        private SerializedProperty movementBlendThresholdProp;
        private SerializedProperty audioClipListProp;
        private SerializedProperty defaultAudioClipProp;
        private SerializedProperty avatarAudioVolumeProp;
        private SerializedProperty avatarAudioPriorityProp;

        private void OnEnable()
        {
            movementBlendThresholdProp = serializedObject.FindProperty("movementBlendThreshold");
            audioClipListProp = serializedObject.FindProperty("audioClipList");
            defaultAudioClipProp = serializedObject.FindProperty("defaultAudioClip");
            avatarAudioVolumeProp = serializedObject.FindProperty("avatarAudioVolume");
            avatarAudioPriorityProp = serializedObject.FindProperty("avatarAudioPriority");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(movementBlendThresholdProp);
            EditorGUILayout.PropertyField(defaultAudioClipProp);
            EditorGUILayout.PropertyField(avatarAudioVolumeProp);
            EditorGUILayout.PropertyField(avatarAudioPriorityProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Audio Clip List", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;

            for (int i = 0; i < audioClipListProp.arraySize; i++)
            {
                SerializedProperty pairProp = audioClipListProp.GetArrayElementAtIndex(i);
                SerializedProperty keyProp = pairProp.FindPropertyRelative("key");
                SerializedProperty valueProp = pairProp.FindPropertyRelative("value");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(keyProp);
                EditorGUILayout.PropertyField(valueProp, GUIContent.none);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space();

            if (GUILayout.Button("Add Element"))
            {
                audioClipListProp.arraySize++;
                serializedObject.ApplyModifiedProperties();
            }

            if (GUILayout.Button("Remove Element"))
            {
                audioClipListProp.arraySize--;
                serializedObject.ApplyModifiedProperties();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
