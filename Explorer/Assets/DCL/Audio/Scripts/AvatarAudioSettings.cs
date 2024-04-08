using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "AvatarAudioSettings", menuName = "SO/Audio/AvatarAudioSettings")]
    public class AvatarAudioSettings : AudioCategorySettings
    {
        //This threshold indicates at what point in the animation movement blend we stop producing sounds. This avoids unwanted sounds from "ghost" steps produced by the animation blending.
        [SerializeField] private float movementBlendThreshold = 0.05f;
        [SerializeField] private List<AudioClipTypeAndConfigKeyValuePair> audioClipConfigsList = new List<AudioClipTypeAndConfigKeyValuePair>();

        [SerializeField] private Dictionary<AvatarAudioClipType, AudioClipConfig> audioClipConfigs;

        public float MovementBlendThreshold => movementBlendThreshold;

        public void SetAudioClipConfigForType(AvatarAudioClipType clipType, AudioClipConfig clipConfig)
        {
            audioClipConfigs ??= new Dictionary<AvatarAudioClipType, AudioClipConfig>();

            if (audioClipConfigs.ContainsKey(clipType))
            {
                audioClipConfigs[clipType] = clipConfig;
            }
            else
            {
                audioClipConfigs.Add(clipType,clipConfig);
            }
        }

        public AudioClipConfig GetAudioClipConfigForType(AvatarAudioClipType type)
        {
            if (audioClipConfigs != null)
            {
                audioClipConfigs.TryGetValue(type, out var clipConfig);
                return clipConfig;
            }

            return null;
        }
    }

    [Serializable]
    struct AudioClipTypeAndConfigKeyValuePair
    {
        public AvatarAudioClipType key;
        public AudioClipConfig value;
    }

    //[CustomEditor(typeof(AvatarAudioSettings))]
    public class AvatarAudioSettingsEditor : Editor
    {
        private SerializedProperty movementBlendThresholdProp;
        private SerializedProperty audioClipConfigsProp;
        private SerializedProperty avatarAudioPriorityProp;
        private SerializedProperty categoryVolume;
        private SerializedProperty audioMixerGroup;


        private void OnEnable()
        {
            movementBlendThresholdProp = serializedObject.FindProperty("movementBlendThreshold");
            audioClipConfigsProp = serializedObject.FindProperty("audioClipConfigs");
            avatarAudioPriorityProp = serializedObject.FindProperty("audioPriority");
            categoryVolume = serializedObject.FindProperty("categoryVolume");
            audioMixerGroup = serializedObject.FindProperty("audioMixerGroup");

        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(movementBlendThresholdProp);
            EditorGUILayout.PropertyField(avatarAudioPriorityProp);
            EditorGUILayout.PropertyField(categoryVolume);
            EditorGUILayout.PropertyField(audioMixerGroup);

            EditorGUILayout.PropertyField(audioClipConfigsProp, true);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

}
