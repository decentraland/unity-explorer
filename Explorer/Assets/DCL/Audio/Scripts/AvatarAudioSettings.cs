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
        //This threshold indicates at what point in the animation movement blend we stop producing sounds. This avoids unwanted sounds from "ghost" steps produced by the animation blending.
        [SerializeField] private float movementBlendThreshold = 0.05f;
        [SerializeField] private Dictionary<AvatarAudioClipType, AudioClipConfig> audioClipConfigs;
        [SerializeField] private int avatarAudioPriority = 125;

        public float MovementBlendThreshold => movementBlendThreshold;
        public bool AvatarAudioEnabled = true; //We will change this when we change volume settings, making it false if volume goes to 0 or whatever
        public int AvatarAudioPriority => avatarAudioPriority;


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

    [CustomEditor(typeof(AvatarAudioSettings))]
    public class AvatarAudioSettingsEditor : Editor
    {
        private SerializedProperty movementBlendThresholdProp;
        private SerializedProperty audioClipConfigsProp;
        private SerializedProperty avatarAudioPriorityProp;

        private void OnEnable()
        {
            movementBlendThresholdProp = serializedObject.FindProperty("movementBlendThreshold");
            audioClipConfigsProp = serializedObject.FindProperty("audioClipConfigs");
            avatarAudioPriorityProp = serializedObject.FindProperty("avatarAudioPriority");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(movementBlendThresholdProp);
            EditorGUILayout.PropertyField(avatarAudioPriorityProp);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(audioClipConfigsProp, true);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

}
