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
        private AvatarAudioSettings audioSettings;

        private void OnEnable()
        {
            audioSettings = (AvatarAudioSettings)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("movementBlendThreshold"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("avatarAudioPriority"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Audio Clip Configs", EditorStyles.boldLabel);
            DisplayAudioClipConfigs();

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);

        }

        private void DisplayAudioClipConfigs()
        {
            Array clipTypes = Enum.GetValues(typeof(AvatarAudioClipType));

            foreach (AvatarAudioClipType clipType in clipTypes)
            {
                EditorGUILayout.BeginHorizontal();

                AudioClipConfig clipConfig = audioSettings.GetAudioClipConfigForType(clipType);
                EditorGUI.BeginChangeCheck(); // Begin change check
                clipConfig = EditorGUILayout.ObjectField(clipType.ToString(), clipConfig, typeof(AudioClipConfig), false) as AudioClipConfig;
                if (EditorGUI.EndChangeCheck())
                {
                    if (clipConfig != null) { audioSettings.SetAudioClipConfigForType(clipType, clipConfig); }
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }}
