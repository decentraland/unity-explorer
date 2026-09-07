using System.Text;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    // Batchmode diagnostic: lists every Mesh sub-asset under the landscape asset
    // folders with its guid + local fileID (the pair a serialized reference needs),
    // plus the attributes that matter for a grass-blade substitute (vertex colors,
    // UVs, vertex count). Used to rewire Grass_Indirect.asset's dangling mesh
    // reference to an in-repo mesh.
    //   Unity -batchmode -nographics -projectPath Explorer \
    //         -executeMethod Editor.GrassMeshProbe.Run -logFile <log>
    // Output: ~/.dcl-rig/grass-mesh-probe.txt
    public static class GrassMeshProbe
    {
        private static readonly string[] ROOTS =
        {
            "Assets/DCL/Landscape/Assets",
            "Packages/decentraland.grassshader",
        };

        private static string RigPath(string fileName) =>
            System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".dcl-rig", fileName);

        public static void Run()
        {
            var sb = new StringBuilder();

            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", ROOTS))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (sub is not Mesh mesh) continue;

                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string meshGuid, out long fileId))
                        continue;

                    sb.AppendLine($"[mesh] {path} name='{mesh.name}' guid={meshGuid} fileID={fileId} " +
                                  $"verts={mesh.vertexCount} submeshes={mesh.subMeshCount} colors={(mesh.colors.Length > 0 ? "yes" : "no")} uv={(mesh.uv.Length > 0 ? "yes" : "no")}");
                }
            }

            System.IO.File.WriteAllText(RigPath("grass-mesh-probe.txt"), sb.ToString());
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }

        // Verifies the four terrain detail assets resolve their Mesh + Material references
        // (a dangling GUID deserializes to null and disables that detail at runtime).
        public static void VerifyDetailAssets()
        {
            var sb = new StringBuilder();

            var terrainData = AssetDatabase.LoadAssetAtPath<DCL.Landscape.Settings.TerrainGenerationData>(
                "Assets/DCL/Landscape/Data/TerrainData.asset");

            if (terrainData == null)
                sb.AppendLine("[verify] TerrainData.asset LOAD FAILED");
            else
                foreach (DCL.Landscape.Config.LandscapeAsset detail in terrainData.detailAssets)
                {
                    Mesh? mesh = detail.TerrainDetailSettings.Mesh;
                    Material? mat = detail.TerrainDetailSettings.Material;
                    sb.AppendLine($"[verify] {detail.name}: mesh={(mesh != null ? mesh.name : "NULL")} material={(mat != null ? mat.name : "NULL")}");
                }

            System.IO.File.WriteAllText(RigPath("grass-wiring-verify.txt"), sb.ToString());
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }
    }
}
