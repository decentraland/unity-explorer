using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Export
{
    public static class ExportAvatarUtilities
    {

        private static Mesh BakeScaleIntoMesh(Mesh sourceMesh, Vector3 scale)
        {
            Mesh bakedMesh = Object.Instantiate(sourceMesh);
            bakedMesh.name = sourceMesh.name + "_Scaled";

            // Scale vertices
            Vector3[] vertices = bakedMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = Vector3.Scale(vertices[i], scale);
            }

            bakedMesh.vertices = vertices;

            // Scale bounds
            bakedMesh.RecalculateBounds();

            // Scale bind poses if present
            if (bakedMesh.bindposes != null && bakedMesh.bindposes.Length > 0)
            {
                Matrix4x4[] bindPoses = bakedMesh.bindposes;
                Matrix4x4 scaleMatrix = Matrix4x4.Scale(scale);
                Matrix4x4 inverseScaleMatrix = Matrix4x4.Scale(new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z));

                for (int i = 0; i < bindPoses.Length; i++)
                {
                    // Bind pose needs to account for scaled vertices
                    bindPoses[i] = bindPoses[i] * inverseScaleMatrix;
                }

                bakedMesh.bindposes = bindPoses;
            }

            // Scale blend shapes if present
            if (bakedMesh.blendShapeCount > 0)
            {
                // Unity doesn't allow direct modification of blend shapes
                // We need to recreate them
                var blendShapeData = new List<(string name, (Vector3[] deltaVertices, Vector3[] deltaNormals, Vector3[] deltaTangents, float weight)[] frames)>();

                for (int shapeIndex = 0; shapeIndex < sourceMesh.blendShapeCount; shapeIndex++)
                {
                    string shapeName = sourceMesh.GetBlendShapeName(shapeIndex);
                    int frameCount = sourceMesh.GetBlendShapeFrameCount(shapeIndex);
                    var frames = new (Vector3[], Vector3[], Vector3[], float)[frameCount];

                    for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                    {
                        Vector3[] deltaVertices = new Vector3[sourceMesh.vertexCount];
                        Vector3[] deltaNormals = new Vector3[sourceMesh.vertexCount];
                        Vector3[] deltaTangents = new Vector3[sourceMesh.vertexCount];

                        sourceMesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                        float weight = sourceMesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);

                        // Scale delta vertices
                        for (int i = 0; i < deltaVertices.Length; i++)
                        {
                            deltaVertices[i] = Vector3.Scale(deltaVertices[i], scale);
                        }

                        frames[frameIndex] = (deltaVertices, deltaNormals, deltaTangents, weight);
                    }

                    blendShapeData.Add((shapeName, frames));
                }

                // Clear and re-add blend shapes
                bakedMesh.ClearBlendShapes();

                foreach (var (name, frames) in blendShapeData)
                {
                    foreach (var (deltaVertices, deltaNormals, deltaTangents, weight) in frames)
                    {
                        bakedMesh.AddBlendShapeFrame(name, weight, deltaVertices, deltaNormals, deltaTangents);
                    }
                }
            }

            return bakedMesh;
        }
        
        /// <summary>
        /// Check if a mesh has bone weight data that could be used for skinning
        /// </summary>
        public static bool HasBoneWeights(Mesh mesh)
        {
            if (mesh == null) return false;
            
            BoneWeight[] weights = mesh.boneWeights;
            return weights != null && weights.Length > 0;
        }
        
        /// <summary>
        /// Check if a mesh has bind poses (required for SkinnedMeshRenderer)
        /// </summary>
        public static bool HasBindPoses(Mesh mesh)
        {
            if (mesh == null) return false;
            
            Matrix4x4[] bindPoses = mesh.bindposes;
            return bindPoses != null && bindPoses.Length > 0;
        }
        
        /// <summary>
        /// Debug all meshes in cached data to see which have bone data
        /// </summary>
        public static void DebugMeshBoneData(List<ExportAvatarSystem.CachedMeshData> cachedMeshes)
        {
            Debug.Log("=== MESH BONE DATA ANALYSIS ===");
            
            int skinnedCount = 0;
            int rigidWithBones = 0;
            int rigidWithoutBones = 0;
            
            foreach (var meshData in cachedMeshes)
            {
                if (meshData.IsSkinnedMesh)
                {
                    skinnedCount++;
                    continue;
                }
                
                // Check if rigid mesh has bone data
                bool hasBoneWeights = HasBoneWeights(meshData.Mesh);
                bool hasBindPoses = HasBindPoses(meshData.Mesh);
                
                if (hasBoneWeights || hasBindPoses)
                {
                    rigidWithBones++;
                    Debug.Log($"<color=yellow>RIGID WITH BONES</color>: {meshData.Name}");
                    Debug.Log($"  - Has BoneWeights: {hasBoneWeights} ({meshData.Mesh.boneWeights?.Length ?? 0} weights)");
                    Debug.Log($"  - Has BindPoses: {hasBindPoses} ({meshData.Mesh.bindposes?.Length ?? 0} poses)");
                    
                    if (hasBoneWeights)
                    {
                        // Sample first bone weight
                        BoneWeight firstWeight = meshData.Mesh.boneWeights[0];
                        Debug.Log($"  - Sample weight: bone0={firstWeight.boneIndex0} ({firstWeight.weight0}), " +
                                  $"bone1={firstWeight.boneIndex1} ({firstWeight.weight1})");
                    }
                }
                else
                {
                    rigidWithoutBones++;
                    Debug.Log($"<color=red>RIGID WITHOUT BONES</color>: {meshData.Name}");
                    Debug.Log($"  - This is a truly rigid accessory (no skinning data)");
                }
            }
            
            Debug.Log($"\n=== SUMMARY ===");
            Debug.Log($"Skinned meshes: {skinnedCount}");
            Debug.Log($"Rigid with bone data: <color=yellow>{rigidWithBones}</color> (CAN be converted to SkinnedMeshRenderer)");
            Debug.Log($"Rigid without bone data: <color=red>{rigidWithoutBones}</color> (CANNOT be skinned, must stay rigid)");
        }
        
        /// <summary>
        /// Detailed analysis of a specific mesh
        /// </summary>
        public static void AnalyzeMesh(Mesh mesh, string name)
        {
            Debug.Log($"\n=== ANALYZING MESH: {name} ===");
            Debug.Log($"Vertex Count: {mesh.vertexCount}");
            Debug.Log($"Triangle Count: {mesh.triangles.Length / 3}");
            
            // Bone weights
            BoneWeight[] weights = mesh.boneWeights;
            if (weights != null && weights.Length > 0)
            {
                Debug.Log($"<color=green>✓ Has Bone Weights: {weights.Length}</color>");
                
                // Find unique bone indices used
                var uniqueBones = new System.Collections.Generic.HashSet<int>();
                foreach (var weight in weights)
                {
                    if (weight.weight0 > 0) uniqueBones.Add(weight.boneIndex0);
                    if (weight.weight1 > 0) uniqueBones.Add(weight.boneIndex1);
                    if (weight.weight2 > 0) uniqueBones.Add(weight.boneIndex2);
                    if (weight.weight3 > 0) uniqueBones.Add(weight.boneIndex3);
                }
                Debug.Log($"  Unique bones referenced: {uniqueBones.Count}");
            }
            else
            {
                Debug.Log("<color=red>✗ No Bone Weights</color>");
            }
            
            // Bind poses
            Matrix4x4[] bindPoses = mesh.bindposes;
            if (bindPoses != null && bindPoses.Length > 0)
            {
                Debug.Log($"<color=green>✓ Has Bind Poses: {bindPoses.Length}</color>");
            }
            else
            {
                Debug.Log("<color=red>✗ No Bind Poses</color>");
            }
            
            // Blend shapes
            if (mesh.blendShapeCount > 0)
            {
                Debug.Log($"<color=cyan>Has Blend Shapes: {mesh.blendShapeCount}</color>");
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    Debug.Log($"  - {mesh.GetBlendShapeName(i)}");
                }
            }
        }
    }
}