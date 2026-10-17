using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Utility.ComputeShaders.Editor
{
    public class ComputeShaderHotReloadPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            for (var i = 0; i < importedAssets.Length; i++)
            {
                string asset = importedAssets[i];

                if (Path.GetExtension(asset).Equals(".compute", StringComparison.OrdinalIgnoreCase))
                {
                    ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(asset);

                    if (shader)
                        ComputeShaderHotReload.Invoke(shader);
                }
            }
        }
    }
}
