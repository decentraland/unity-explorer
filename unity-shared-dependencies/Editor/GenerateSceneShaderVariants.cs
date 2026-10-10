using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DCL.Shaders.Editor
{
    public static class GenerateSceneShaderVariants
    {
        [MenuItem("Decentraland/Shaders/Generate \"Scene\" Shader Variants")]
        public static void ExecuteMenuItem()
        {
            string path = EditorUtility.OpenFilePanel("Shader Variants", Application.dataPath, "shadervariants");

            if (!string.IsNullOrEmpty(path))
            {
                string assetPath = "Assets/" + path.Substring(Application.dataPath.Length + 1);
                ShaderVariantCollection shaderVariants = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(assetPath);
                shaderVariants.Clear();

                var shader = Shader.Find("DCL/Scene");

                // Add all combinations of keywords to the shader variant collection
                // Starting from all constants variants

                void AddVariant(List<string> keywords)
                {
                    var finalKeywords = new string[keywords.Count + SceneShaderUtils.CONSTANT_KEYWORDS.Length];

                    SceneShaderUtils.CONSTANT_KEYWORDS.CopyTo(finalKeywords, 0);
                    keywords.CopyTo(finalKeywords, SceneShaderUtils.CONSTANT_KEYWORDS.Length);

                    // ignore failing combinations
                    try { shaderVariants.Add(new ShaderVariantCollection.ShaderVariant(shader, SceneShaderUtils.PASS_TYPE, finalKeywords)); }
                    catch (ArgumentException) { }
                }

                // Start with the pure one
                var currentCombination = new List<string>();
                AddVariant(currentCombination);

                float combinationsCount = Mathf.Pow(2, SceneShaderUtils.VARIABLE_KEYWORDS.Length);

                // Add all combinations of variable keywords
                for (var i = 1; i < combinationsCount; i++)
                {
                    currentCombination.Clear();

                    for (var j = 0; j < SceneShaderUtils.VARIABLE_KEYWORDS.Length; j++)
                    {
                        if ((i & (1 << j)) != 0)
                            currentCombination.Add(SceneShaderUtils.VARIABLE_KEYWORDS[j]);
                    }

                    AddVariant(currentCombination);
                }

                EditorUtility.SetDirty(shaderVariants);
            }
        }
    }
}
