using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Editor
{
    // Batchmode diagnostic: force-reimports the landscape/grass shader assets and
    // dumps every compiler message (with platform + file + line) so the shader
    // errors counted by BuildReport.summary.totalErrors become visible verbatim.
    //   Unity -batchmode -nographics -projectPath Explorer \
    //         -executeMethod Editor.ShaderErrorDump.Run -logFile <log>
    // Output: ~/.dcl-rig/shader-error-dump.txt (and the editor log).
    public static class ShaderErrorDump
    {
        private static readonly string[] ROOTS =
        {
            "Assets/DCL/Landscape",
            "Packages/com.decentraland.unity-shared-dependencies",
            "Packages/decentraland.grassshader",
        };

        private static string RigPath(string fileName) =>
            System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".dcl-rig", fileName);

        public static void Run()
        {
            var sb = new StringBuilder();

            foreach (string guid in AssetDatabase.FindAssets("t:ComputeShader", ROOTS))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);

                if (cs == null)
                {
                    sb.AppendLine($"[dump] COMPUTE LOAD-FAILED {path}");
                    continue;
                }

                ShaderMessage[] msgs = ShaderUtil.GetComputeShaderMessages(cs);
                sb.AppendLine($"[dump] COMPUTE {path} messages={msgs.Length}");
                Append(sb, msgs);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Shader", ROOTS))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sh = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (sh == null) continue;

                int n = ShaderUtil.GetShaderMessageCount(sh);
                sb.AppendLine($"[dump] SHADER {path} name={sh.name} messages={n}");
                if (n > 0) Append(sb, ShaderUtil.GetShaderMessages(sh));
            }

            System.IO.File.WriteAllText(RigPath("shader-error-dump.txt"), sb.ToString());
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }

        private static void Append(StringBuilder sb, ShaderMessage[] msgs)
        {
            foreach (ShaderMessage m in msgs)
                sb.AppendLine($"[dump]   {m.severity} [{m.platform}] {m.file}({m.line}): {m.message} || {m.messageDetails}");
        }
    }
}
