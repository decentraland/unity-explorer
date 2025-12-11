using System;
using System.Collections.Generic;
using System.Linq;
using DCL.AvatarRendering.AvatarShape.Components;
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
            in AvatarShapeComponent avatarShape,
            AvatarBase avatarBase,
            IReadOnlyList<CachedAttachment> instantiatedWearables,
            VRMExportSettings settings)
        {
            var meshCollector = new WearableMeshCollector();
            VRMaterialConverter materialConverter = null;
            
            try
            {
                // Create material converter with avatar colors and facial textures
                materialConverter = new VRMaterialConverter(
                    avatarShape.SkinColor,
                    avatarShape.HairColor,
                    avatarShape.EyesColor,
                    avatarShape.FacialFeatureMainTexturesForExport,
                    avatarShape.FacialFeatureMaskTexturesForExport);
                
                var skeletonBuilder = new ExportSkeletonBuilder();
                var skeleton = skeletonBuilder.BuildFromAvatarBase(avatarBase, instantiatedWearables);
                skeleton.Root.transform.position = Vector3.zero;
                var humanBones = skeleton.ToHumanBoneDictionary();
                EnforceDCLTPose(humanBones);

                GameObject exportRoot = skeleton.Root;
                disposables.Add(exportRoot);

                var collectedMeshes = meshCollector.CollectFromWearables(instantiatedWearables);

                var boneRemapper = new BoneRemapper(skeleton);
                foreach (var meshData in collectedMeshes)
                {
                    meshData.Materials = materialConverter.ConvertMaterials(
                        meshData.Materials, 
                        meshData.Name);
                    
                    if (meshData.IsSkinnedMesh)
                        AttachSkinnedMesh(meshData, skeleton, boneRemapper);
                    else
                        AttachStaticMesh(meshData, skeleton, boneRemapper);
                }
                
                var avatar = CreateHumanoidAvatar(skeleton, humanBones);
                if (avatar == null || !avatar.isValid)
                {
                    ReportHub.LogError(ReportCategory.AVATAR_EXPORT, "VRMExport: Failed to create valid humanoid avatar!");
                    return null;
                }

                var normalizedRoot = VRMBoneNormalizer.Execute(exportRoot, true);
                disposables.Add(normalizedRoot);

                var vrmData = VRMExporter.Export(gltfExportSettings, normalizedRoot, textureSerializer);
                return vrmData.ToGlbBytes();
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.AVATAR_EXPORT);
                throw;
            }
            finally
            {
                meshCollector.Cleanup();
                materialConverter?.Cleanup();
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

            var clonedMesh = CloneMesh(meshData.SharedMesh);
            disposables.Add(clonedMesh);
            
            var remappedBones = boneRemapper.RemapBones(meshData.SourceBones, meshData.SourceBoneNames);

            Transform rootBone = null;
            if (!string.IsNullOrEmpty(meshData.RootBoneName))
            {
                rootBone = boneRemapper.GetTargetBone(meshData.RootBoneName);
            }
            rootBone ??= skeleton.GetByHumanBone(HumanBodyBones.Hips);

            var bindPoses = new Matrix4x4[remappedBones.Length];
            for (int i = 0; i < remappedBones.Length; i++)
            {
                if (remappedBones[i] != null)
                    bindPoses[i] = remappedBones[i].worldToLocalMatrix;
                else
                {
                    // Fallback to root bone for unmapped bones
                    bindPoses[i] = rootBone != null 
                        ? rootBone.worldToLocalMatrix 
                        : Matrix4x4.identity;
                }
            }
            clonedMesh.bindposes = bindPoses;

            targetRenderer.sharedMesh = clonedMesh;
            targetRenderer.sharedMaterials = meshData.Materials;
            targetRenderer.bones = remappedBones;
            targetRenderer.rootBone = rootBone;
            targetRenderer.localBounds = clonedMesh.bounds;

            if (meshData.BlendShapeWeights != null)
                for (int i = 0; i < meshData.BlendShapeWeights.Length; i++)
                    targetRenderer.SetBlendShapeWeight(i, meshData.BlendShapeWeights[i]);
        }

        private void AttachStaticMesh(
            CollectedMeshData meshData,
            ExportSkeletonMapping skeleton,
            BoneRemapper boneRemapper)
        {
            Transform attachBone = boneRemapper.FindAttachmentBone(meshData.OriginalParentPath)
                                   ?? skeleton.GetByHumanBone(HumanBodyBones.Hips);

            var meshGO = new GameObject(meshData.Name);
            meshGO.transform.SetParent(attachBone);
            meshGO.transform.ResetLocalTRS();

            var targetRenderer = meshGO.AddComponent<SkinnedMeshRenderer>();

            var clonedMesh = CloneMesh(meshData.SharedMesh);
            disposables.Add(clonedMesh);

            // Create single-bone skinning
            var boneWeights = new BoneWeight[clonedMesh.vertexCount];
            for (int i = 0; i < boneWeights.Length; i++)
                boneWeights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            
            clonedMesh.boneWeights = boneWeights;
            clonedMesh.bindposes = new Matrix4x4[] { attachBone.worldToLocalMatrix };

            targetRenderer.sharedMesh = clonedMesh;
            targetRenderer.sharedMaterials = meshData.Materials;
            targetRenderer.bones = new Transform[] { attachBone };
            targetRenderer.rootBone = attachBone;
            targetRenderer.localBounds = clonedMesh.bounds;
        }
        
        private Mesh CloneMesh(Mesh source)
        {
            var mesh = Object.Instantiate(source);
            mesh.name = source.name;
            mesh.RecalculateBounds();
            return mesh;
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
                ReportHub.LogError(ReportCategory.AVATAR_EXPORT, "Missing required bones: " + string.Join(", ", missingBones));
                return null;
            }

            var avatarDescription = AvatarDescription.Create();
            avatarDescription.SetHumanBones(humanBones);

            var avatar = avatarDescription.CreateAvatar(skeleton.Root.transform);

            if (avatar.isValid)
            {
                avatar.name = "DCL_Export_Avatar";
                animator.avatar = avatar;
                ReportHub.Log(ReportCategory.AVATAR_EXPORT,"Created valid humanoid avatar");
            }
            else
                ReportHub.LogError(ReportCategory.AVATAR_EXPORT,"Avatar is not valid!");

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

            ReportHub.Log(ReportCategory.AVATAR_EXPORT,"Applied T-pose");
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
