using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "AudioSettings", menuName = "SO/Audio/AudioSettings")]
    public class AudioSettings : ScriptableObject
    {
        [SerializeField] private List<AudioCategorySettingsKeyValuePair> audioCategorySettings = new List<AudioCategorySettingsKeyValuePair>();
        [SerializeField] private float masterVolume = 1;
        [SerializeField] private AudioMixer masterAudioMixer;


        public float MasterVolume => masterVolume;
        public List<AudioCategorySettingsKeyValuePair> CategorySettings => audioCategorySettings;

    }

    [Serializable]
    public class AudioCategorySettings
    {
        [SerializeField] private float categoryVolume = 1;
        [SerializeField] private int audioPriority = 125;
        [SerializeField] private AudioMixerGroup audioMixerGroup;

        public float CategoryVolume => categoryVolume;
        public int AudioPriority => audioPriority;
    }

     [Serializable]
    public class AudioCategorySettingsKeyValuePair
    {
            public AudioCategory key;
            public AudioCategorySettings value;
    }

//[CustomEditor(typeof(AudioSettings))]
public class AudioSettingsEditor : Editor
{
    private SerializedProperty audioCategorySettings;

    private void OnEnable()
    {
        audioCategorySettings = serializedObject.FindProperty("audioCategorySettings");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Audio Category Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        int arraySize = audioCategorySettings.arraySize;
        HashSet<AudioCategory> existingKeys = new HashSet<AudioCategory>();
        for (int i = 0; i < arraySize; i++)
        {
            SerializedProperty element = audioCategorySettings.GetArrayElementAtIndex(i);
            SerializedProperty key = element.FindPropertyRelative("key");
            SerializedProperty value = element.FindPropertyRelative("value");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(key, GUIContent.none);
            EditorGUILayout.PropertyField(value, GUIContent.none);
            if (GUILayout.Button("-", GUILayout.Width(20)))
            {
                audioCategorySettings.DeleteArrayElementAtIndex(i);
                break;
            }
            EditorGUILayout.EndHorizontal();

            if (!existingKeys.Add((AudioCategory)key.enumValueIndex))
            {
                EditorGUILayout.HelpBox("Duplicate key found! Please remove the duplicate.", MessageType.Error);
            }
        }
        EditorGUI.indentLevel--;

        if (GUILayout.Button("Add"))
        {
            // Ensure no duplicate keys are added
            List<AudioCategory> unusedKeys = new List<AudioCategory>((AudioCategory[])Enum.GetValues(typeof(AudioCategory)));
            unusedKeys.RemoveAll(existingKeys.Contains);

            if (unusedKeys.Count > 0)
            {
                audioCategorySettings.InsertArrayElementAtIndex(arraySize);
                SerializedProperty newElement = audioCategorySettings.GetArrayElementAtIndex(arraySize);
                SerializedProperty newKey = newElement.FindPropertyRelative("key");
                newKey.enumValueIndex = (int)unusedKeys[0]; // Add the first unused key
            }
            else
            {
                EditorGUILayout.HelpBox("All keys have been used. No more can be added.", MessageType.Warning);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}


}

