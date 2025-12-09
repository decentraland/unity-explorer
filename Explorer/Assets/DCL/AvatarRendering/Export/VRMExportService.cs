using System;
using System.Collections.Generic;
using System.Linq;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.Diagnostics;
using UniHumanoid;
using UnityEngine;
using Utility;
using VRM;
using VRMShaders;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Export
{
    public class VRMExportService : IDisposable
    {
        private readonly UniGLTF.GltfExportSettings gltfExportSettings = new();
        private readonly RuntimeTextureSerializer textureSerializer = new();
        private readonly List<Object> disposables = new();

        public byte[] ExportToVRM(
            AvatarBase avatarBase,
            IReadOnlyList<CachedAttachment> instantiatedWearables,
            VRMExportSettings settings)
        {
            var meshCollector = new WearableMeshCollector();
            
            try
            {
                var skeletonBuilder = new ExportSkeletonBuilder();
                var skeleton = skeletonBuilder.BuildFromAvatarBase(avatarBase, instantiatedWearables);
                skeleton.Root.transform.position = Vector3.zero;
                var humanBones = skeleton.ToHumanBoneDictionary();
                EnforceDCLTPose(humanBones);
                //Physics.SyncTransforms();

                GameObject exportRoot = skeleton.Root;
                disposables.Add(exportRoot);

                var collectedMeshes = meshCollector.CollectFromWearables(instantiatedWearables);

                var boneRemapper = new BoneRemapper(skeleton);
                foreach (var meshData in collectedMeshes)
                {
                    if (meshData.IsSkinnedMesh)
                    {
                        AttachSkinnedMesh(meshData, skeleton, boneRemapper);
                    }
                    else
                    {
                        AttachStaticMesh(meshData, skeleton, boneRemapper);
                    }
                }
                
                //RecalculateAllBindPoses(exportRoot);

                var avatar = CreateHumanoidAvatar(skeleton, humanBones);
                if (avatar == null || !avatar.isValid)
                {
                    ReportHub.LogError(ReportCategory.AVATAR, "VRMExport: Failed to create valid humanoid avatar!");
                    return null;
                }

                var normalizedRoot = VRMBoneNormalizer.Execute(exportRoot, true);
                disposables.Add(normalizedRoot);

                //var vrmData = VRMExporter.Export(gltfExportSettings, exportRoot, textureSerializer);
                var vrmData = VRMExporter.Export(gltfExportSettings, normalizedRoot, textureSerializer);
                return vrmData.ToGlbBytes();
            }
            catch (Exception e)
            {
                Debug.LogError("[VRMExport] Export failed: " + e);
                throw;
            }
            finally
            {
                meshCollector.Cleanup();
            }
        }

        private void AttachSkinnedMesh(
            CollectedMeshData meshData,
            ExportSkeletonMapping skeleton,
            BoneRemapper boneRemapper)
        {
            var meshGO = new GameObject(meshData.Name);
            meshGO.transform.SetParent(skeleton.Root.transform);
            meshGO.transform.ResetLocalTRS();

            var targetRenderer = meshGO.AddComponent<SkinnedMeshRenderer>();

            var clonedMesh = CloneAndScaleMesh(meshData.SharedMesh, Vector3.one);
            disposables.Add(clonedMesh);
            
            var remappedBones = boneRemapper.RemapBones(meshData.SourceBones, meshData.SourceBoneNames);

            Transform rootBone = null;
            if (!string.IsNullOrEmpty(meshData.RootBoneName))
            {
                rootBone = boneRemapper.GetTargetBone(meshData.RootBoneName);
            }
            rootBone ??= skeleton.GetByHumanBone(HumanBodyBones.Hips);

            // Calculate bind poses for the new skeleton
            // Formula: bindPose = bone.worldToLocalMatrix * mesh.localToWorldMatrix
            // This transforms: mesh-local space → world space → bone-local space
            var bindPoses = new Matrix4x4[remappedBones.Length];
            
            for (int i = 0; i < remappedBones.Length; i++)
            {
                if (remappedBones[i] != null)
                {
                    bindPoses[i] = remappedBones[i].worldToLocalMatrix;
                }
                else
                {
                    // Fallback to root bone for unmapped bones
                    bindPoses[i] = rootBone != null 
                        ? rootBone.worldToLocalMatrix 
                        : Matrix4x4.identity;
                }
            }
            clonedMesh.bindposes = bindPoses;

            // Assign to renderer
            targetRenderer.sharedMesh = clonedMesh;
            targetRenderer.sharedMaterials = meshData.Materials;
            targetRenderer.bones = remappedBones;
            targetRenderer.rootBone = rootBone;
            targetRenderer.localBounds = clonedMesh.bounds;

            // Copy blend shape weights
            if (meshData.BlendShapeWeights != null)
            {
                for (int i = 0; i < meshData.BlendShapeWeights.Length; i++)
                {
                    targetRenderer.SetBlendShapeWeight(i, meshData.BlendShapeWeights[i]);
                }
            }
        }
        
        // private void AttachSkinnedMesh(
        //     CollectedMeshData meshData,
        //     ExportSkeletonMapping skeleton,
        //     BoneRemapper boneRemapper)
        // {
        //     var meshGO = new GameObject(meshData.Name);
        //     meshGO.transform.SetParent(skeleton.Root.transform);
        //
        //     var targetRenderer = meshGO.AddComponent<SkinnedMeshRenderer>();
        //
        //     var clonedMesh = CloneAndScaleMesh(meshData.SharedMesh, skeleton.MeshScale);
        //     disposables.Add(clonedMesh);
        //
        //     // targetRenderer.sharedMesh = clonedMesh;
        //     // targetRenderer.sharedMaterials = meshData.Materials;
        //
        //     // Remap bones
        //     var remappedBones = boneRemapper.RemapBones(meshData.SourceBones, meshData.SourceBoneNames);
        //     //targetRenderer.bones = remappedBones;
        //
        //     // Set root bone
        //     Transform rootBone = null;
        //     if (!string.IsNullOrEmpty(meshData.RootBoneName))
        //     {
        //         rootBone = boneRemapper.GetTargetBone(meshData.RootBoneName);
        //     }
        //     rootBone ??= skeleton.GetByHumanBone(HumanBodyBones.Hips);
        //
        //     // Calculate bind poses for the new skeleton
        //     // Formula: bindPose = bone.worldToLocalMatrix * mesh.localToWorldMatrix
        //     // This transforms: mesh-local space → world space → bone-local space
        //     var bindPoses = new Matrix4x4[remappedBones.Length];
        //     Matrix4x4 meshLocalToWorld = meshGO.transform.localToWorldMatrix;
        //     
        //     for (int i = 0; i < remappedBones.Length; i++)
        //     {
        //         if (remappedBones[i] != null)
        //         {
        //             bindPoses[i] = remappedBones[i].worldToLocalMatrix * meshLocalToWorld;
        //         }
        //         else
        //         {
        //             // Fallback to root bone for unmapped bones
        //             bindPoses[i] = rootBone != null 
        //                 ? rootBone.worldToLocalMatrix * meshLocalToWorld 
        //                 : Matrix4x4.identity;
        //         }
        //     }
        //     clonedMesh.bindposes = bindPoses;
        //
        //     // Assign to renderer
        //     targetRenderer.sharedMesh = clonedMesh;
        //     targetRenderer.sharedMaterials = meshData.Materials;
        //     targetRenderer.bones = remappedBones;
        //     targetRenderer.rootBone = rootBone;
        //     targetRenderer.localBounds = clonedMesh.bounds;
        //
        //     // Copy blend shape weights
        //     if (meshData.BlendShapeWeights != null)
        //     {
        //         for (int i = 0; i < meshData.BlendShapeWeights.Length; i++)
        //         {
        //             targetRenderer.SetBlendShapeWeight(i, meshData.BlendShapeWeights[i]);
        //         }
        //     }
        // }

        private void AttachStaticMesh(
            CollectedMeshData meshData,
            ExportSkeletonMapping skeleton,
            BoneRemapper boneRemapper)
        {
            Transform attachBone = boneRemapper.FindAttachmentBone(meshData.OriginalParentPath)
                ?? skeleton.GetByHumanBone(HumanBodyBones.Hips);

            var meshGO = new GameObject(meshData.Name);
            meshGO.transform.SetParent(attachBone);

            meshGO.transform.localPosition = Vector3.Scale(meshData.LocalPosition, skeleton.MeshScale);
            meshGO.transform.localRotation = meshData.LocalRotation;

            var targetRenderer = meshGO.AddComponent<SkinnedMeshRenderer>();

            var clonedMesh = CloneAndScaleMesh(meshData.SharedMesh, skeleton.MeshScale);
            disposables.Add(clonedMesh);

            // Create single-bone skinning
            var boneWeights = new BoneWeight[clonedMesh.vertexCount];
            for (int i = 0; i < boneWeights.Length; i++)
            {
                boneWeights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            }
            clonedMesh.boneWeights = boneWeights;
            
            // Bind pose: mesh local -> world -> bone local
            clonedMesh.bindposes = new Matrix4x4[] 
            { 
                attachBone.worldToLocalMatrix * meshGO.transform.localToWorldMatrix 
            };

            targetRenderer.sharedMesh = clonedMesh;
            targetRenderer.sharedMaterials = meshData.Materials;
            targetRenderer.bones = new Transform[] { attachBone };
            targetRenderer.rootBone = attachBone;
            targetRenderer.localBounds = clonedMesh.bounds;


        }

        /// <summary>
        /// Clones mesh and scales vertices by the given scale factor.
        /// This bakes the armature scale (0.01) into the mesh vertices.
        /// </summary>
        private Mesh CloneAndScaleMesh(Mesh source, Vector3 scale)
        {
            var mesh = Object.Instantiate(source);
            mesh.name = source.name;

            // Scale vertices
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = Vector3.Scale(vertices[i], scale);
            }
            mesh.vertices = vertices;

            // Recalculate bounds
            mesh.RecalculateBounds();

            // Scale blend shapes
            if (source.blendShapeCount > 0)
            {
                ScaleBlendShapes(mesh, source, scale);
            }

            return mesh;
        }

        private void ScaleBlendShapes(Mesh targetMesh, Mesh sourceMesh, Vector3 scale)
        {
            var blendShapeData = new List<(string name, List<(Vector3[] deltaVerts, Vector3[] deltaNormals, Vector3[] deltaTangents, float weight)> frames)>();

            for (int shapeIndex = 0; shapeIndex < sourceMesh.blendShapeCount; shapeIndex++)
            {
                string shapeName = sourceMesh.GetBlendShapeName(shapeIndex);
                int frameCount = sourceMesh.GetBlendShapeFrameCount(shapeIndex);
                var frames = new List<(Vector3[], Vector3[], Vector3[], float)>();

                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    var deltaVertices = new Vector3[sourceMesh.vertexCount];
                    var deltaNormals = new Vector3[sourceMesh.vertexCount];
                    var deltaTangents = new Vector3[sourceMesh.vertexCount];

                    sourceMesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                    float weight = sourceMesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);

                    for (int i = 0; i < deltaVertices.Length; i++)
                    {
                        deltaVertices[i] = Vector3.Scale(deltaVertices[i], scale);
                    }

                    frames.Add((deltaVertices, deltaNormals, deltaTangents, weight));
                }

                blendShapeData.Add((shapeName, frames));
            }

            targetMesh.ClearBlendShapes();

            foreach (var (name, frames) in blendShapeData)
            {
                foreach (var (deltaVerts, deltaNormals, deltaTangents, weight) in frames)
                {
                    targetMesh.AddBlendShapeFrame(name, weight, deltaVerts, deltaNormals, deltaTangents);
                }
            }
        }

        private Avatar CreateHumanoidAvatar(ExportSkeletonMapping skeleton, Dictionary<HumanBodyBones, Transform> humanBones)
        {
            var animator = skeleton.Root.GetComponent<Animator>() ?? skeleton.Root.AddComponent<Animator>();

            var requiredBones = new[]
            {
                HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
                HumanBodyBones.Neck, HumanBodyBones.Head,
                HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
            };

            var missingBones = requiredBones.Where(b => !humanBones.ContainsKey(b)).ToList();
            if (missingBones.Count > 0)
            {
                Debug.LogError("[VRMExport] Missing required bones: " + string.Join(", ", missingBones));
                return null;
            }

            var avatarDescription = AvatarDescription.Create();
            avatarDescription.SetHumanBones(humanBones);

            var avatar = avatarDescription.CreateAvatar(skeleton.Root.transform);

            if (avatar.isValid)
            {
                avatar.name = "DCL_Export_Avatar";
                animator.avatar = avatar;
                Debug.Log("VRMExport: Created valid humanoid avatar");
            }
            else
                Debug.LogError("VRMExport: Avatar is not valid!");

            return avatar;
        }

        private void EnforceDCLTPose(Dictionary<HumanBodyBones, Transform> bones)
        {
            // Reset all to identity first
            foreach (var bone in bones.Values)
                bone.localRotation = Quaternion.identity;

            // DCL-specific corrections
            if (bones.TryGetValue(HumanBodyBones.Hips, out var hips))
                hips.localRotation = Quaternion.Euler(-90, 0, -180);
            
            if (bones.TryGetValue(HumanBodyBones.Spine, out var spine))
                spine.localRotation = Quaternion.Euler(0, 0, -180);

            if (bones.TryGetValue(HumanBodyBones.LeftShoulder, out var leftShoulder))
                leftShoulder.localRotation = Quaternion.Euler(0, -180, -90);

            if (bones.TryGetValue(HumanBodyBones.RightShoulder, out var rightShoulder))
                rightShoulder.localRotation = Quaternion.Euler(0, 0, -90);

            if (bones.TryGetValue(HumanBodyBones.LeftFoot, out var leftFoot))
                leftFoot.localRotation = Quaternion.Euler(-90, 180, 0);

            if (bones.TryGetValue(HumanBodyBones.RightFoot, out var rightFoot))
                rightFoot.localRotation = Quaternion.Euler(-90, 180, 0);

            Debug.Log("[VRMExport] Applied T-pose");
        }

        public void Dispose()
        {
            return;
            foreach (var obj in disposables)
            {
                if (obj != null)
                    Object.Destroy(obj);
            }
            disposables.Clear();
        }
    }
}
