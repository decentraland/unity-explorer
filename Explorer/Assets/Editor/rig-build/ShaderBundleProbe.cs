using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Editor
{
    // Batchmode diagnostic: compiles the landscape/grass shaders for
    // StandaloneLinux64 by building them into a throwaway AssetBundle — the same
    // target-platform variant compilation the player build performs, minus the
    // player build itself. Any "Shader error in ..." lines land in the -logFile.
    //   Unity -batchmode -nographics -buildTarget StandaloneLinux64 \
    //         -projectPath Explorer -executeMethod Editor.ShaderBundleProbe.Run \
    //         -logFile <log>
    public static class ShaderBundleProbe
    {
        private static readonly string[] ASSETS =
        {
            "Assets/DCL/Landscape/Shaders/GrassScatter.compute",
            "Assets/DCL/Landscape/Shaders/FlowerScatter.compute",
            "Assets/DCL/Landscape/Shaders/CatTailScatter.compute",
            "Assets/DCL/Landscape/Shaders/QuadTreeEvalutation.compute",
            "Assets/DCL/Landscape/Shaders/RenderQuadTree.compute",
            "Assets/DCL/Landscape/Shaders/CS_NoiseTexture.compute",
            "Assets/DCL/Landscape/Shaders/CS_NoiseTexture_Complex.compute",
            "Assets/DCL/Landscape/Shaders/MountainLit.shader",
            "Assets/DCL/Landscape/Shaders/Tree/DCL_Tree.shader",
            "Assets/DCL/Landscape/Shaders/Tree/TreeImposter/DCL_Tree_Impostor.shader",
            "Packages/decentraland.grassshader/Runtime/Shaders/StylizedGrass.shader",
        };

        private static string RigPath(string fileName) =>
            System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".dcl-rig", fileName);

        public static void Run()
        {
            string outDir = RigPath("shaderprobe-bundle");
            System.IO.Directory.CreateDirectory(outDir);

            var bundle = new AssetBundleBuild
            {
                assetBundleName = "shaderprobe",
                assetNames = ASSETS,
            };

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                outDir, new[] { bundle },
                BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneLinux64);

            // The bundle build repopulates the per-asset message store — dump it.
            var sb = new StringBuilder();
            sb.AppendLine($"[probe] manifest={(manifest != null ? "OK" : "NULL")}");

            foreach (string path in ASSETS)
            {
                var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);

                if (cs != null)
                {
                    ShaderMessage[] msgs = ShaderUtil.GetComputeShaderMessages(cs);
                    sb.AppendLine($"[probe] COMPUTE {path} messages={msgs.Length}");
                    Append(sb, msgs);
                    continue;
                }

                var sh = AssetDatabase.LoadAssetAtPath<Shader>(path);

                if (sh != null)
                {
                    int n = ShaderUtil.GetShaderMessageCount(sh);
                    sb.AppendLine($"[probe] SHADER {path} messages={n}");
                    if (n > 0) Append(sb, ShaderUtil.GetShaderMessages(sh));
                }
            }

            System.IO.File.WriteAllText(RigPath("shaderprobe-messages.txt"), sb.ToString());
            Debug.Log(sb.ToString());
            EditorApplication.Exit(manifest != null ? 0 : 1);
        }

        private static void Append(StringBuilder sb, ShaderMessage[] msgs)
        {
            foreach (ShaderMessage m in msgs)
                sb.AppendLine($"[probe]   {m.severity} [{m.platform}] {m.file}({m.line}): {m.message} || {m.messageDetails}");
        }
    }
}
