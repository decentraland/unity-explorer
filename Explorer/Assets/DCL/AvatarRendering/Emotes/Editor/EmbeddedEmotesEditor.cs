using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    public static class EmbeddedEmotesEditor
    {
        [MenuItem("Decentraland/Generate Embedded Emote Prefabs")]
        private static void GeneratePrefabFiles()
        {
            GeneratePrefabs();
        }

        private static void GeneratePrefabs()
        {
            // Load the text files
            var clipsPath = "Assets/DCL/AvatarRendering/AvatarShape/Assets/EmbeddedEmotes/Animations";
            var prefabsPath = "Assets/DCL/AvatarRendering/AvatarShape/Assets/EmbeddedEmotes/Prefabs";

            string[]? clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { clipsPath });
            string[]? prefabs = AssetDatabase.FindAssets("t:GameObject", new[] { prefabsPath });
            clips = clips.Select(AssetDatabase.GUIDToAssetPath).ToArray();
            prefabs = prefabs.Select(AssetDatabase.GUIDToAssetPath).ToArray();
            string[] prefabFileNames = prefabs.Select(p => Path.GetFileNameWithoutExtension(p).ToLower().Replace("_emote", "")).ToArray();

            GameObject? baseEmote = AssetDatabase.LoadAssetAtPath<GameObject>(prefabsPath + "/BaseEmote.prefab");

            foreach (string clip in clips)
            {
                string fileName = Path.GetFileNameWithoutExtension(clip).ToLower();
                AnimationClip? animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clip);

                GameObject? gameObject = null;
                var currentPrefabPath = "";

                if (!prefabFileNames.Contains(fileName))
                {
                    //Debug.LogWarning($"Emote {fileName} does not have an associated prefab");
                    gameObject = PrefabUtility.InstantiatePrefab(baseEmote) as GameObject;
                    currentPrefabPath = prefabsPath + $"/{fileName}_Emote.prefab";

                    Debug.Log($"{fileName} -> {currentPrefabPath}");
                }
                else
                {
                    int index = Array.IndexOf(prefabFileNames, fileName);
                    currentPrefabPath = prefabs[index];
                    Debug.Log($"{fileName} -> {currentPrefabPath}");
                    gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(currentPrefabPath);
                }

                Animator? animator = gameObject!.GetComponent<Animator>();

                if (animator.runtimeAnimatorController == null)
                {
                    var animatorController = AnimatorController.CreateAnimatorControllerAtPath(prefabsPath + $"/{fileName}.controller");
                    AnimatorStateMachine? rootStateMachine = animatorController.layers[0].stateMachine;
                    string animationName = fileName + "_avatar";
                    animatorController.AddParameter(animationName, AnimatorControllerParameterType.Trigger);
                    AnimatorState? state = animatorController.AddMotion(animationClip, 0);
                    AnimatorStateTransition? anyStateTransition = rootStateMachine.AddAnyStateTransition(state);
                    anyStateTransition.AddCondition(AnimatorConditionMode.If, 0, animationName);
                    anyStateTransition.duration = 0;

                    animator.runtimeAnimatorController = animatorController;
                    PrefabUtility.SaveAsPrefabAsset(gameObject, currentPrefabPath);
                }
            }
        }
    }
}
