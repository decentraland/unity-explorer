using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DCL.Audio
{
    public class AudioConfigViewerWindow : EditorWindow
    {
        private const string ASSETS_PATH = "Assets/DCL/Audio/AudioConfigs";

        private List<AudioClipConfig> audioConfigs = new List<AudioClipConfig>();

        [MenuItem("Window/UI Audio Config Updater")]
        public static void ShowWindow()
        {
            GetWindow<AudioConfigViewerWindow>("UI Audio Configs Viewer");
        }

        private Vector2 scrollPosition = Vector2.zero;
        private Dictionary<AudioCategory, bool> foldoutStates = new Dictionary<AudioCategory, bool>();

        private void OnGUI()
        {
            EditorGUILayout.Space();

            if (GUILayout.Button("Update Audio Configs"))
            {
                UpdateAudioConfigs();
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("UI Audio Configs updated successfully.");
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Create New UIAudioConfig"))
            {
                CreateNewUIAudioConfig();
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            var groupedAudioConfigs = GroupByCategory(audioConfigs);

            EditorGUILayout.LabelField("Audio Configs:", EditorStyles.boldLabel);
            foreach (var group in groupedAudioConfigs)
            {
                EditorGUILayout.Space();
                if (!foldoutStates.ContainsKey(group.Key))
                {
                    foldoutStates[group.Key] = false;
                }
                foldoutStates[group.Key] = EditorGUILayout.Foldout(foldoutStates[group.Key], group.Key.ToString(), true, EditorStyles.foldout);

                if (foldoutStates[group.Key])
                {
                    foreach (var audioConfig in group)
                    {
                        ShowUIAudioConfig(audioConfig);
                        EditorGUILayout.Space();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void ShowUIAudioConfig(AudioClipConfig audioConfig)
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Object", GUILayout.Width(EditorGUIUtility.labelWidth * 0.3f));
                EditorGUILayout.ObjectField("", audioConfig, typeof(AudioClipConfig), false, GUILayout.Width(position.width * 0.35f));
                GUILayout.Label("Rename", GUILayout.Width(EditorGUIUtility.labelWidth * 0.45f));
                string newName = EditorGUILayout.TextField("", audioConfig.name);
            EditorGUILayout.EndHorizontal();

            if (newName != audioConfig.name)
            {
                Undo.RecordObject(audioConfig, "Rename");
                audioConfig.name = newName;
            }

            EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Audio Clips:", GUILayout.Width(position.width * 0.7f));
                if (GUILayout.Button("Add AudioClip"))
                {
                    Array.Resize(ref audioConfig.audioClips, audioConfig.audioClips.Length + 1);
                }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            if (audioConfig.audioClips != null)
            {
                for (int i = 0; i < audioConfig.audioClips.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    audioConfig.audioClips[i] = (AudioClip)EditorGUILayout.ObjectField("Clip " + i, audioConfig.audioClips[i], typeof(AudioClip), false, GUILayout.Width(position.width * 0.7f));
                    if (GUILayout.Button("Remove"))
                    {
                        audioConfig.audioClips = audioConfig.audioClips.Where((clip, index) => index != i).ToArray();
                        EditorGUIUtility.ExitGUI();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }

            }


            EditorGUI.indentLevel--;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Volume", GUILayout.Width(EditorGUIUtility.labelWidth * 0.4f));
            audioConfig.volume = EditorGUILayout.Slider(audioConfig.volume, 0f, 1f, GUILayout.Width(position.width * 0.2f));

            GUILayout.Label("Priority", GUILayout.Width(EditorGUIUtility.labelWidth * 0.4f));
            audioConfig.priority = EditorGUILayout.Slider(audioConfig.priority, 0f, 1f, GUILayout.Width(position.width * 0.2f));

            GUILayout.FlexibleSpace();

            GUILayout.Label("Category", GUILayout.Width(EditorGUIUtility.labelWidth * 0.4f));
            audioConfig.audioCategory = (AudioCategory)EditorGUILayout.EnumPopup(audioConfig.audioCategory);

            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(audioConfig);
            }
        }

        private IEnumerable<IGrouping<AudioCategory, AudioClipConfig>> GroupByCategory(IEnumerable<AudioClipConfig> configs)
        {
            return configs.GroupBy(config => config.audioCategory);
        }


        private void CreateNewUIAudioConfig()
        {
            string folderPath = ASSETS_PATH;
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogError($"Folder path '{folderPath}' does not exist.");
                return;
            }
            AudioClipConfig newAudioConfig = ScriptableObject.CreateInstance<AudioClipConfig>();
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, "NewUIAudioConfig.asset"));
            AssetDatabase.CreateAsset(newAudioConfig, assetPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            audioConfigs.Add(newAudioConfig);
        }


        private void UpdateAudioConfigs()
        {
            audioConfigs.Clear();

            string folderPath = ASSETS_PATH;

            string[] assetPaths = Directory.GetFiles(folderPath, "*.asset", SearchOption.AllDirectories);

            foreach (string assetPath in assetPaths)
            {
                AudioClipConfig audioConfig = AssetDatabase.LoadAssetAtPath<AudioClipConfig>(assetPath);
                if (audioConfig != null) { audioConfigs.Add(audioConfig); }
            }
        }
    }
}

