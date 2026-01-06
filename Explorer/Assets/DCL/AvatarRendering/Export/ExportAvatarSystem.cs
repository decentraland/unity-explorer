using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UniGLTF;
using UnityEngine;
using Utility;
using VRM;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Export
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.AVATAR_EXPORT)]
    public sealed partial class ExportAvatarSystem : BaseUnityLoopSystem
    {
        private readonly IEventBus analyticsEventBus;
        private readonly GltfExportSettings gltfExportSettings = new ();
        private readonly RuntimeTextureSerializer textureSerializer = new ();
        private readonly ExportingState exportingState = new ();
        private readonly AvatarBase avatarBasePrefab;
        private CancellationTokenSource cts;

        private ExportAvatarSystem(World world, AvatarBase avatarBasePrefab,
            IEventBus analyticsEventBus) : base(world)
        {
            this.avatarBasePrefab = avatarBasePrefab;
            this.analyticsEventBus = analyticsEventBus;
        }

        protected override void Update(float deltaTime)
        {
            ProcessVrmExportQuery(World);
        }

        [Query]
        [All(typeof(VrmExportDataComponent))]
        private void ProcessVrmExport(in Entity entity, ref VrmExportDataComponent exportData)
        {
            cts = cts.SafeRestart();
            ExportToVrmAsync(entity, exportData, cts.Token).Forget();
            World.Remove(entity, typeof(VrmExportDataComponent));
        }

        private async UniTaskVoid ExportToVrmAsync(Entity entity, VrmExportDataComponent exportData, CancellationToken ctsToken)
        {
            using var _ = exportingState;

            using var meshCollector = new WearableMeshCollector();

            using var materialConverter = new VrmMaterialConverter(
                exportData.SkinColor,
                exportData.HairColor,
                exportData.EyesColor,
                exportData.FacialFeatureMainTextures,
                exportData.FacialFeatureMaskTextures);

            var vrmMetaObject = AvatarExportUtilities.CreateVrmMetaObject(
                exportData.AuthorName, exportData.WearableInfos);

            exportingState.Add(vrmMetaObject);

            try
            {
                // Build skeleton
                var skeletonBuilder = new ExportSkeletonBuilder();
                var skeleton = skeletonBuilder.BuildFromAvatarBase(exportData.AvatarBase, exportData.InstantiatedWearables);

                if (skeleton == null)
                {
                    ReportHub.LogError(GetReportCategory(), "Failed to create skeleton");
                    return;
                }

                exportingState.Add(skeleton.Root);
                skeleton.Root.transform.position = Vector3.zero;

                var humanBones = skeleton.ToHumanBoneDictionary();
                AvatarExportUtilities.EnforceDCLTPose(humanBones);

                // Collect meshes
                var collectedMeshes = meshCollector.CollectFromWearables(exportData.InstantiatedWearables);

                // Attach meshes with converted materials
                var boneRemapper = new ExportSkeletonBoneRemapper(skeleton);

                foreach (var meshData in collectedMeshes)
                {
                    meshData.Materials = materialConverter.ConvertMaterials(meshData.Materials, meshData.Name);

                    if (meshData.IsSkinnedMesh)
                        AttachSkinnedMesh(meshData, skeleton, boneRemapper);
                    else
                        AttachStaticMesh(meshData, skeleton, boneRemapper);
                }

                // Create humanoid avatar
                var avatar = AvatarExportUtilities.CreateHumanoidAvatar(
                    skeleton.Root, humanBones);

                if (avatar == null || !avatar.isValid)
                {
                    ReportHub.LogError(GetReportCategory(), "Failed to create valid humanoid avatar");
                    return;
                }

                // Normalize and export
                var normalizedRoot = skeleton.Root; //VRMBoneNormalizer.Execute(skeleton.Root, true, false);
                exportingState.Add(normalizedRoot);
                normalizedRoot.AddComponent<VRMMeta>().Meta = vrmMetaObject;

                var vrmData = VRMExporter.Export(gltfExportSettings, normalizedRoot, textureSerializer);
                byte[] vrmBytes = vrmData.ToGlbBytes();

                string directory = Path.GetDirectoryName(exportData.SavePath);

                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                await File.WriteAllBytesAsync(exportData.SavePath, vrmBytes, ctsToken);

                exportData.OnFinishedAction?.Invoke();
                analyticsEventBus?.Publish(new AvatarExportEvents(true));

                ReportHub.Log(GetReportCategory(), $"VRM exported successfully to: {exportData.SavePath}");
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, GetReportCategory());
                analyticsEventBus?.Publish(new AvatarExportEvents(false));
            }
            finally
            {
                exportData.Cleanup();
                World.Destroy(entity);
            }
        }

        private void AttachSkinnedMesh(
            CollectedMeshData meshData,
            ExportSkeletonMapping skeleton,
            ExportSkeletonBoneRemapper exportSkeletonBoneRemapper)
        {
            var meshGO = new GameObject(meshData.Name);
            meshGO.transform.SetParent(skeleton.Root.transform);
            meshGO.transform.ResetLocalTRS();

            var targetRenderer = meshGO.AddComponent<SkinnedMeshRenderer>();

            var clonedMesh = AvatarExportUtilities.CloneAndRecalculateBounds(meshData.SharedMesh);
            exportingState.Add(clonedMesh);

            var remappedBones = exportSkeletonBoneRemapper.RemapBones(meshData.SourceBones, meshData.SourceBoneNames);

            Transform rootBone = null;

            if (!string.IsNullOrEmpty(meshData.RootBoneName)) { rootBone = exportSkeletonBoneRemapper.GetTargetBone(meshData.RootBoneName); }

            rootBone ??= skeleton.GetByHumanBone(HumanBodyBones.Hips);

            var bindPoses = new Matrix4x4[remappedBones.Length];

            for (var i = 0; i < remappedBones.Length; i++)
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
                for (var i = 0; i < meshData.BlendShapeWeights.Length; i++)
                    targetRenderer.SetBlendShapeWeight(i, meshData.BlendShapeWeights[i]);
        }

        private void AttachStaticMesh(
            CollectedMeshData meshData,
            ExportSkeletonMapping skeleton,
            ExportSkeletonBoneRemapper exportSkeletonBoneRemapper)
        {
            Transform attachBone = exportSkeletonBoneRemapper.FindAttachmentBone(meshData.OriginalParentPath)
                                   ?? skeleton.GetByHumanBone(HumanBodyBones.Hips);

            var meshGO = new GameObject(meshData.Name);
            meshGO.transform.SetParent(attachBone);
            meshGO.transform.ResetLocalTRS();

            var targetRenderer = meshGO.AddComponent<SkinnedMeshRenderer>();

            var clonedMesh = AvatarExportUtilities.CloneAndRecalculateBounds(meshData.SharedMesh);
            exportingState.Add(clonedMesh);

            // Create single-bone skinning
            var boneWeights = new BoneWeight[clonedMesh.vertexCount];

            for (var i = 0; i < boneWeights.Length; i++)
                boneWeights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };

            clonedMesh.boneWeights = boneWeights;
            clonedMesh.bindposes = new[] { attachBone.worldToLocalMatrix };

            targetRenderer.sharedMesh = clonedMesh;
            targetRenderer.sharedMaterials = meshData.Materials;
            targetRenderer.bones = new[] { attachBone };
            targetRenderer.rootBone = attachBone;
            targetRenderer.localBounds = clonedMesh.bounds;
        }

        internal class ExportingState : IDisposable
        {
            private readonly List<Object> objects = new ();

            public void Add(Object obj) =>
                objects.Add(obj);

            public void Dispose()
            {
                foreach (Object obj in objects)
                    UnityObjectUtils.SafeDestroy(obj);

                objects.Clear();
            }
        }
    }
}
