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

        [MenuItem("Tools/Audio Config Editor")]
        public static void ShowWindow()
        {
            GetWindow<AudioConfigViewerWindow>("Audio Config Editor");
        }

        private Vector2 scrollPosition = Vector2.zero;
        private Dictionary<AudioCategory, bool> foldoutStates = new Dictionary<AudioCategory, bool>();

        private void OnGUI()
        {
            float margin = 10f;

            GUILayout.BeginArea(new Rect(margin, margin, position.width - (margin * 2), position.height - (margin * 2)));

            EditorGUILayout.Space();

            if (GUILayout.Button("Update Audio Configs"))
            {
                UpdateAudioConfigs();
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Create New AudioClipConfig"))
            {
                CreateNewUIAudioConfig();
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            var groupedAudioConfigs = GroupByCategory(audioConfigs);

            EditorGUILayout.LabelField("Audio Clip Configs:", EditorStyles.boldLabel);
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
                        EditorGUILayout.Space();
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private AudioClipConfig audioConfigRenamed = null;
        private string newName = "";

        void RenameAsset(AudioClipConfig audioConfig, string newName)
        {
            if (newName != audioConfig.name)
            {
                Undo.RecordObject(audioConfig, "Rename");
                string assetPath = AssetDatabase.GetAssetPath(audioConfig);
                AssetDatabase.RenameAsset(assetPath, newName);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            audioConfigRenamed = null;
        }

        private void ShowUIAudioConfig(AudioClipConfig audioConfig)
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            {
                if (audioConfigRenamed != audioConfig)
                {
                    GUILayout.Label("Object", GUILayout.Width(EditorGUIUtility.labelWidth * 0.3f));
                    audioConfig = (AudioClipConfig)EditorGUILayout.ObjectField("", audioConfig, typeof(AudioClipConfig), false, GUILayout.Width(position.width * 0.35f));

                    if (GUILayout.Button("Rename"))
                    {
                        audioConfigRenamed = audioConfig;
                        newName = audioConfig.name;
                    }
                }
                else
                {
                    newName = EditorGUILayout.TextField(newName);
                    if (Event.current.isKey && Event.current.keyCode == KeyCode.Return)
                    {
                        RenameAsset(audioConfig, newName);
                    }
                    if (GUILayout.Button("Save", GUILayout.Width(80)))
                    {
                        RenameAsset(audioConfig, newName);
                    }
                    if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                    {
                        audioConfigRenamed = null;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.Label("Volume", GUILayout.Width(EditorGUIUtility.labelWidth * 0.4f));
                audioConfig.volume = EditorGUILayout.Slider(audioConfig.volume, 0f, 1f, GUILayout.Width(position.width * 0.4f));

                GUILayout.Label("Category", GUILayout.Width(EditorGUIUtility.labelWidth * 0.4f));
                audioConfig.audioCategory = (AudioCategory)EditorGUILayout.EnumPopup(audioConfig.audioCategory);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Audio Clips:", GUILayout.Width(position.width * 0.5f));

                if (GUILayout.Button("Add AudioClip")) { Array.Resize(ref audioConfig.audioClips, audioConfig.audioClips.Length + 1); }
            }
            EditorGUILayout.EndHorizontal();

            if (audioConfig.audioClips != null)
            {
                EditorGUI.indentLevel++;
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
                EditorGUI.indentLevel--;
            }

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
            newAudioConfig.audioCategory = AudioCategory.OTHER;

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

