using System.IO;
using UnityEditor;
using UnityEngine;

namespace DCL.Character.CharacterMotion
{
    public class ExtractGLTFAnimations
    {
        [MenuItem("Assets/Copy GLTF Animations")]
        private static void CopyGLTFAnimations()
        {
            // Get the selected GLTF file
            Object selectedObject = Selection.activeObject;
            string assetPath = AssetDatabase.GetAssetPath(selectedObject);

            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".gltf"))
            {
                // Load the GLTF file
                GameObject gltfObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                Animator animator = gltfObject.GetComponent<Animator>();

                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    AnimationClip[] animationClips = animator.runtimeAnimatorController.animationClips;

                    // Iterate over each animation clip
                    foreach (AnimationClip clip in animationClips)
                    {
                        if (clip == null) continue;

                        // Create a copy of the animation clip
                        var newClip = new AnimationClip();

                        EditorUtility.CopySerialized(clip, newClip);

                        // Save the animation clip as a separate file
                        string newPath = Path.GetDirectoryName(assetPath) + "/" + clip.name + ".anim";

                        newClip.legacy = false;

                        AssetDatabase.CreateAsset(newClip, newPath);

                        Debug.Log("Animation clip copied and saved at: " + newPath);
                    }
                }
                else
                {
                    // If Animator component is not found or runtimeAnimatorController is null, try accessing animation clips directly
                    UnityEngine.Animation[] animations = gltfObject.GetComponents<UnityEngine.Animation>();

                    foreach (UnityEngine.Animation animation in animations)
                    {
                        foreach (AnimationState animState in animation)
                        {
                            // Create a copy of the animation clip
                            var newClip = new AnimationClip();
                            EditorUtility.CopySerialized(animState.clip, newClip);

                            newClip.legacy = false;

                            // Save the animation clip as a separate file
                            string newPath = Path.GetDirectoryName(assetPath) + "/" + animState.clip.name + ".anim";

                            AssetDatabase.CreateAsset(newClip, newPath);
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();

                            Debug.Log("Animation clip copied and saved at: " + newPath);
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            else { Debug.LogWarning("Please select a GLTF file to copy animations."); }
        }
    }
}
