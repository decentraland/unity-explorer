using System;
using Arch.Core;
using Arch.System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Arch.SystemGroups;
using Crosstales.FB;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.StreamableLoading.Common;
using UniHumanoid;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using VRM;
using VRMShaders;
//using WearablesLoadResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Wearables.Components.WearablesResolution>;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Export
{
	[UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
	[LogCategory(ReportCategory.AVATAR)]
	public partial class ExportAvatarSystem : BaseUnityLoopSystem
	{
		private readonly IAttachmentsAssetsCache wearableAssetsCache;
		private readonly IWearableStorage wearableStorage;
		private readonly UniGLTF.GltfExportSettings gltsExportSettings = new ();
		private readonly RuntimeTextureSerializer textureSerializer = new ();
		private readonly List<Object> disposables = new();

		public ExportAvatarSystem(World world, /*IAttachmentsAssetsCache wearableAssetsCache,*/ IWearableStorage wearableStorage, VRMBonesMappingSO bonesMapping) : base(world)
		{
			this.wearableStorage = wearableStorage;
			this.wearableAssetsCache = wearableAssetsCache;
		}

		protected override void Update(float deltaTime)
		{
			ProcessExportIntentionsQuery(World);
		}

		[Query]
		[All(typeof(ExportAvatarIntention))]
		private void ProcessExportIntentions(in Entity entity, ref AvatarShapeComponent avatarShape)
		{
			// Check if avatar is ready
			if (avatarShape.IsDirty || !avatarShape.WearablePromise.IsConsumed)
			{
				ReportHub.LogWarning(GetReportCategory(), $"Avatar {avatarShape.Name} is not ready for export yet. IsDirty={avatarShape.IsDirty}");
				return;
			}

			// Get AvatarBase
			if (!World.TryGet(entity, out AvatarBase avatarBase) || avatarBase == null)
			{
				ReportHub.LogError(GetReportCategory(), $"Entity {entity} has no AvatarBase component");
				World.Remove<ExportAvatarIntention>(entity);
				return;
			}

			ReportHub.Log(GetReportCategory(), $"Processing export for avatar: {avatarShape.Name}");

			DebugAvatar(entity, ref avatarShape, avatarBase);
			Export(ref avatarShape, avatarBase).Forget();
			World.Remove<ExportAvatarIntention>(entity);
			World.Remove<AvatarExportData>(entity);
		}

		// private async UniTaskVoid ExportAvatar()
		// {
		//     
		// }

		private UniTaskVoid Export(ref AvatarShapeComponent avatarShape, AvatarBase avatarBase)
		{
			// TODO:
			// - Get all bones for VRMExporterUtils.CacheFBXBones(). Or figure out better way. 
			// - Add wearables bones
			// - Figure out how to pass mesh references, in original PR it's not directly passed reference.
			// - Create separate avatar, since modifying preview breaks (in asset promise?)
			// - Pass materials
			var exporterReferences = new VRMExportSettings();
			using var exportService = new VRMExportService();
			
			try
			{
				// TODO: Get this from settings as SO
				exporterReferences.metaObject = new VRMMetaObject
				{
					Version = "1.0, UniVRM v0.112.0",
					Author = "TODO: Get author name",
					Reference = "TODO: Get asset reference"
				};
				
				var wearables = avatarShape.InstantiatedWearables;
				
				if (wearables.Count == 0)
				{
					ReportHub.LogWarning(GetReportCategory(), "No wearables found to export");
					return default;
				}
				
				ReportHub.Log(GetReportCategory(), "Exporting " + wearables.Count + " wearables");

				byte[] vrmBytes = exportService.ExportToVRM(avatarBase, wearables, exporterReferences);

				if (vrmBytes == null || vrmBytes.Length == 0)
				{
					ReportHub.LogError(GetReportCategory(), "VRM export produced no data");
					return default;
				}

				string fileName = $"Avatar_{DateTime.Now:yyyyMMddhhmmss}";
				// //string savePath = FileBrowser.Instance.SaveFile("Save avatar VRM", Application.persistentDataPath, fileName, new ExtensionFilter("vrm", "vrm"));
				string savePath = "C://VRM/" + fileName + ".vrm";
				if (string.IsNullOrEmpty(savePath))
				{
					savePath = Path.Combine(
						Application.persistentDataPath,
						"Exports",
						"Avatar_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".vrm");
				}

				string directory = Path.GetDirectoryName(savePath);
				if (!string.IsNullOrEmpty(directory))
				{
					Directory.CreateDirectory(directory);
				}

				File.WriteAllBytes(savePath, vrmBytes);

				ReportHub.Log(GetReportCategory(), "VRM exported successfully to: " + savePath);
			}
			catch (Exception e)
			{
				ReportHub.LogException(e, GetReportCategory());
				throw;
			}
			
			// var originalWearables = avatarShape.WearablePromise.SafeTryConsume(World, GetReportCategory(), out WearablesLoadResult wearablesResult);
			//
			// HashSet<string> usedCategories = HashSetPool<string>.Get();
			// //GameObject originalAvatar = AvatarInstantiationPolymorphicBehaviour.AppendToAvatar(wearableAssetsCache, usedCategories, ref facialFeatureTextures, ref avatarShape, attachPoint)
			//
			// var skeleton = DuplicateSkeletonFromAvatarBase(avatarBase);
			// CopyMeshesToSkeleton(avatarBase, skeleton);
			//
			// Avatar avatar = CreateAvatarFromSkeleton(skeleton);
			// if (avatar == null || !avatar.isValid)
			// {
			// 	Debug.LogError("Failed to create valid avatar!");
			// 	UnityEngine.Object.Destroy(skeleton.Root);
			// 	return default;
			// }
			//
			// var bonesNormalized = VRMBoneNormalizer.Execute(skeleton.Root, true);
			// var vrmNormalized = VRMExporter.Export(gltsExportSettings, bonesNormalized, textureSerializer);
			//
			// string fileName = $"Avatar_{DateTime.Now:yyyyMMddhhmmss}";
			// //string savePath = FileBrowser.Instance.SaveFile("Save avatar VRM", Application.persistentDataPath, fileName, new ExtensionFilter("vrm", "vrm"));
			// string savePath = "C://VRM/" + fileName + ".vrm";
			//
			// if (!string.IsNullOrEmpty(savePath))
			// {
			// 	try
			// 	{
			// 		Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
			// 		File.WriteAllBytes(savePath, vrmNormalized.ToGlbBytes());
			// 	}
			// 	catch (Exception e)
			// 	{
			// 		Console.WriteLine(e);
			// 		throw;
			// 	}
			// }
			
			return default;
		}
		
		private ExportSkeletonMapping DuplicateSkeletonFromAvatarBase(AvatarBase avatarBase)
		{
			var armature = avatarBase.Armature;
			var duplicateRoot = new GameObject("DCL_Avatar");
			duplicateRoot.transform.position = armature.position;
			//duplicateRoot.transform.localScale = armature.localScale;

			duplicateRoot.AddComponent<Animator>();
			var boneRenderer = duplicateRoot.AddComponent<BoneRenderer>();

			var mapping = new ExportSkeletonMapping(duplicateRoot, avatarBase.Armature.localScale);

			// Define hierarchy structure: (humanBone, parent, source transform, source bone name)
			var boneHierarchy = new (HumanBodyBones bone, HumanBodyBones? parent, Transform source, string sourceName)[]
			{
				// Core bones
				(HumanBodyBones.Hips, null, avatarBase.HipAnchorPoint, "Hips"),
				(HumanBodyBones.Spine, HumanBodyBones.Hips, avatarBase.SpineAnchorPoint, "Spine"),
				(HumanBodyBones.Chest, HumanBodyBones.Spine, avatarBase.Spine1AnchorPoint, "Spine1"),
				(HumanBodyBones.UpperChest, HumanBodyBones.Chest, avatarBase.Spine2AnchorPoint, "Spine2"),
				(HumanBodyBones.Neck, HumanBodyBones.UpperChest, avatarBase.NeckAnchorPoint, "Neck"),
				(HumanBodyBones.Head, HumanBodyBones.Neck, avatarBase.HeadAnchorPoint, "Head"),

				// Left Arm
				(HumanBodyBones.LeftShoulder, HumanBodyBones.UpperChest, avatarBase.LeftShoulderAnchorPoint, "LeftShoulder"),
				(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftShoulder, avatarBase.LeftArmAnchorPoint, "LeftArm"),
				(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftUpperArm, avatarBase.LeftForearmAnchorPoint, "LeftForeArm"),
				(HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm, avatarBase.LeftHandAnchorPoint, "LeftHand"),

				// Left Fingers
				(HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.ThumbProximalAnchorPoint, "LeftHandThumb1"),
				(HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbProximal, avatarBase.LeftHandFingers.ThumbIntermediateAnchorPoint, "LeftHandThumb2"),
				(HumanBodyBones.LeftThumbDistal, HumanBodyBones.LeftThumbIntermediate, avatarBase.LeftHandFingers.ThumbDistalAnchorPoint, "LeftHandThumb3"),
				(HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.IndexProximalAnchorPoint, "LeftHandIndex1"),
				(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexProximal, avatarBase.LeftHandFingers.IndexIntermediateAnchorPoint, "LeftHandIndex2"),
				(HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftIndexIntermediate, avatarBase.LeftHandFingers.IndexDistalAnchorPoint, "LeftHandIndex3"),
				(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.MiddleProximalAnchorPoint, "LeftHandMiddle1"),
				(HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleProximal, avatarBase.LeftHandFingers.MiddleIntermediateAnchorPoint, "LeftHandMiddle2"),
				(HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftMiddleIntermediate, avatarBase.LeftHandFingers.MiddleDistalAnchorPoint, "LeftHandMiddle3"),
				(HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.RingProximalAnchorPoint, "LeftHandRing1"),
				(HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingProximal, avatarBase.LeftHandFingers.RingIntermediateAnchorPoint, "LeftHandRing2"),
				(HumanBodyBones.LeftRingDistal, HumanBodyBones.LeftRingIntermediate, avatarBase.LeftHandFingers.RingDistalAnchorPoint, "LeftHandRing3"),
				(HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.LittleProximalAnchorPoint, "LeftHandPinky1"),
				(HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleProximal, avatarBase.LeftHandFingers.LittleIntermediateAnchorPoint, "LeftHandPinky2"),
				(HumanBodyBones.LeftLittleDistal, HumanBodyBones.LeftLittleIntermediate, avatarBase.LeftHandFingers.LittleDistalAnchorPoint, "LeftHandPinky3"),

				// Right Arm
				(HumanBodyBones.RightShoulder, HumanBodyBones.UpperChest, avatarBase.RightShoulderAnchorPoint, "RightShoulder"),
				(HumanBodyBones.RightUpperArm, HumanBodyBones.RightShoulder, avatarBase.RightArmAnchorPoint, "RightArm"),
				(HumanBodyBones.RightLowerArm, HumanBodyBones.RightUpperArm, avatarBase.RightForearmAnchorPoint, "RightForeArm"),
				(HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, avatarBase.RightHandAnchorPoint, "RightHand"),

				// Right Fingers
				(HumanBodyBones.RightThumbProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.ThumbProximalAnchorPoint, "RightHandThumb1"),
				(HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbProximal, avatarBase.RightHandFingers.ThumbIntermediateAnchorPoint, "RightHandThumb2"),
				(HumanBodyBones.RightThumbDistal, HumanBodyBones.RightThumbIntermediate, avatarBase.RightHandFingers.ThumbDistalAnchorPoint, "RightHandThumb3"),
				(HumanBodyBones.RightIndexProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.IndexProximalAnchorPoint, "RightHandIndex1"),
				(HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexProximal, avatarBase.RightHandFingers.IndexIntermediateAnchorPoint, "RightHandIndex2"),
				(HumanBodyBones.RightIndexDistal, HumanBodyBones.RightIndexIntermediate, avatarBase.RightHandFingers.IndexDistalAnchorPoint, "RightHandIndex3"),
				(HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.MiddleProximalAnchorPoint, "RightHandMiddle1"),
				(HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleProximal, avatarBase.RightHandFingers.MiddleIntermediateAnchorPoint, "RightHandMiddle2"),
				(HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightMiddleIntermediate, avatarBase.RightHandFingers.MiddleDistalAnchorPoint, "RightHandMiddle3"),
				(HumanBodyBones.RightRingProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.RingProximalAnchorPoint, "RightHandRing1"),
				(HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingProximal, avatarBase.RightHandFingers.RingIntermediateAnchorPoint, "RightHandRing2"),
				(HumanBodyBones.RightRingDistal, HumanBodyBones.RightRingIntermediate, avatarBase.RightHandFingers.RingDistalAnchorPoint, "RightHandRing3"),
				(HumanBodyBones.RightLittleProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.LittleProximalAnchorPoint, "RightHandPinky1"),
				(HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleProximal, avatarBase.RightHandFingers.LittleIntermediateAnchorPoint, "RightHandPinky2"),
				(HumanBodyBones.RightLittleDistal, HumanBodyBones.RightLittleIntermediate, avatarBase.RightHandFingers.LittleDistalAnchorPoint, "RightHandPinky3"),

				// Left Leg
				(HumanBodyBones.LeftUpperLeg, HumanBodyBones.Hips, avatarBase.LeftUpLegAnchorPoint, "LeftUpLeg"),
				(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftUpperLeg, avatarBase.LeftLegAnchorPoint, "LeftLeg"),
				(HumanBodyBones.LeftFoot, HumanBodyBones.LeftLowerLeg, avatarBase.LeftFootAnchorPoint, "LeftFoot"),
				(HumanBodyBones.LeftToes, HumanBodyBones.LeftFoot, avatarBase.LeftToeBaseAnchorPoint, "LeftToeBase"),

				// Right Leg
				(HumanBodyBones.RightUpperLeg, HumanBodyBones.Hips, avatarBase.RightUpLegAnchorPoint, "RightUpLeg"),
				(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightUpperLeg, avatarBase.RightLegAnchorPoint, "RightLeg"),
				(HumanBodyBones.RightFoot, HumanBodyBones.RightLowerLeg, avatarBase.RightFootAnchorPoint, "RightFoot"),
				(HumanBodyBones.RightToes, HumanBodyBones.RightFoot, avatarBase.RightToeBaseAnchorPoint, "RightToeBase")
			};

			foreach ((var bone, var parent, var source, string sourceName) in boneHierarchy)
			{
				if (source == null)
					continue;

				Transform newParent;
				if (parent.HasValue)
				{
					if (!mapping.TryGetByHumanBone(parent.Value, out newParent))
						continue;
				}
				else
				{
					newParent = duplicateRoot.transform;
				}

				var newBoneGO = new GameObject(bone.ToString());
				var newBone = newBoneGO.transform;
				newBone.SetParent(newParent);
				newBone.SetPositionAndRotation(source.position, source.rotation);
				newBone.localScale = source.localScale;

				mapping.AddBone(new ExportBoneData(bone, parent, newBone, sourceName));
			}

			// TODO: Debug value, remove it
			duplicateRoot.transform.position += new Vector3(2f, 0f, 0f);

			boneRenderer.transforms = mapping.Bones.Select(b => b.TargetTransform).ToArray();

			Debug.Log($"Created skeleton with {mapping.BoneCount} bones");

			return mapping;
		}
		
		private void CopyMeshesToSkeleton(AvatarBase sourceAvatar, ExportSkeletonMapping skeleton)
		{
			var hipsTransform = skeleton.GetByHumanBone(HumanBodyBones.Hips);
			Transform avatarRoot = sourceAvatar.transform.parent;

			var sourceRenderers = sourceAvatar.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(false);

			int copiedCount = 0;
			int skippedCount = 0;

			foreach (var sourceRenderer in sourceRenderers)
			{
				if (!sourceRenderer.enabled || !sourceRenderer.gameObject.activeInHierarchy)
				{
					skippedCount++;
					continue;
				}

				if (sourceRenderer.sharedMesh == null)
				{
					skippedCount++;
					continue;
				}

				CopySkinnedMeshRenderer(sourceRenderer, skeleton, hipsTransform);
				copiedCount++;
			}

			var meshRenderers = sourceAvatar.gameObject.GetComponentsInChildren<MeshRenderer>(false);
			foreach (var sourceRenderer in meshRenderers)
			{
				if (!sourceRenderer.enabled || !sourceRenderer.gameObject.activeInHierarchy)
				{
					skippedCount++;
					continue;
				}

				var meshFilter = sourceRenderer.GetComponent<MeshFilter>();
				if (meshFilter == null || meshFilter.sharedMesh == null)
				{
					skippedCount++;
					continue;
				}

				//CopyMeshRenderer(sourceRenderer, meshFilter, skeleton);
				var wearableRoot = FindWearableRoot(sourceRenderer.transform, avatarRoot);
				ConvertMeshFilterToSkinnedMeshRenderer(sourceRenderer, meshFilter, wearableRoot, skeleton, hipsTransform);
				copiedCount++;
			}

			Debug.Log($"Mesh copy complete: {copiedCount} copied, {skippedCount} skipped");
		}
		
		/// <summary>
		/// Finds which wearable root a mesh belongs to
		/// </summary>
		private Transform FindWearableRoot(Transform meshTransform, Transform avatarRoot)
		{
			Transform current = meshTransform.parent;
    
			while (current != null && current != avatarRoot)
			{
				if (current.parent == avatarRoot)
					return current;
        
				current = current.parent;
			}
    
			return null;
		}

		private void CopySkinnedMeshRenderer(SkinnedMeshRenderer source, ExportSkeletonMapping skeleton, Transform defaultRootBone)
		{
			var meshGO = new GameObject($"Mesh_{source.name}");
			meshGO.transform.SetParent(skeleton.Root.transform);
			meshGO.transform.localPosition = Vector3.zero;
			meshGO.transform.localRotation = Quaternion.identity;
			meshGO.transform.localScale = new Vector3(
				source.transform.localScale.x * skeleton.MeshScale.x, 
				source.transform.localScale.y * skeleton.MeshScale.y, 
				source.transform.localScale.z * skeleton.MeshScale.z);

			var targetRenderer = meshGO.AddComponent<SkinnedMeshRenderer>();
			targetRenderer.sharedMesh = source.sharedMesh;
			targetRenderer.sharedMaterials = source.sharedMaterials;

			// Remap bones by name
			var sourceBones = source.bones;
			var targetBones = new Transform[sourceBones.Length];
			int unmappedCount = 0;

			for (int i = 0; i < sourceBones.Length; i++)
			{
				if (sourceBones[i] != null && skeleton.TryGetByBoneName(sourceBones[i].name, out var targetBone))
				{
					targetBones[i] = targetBone;
				}
				else
				{
					targetBones[i] = defaultRootBone;
					unmappedCount++;

					if (sourceBones[i] != null)
						Debug.LogWarning($"Unmapped bone '{sourceBones[i].name}' in '{source.name}'");
				}
			}

			targetRenderer.bones = targetBones;

			// Root bone
			if (source.rootBone != null && skeleton.TryGetByBoneName(source.rootBone.name, out var mappedRoot))
				targetRenderer.rootBone = mappedRoot;
			else
				targetRenderer.rootBone = defaultRootBone;

			targetRenderer.localBounds = source.localBounds;

			// Blend shapes
			for (int i = 0; i < source.sharedMesh.blendShapeCount; i++) targetRenderer.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));

			if (unmappedCount > 0)
				Debug.LogWarning($"'{source.name}': {sourceBones.Length - unmappedCount}/{sourceBones.Length} bones mapped");
		}

		private void CopyMeshRenderer(MeshRenderer source, MeshFilter sourceMeshFilter, ExportSkeletonMapping skeleton)
		{
			var meshGO = new GameObject($"Rigid_{source.name}");
			meshGO.transform.SetParent(skeleton.Root.transform);
			meshGO.transform.localPosition = source.transform.localPosition;
			meshGO.transform.localRotation = source.transform.localRotation;
			meshGO.transform.localScale = new Vector3(
				source.transform.localScale.x * skeleton.MeshScale.x, 
				source.transform.localScale.y * skeleton.MeshScale.y, 
				source.transform.localScale.z * skeleton.MeshScale.z);

			var targetMeshFilter = meshGO.AddComponent<MeshFilter>();
			var targetRenderer = meshGO.AddComponent<MeshRenderer>();

			targetMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;
			targetRenderer.sharedMaterials = source.sharedMaterials;
		}

		private void ConvertMeshFilterToSkinnedMeshRenderer(
			MeshRenderer sourceRenderer,
			MeshFilter sourceMeshFilter,
			Transform wearableRoot,
			ExportSkeletonMapping skeleton,
			Transform defaultBone)
		{
			Mesh sourceMesh = sourceMeshFilter.sharedMesh;

			Debug.Log($"=== Converting '{sourceRenderer.name}' ===");
			Debug.Log($"  Vertices: {sourceMesh.vertexCount}");
			Debug.Log($"  Has BoneWeights: {sourceMesh.boneWeights?.Length > 0}");
			Debug.Log($"  Has BindPoses: {sourceMesh.bindposes?.Length > 0}");

			// Find the wearable's skeleton (sibling to the mesh)
			Transform wearableSkeleton = FindWearableSkeleton(sourceRenderer.transform, wearableRoot);

			if (wearableSkeleton != null)
				Debug.Log($"  Found wearable skeleton: {wearableSkeleton.name}");
			else
				Debug.Log($"  No wearable skeleton found!");

			// Create target GameObject
			var meshGO = new GameObject($"Mesh_{sourceRenderer.name}");
			meshGO.transform.SetParent(skeleton.Root.transform);
			meshGO.transform.localPosition = Vector3.zero;
			meshGO.transform.localRotation = Quaternion.identity;
			meshGO.transform.localScale = skeleton.MeshScale;

			var targetRenderer = meshGO.AddComponent<SkinnedMeshRenderer>();
			targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

			// Check if mesh already has bone data
			if (sourceMesh.boneWeights != null && sourceMesh.boneWeights.Length > 0 &&
			    sourceMesh.bindposes != null && sourceMesh.bindposes.Length > 0)
			{
				Debug.Log($"  Mesh has existing bone data - remapping bones");

				// Find bones in wearable skeleton and map to our skeleton
				Transform[] sourceBones = FindWearableBones(wearableSkeleton, sourceMesh.bindposes.Length);
				Transform[] targetBones = RemapBonesToSkeleton(sourceBones, skeleton, defaultBone);

				// Create mesh copy (don't modify original)
				Mesh skinnedMesh = UnityEngine.Object.Instantiate(sourceMesh);
				skinnedMesh.name = sourceMesh.name + "_Export";

				// Recalculate bind poses for target skeleton
				Matrix4x4[] newBindPoses = new Matrix4x4[targetBones.Length];
				for (int i = 0; i < targetBones.Length; i++)
				{
					newBindPoses[i] = targetBones[i].worldToLocalMatrix * skeleton.Root.transform.localToWorldMatrix;
				}

				skinnedMesh.bindposes = newBindPoses;

				targetRenderer.sharedMesh = skinnedMesh;
				targetRenderer.bones = targetBones;
				targetRenderer.rootBone = skeleton.GetByHumanBone(HumanBodyBones.Hips) ?? defaultBone;

				Debug.Log($"  Remapped {targetBones.Length} bones");
			}
			else
			{
				Debug.Log($"  No bone data - creating single-bone binding");

				// Fallback: bind all vertices to nearest bone
				Transform attachmentBone = FindAttachmentBone(sourceRenderer.transform, skeleton) ?? defaultBone;

				Mesh skinnedMesh = UnityEngine.Object.Instantiate(sourceMesh);
				skinnedMesh.name = sourceMesh.name + "_Export";

				// All vertices bound to one bone
				var boneWeights = new BoneWeight[skinnedMesh.vertexCount];
				for (int i = 0; i < boneWeights.Length; i++)
				{
					boneWeights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
				}

				skinnedMesh.boneWeights = boneWeights;
				skinnedMesh.bindposes = new Matrix4x4[] { attachmentBone.worldToLocalMatrix };

				targetRenderer.sharedMesh = skinnedMesh;
				targetRenderer.bones = new Transform[] { attachmentBone };
				targetRenderer.rootBone = attachmentBone;

				Debug.Log($"  Bound to single bone: {attachmentBone.name}");
			}

			targetRenderer.localBounds = targetRenderer.sharedMesh.bounds;

			// Copy blend shapes if any
			if (sourceMesh.blendShapeCount > 0)
			{
				Debug.Log($"  Mesh has {sourceMesh.blendShapeCount} blend shapes");
			}
		}
		
		/// <summary>
		/// Maps source wearable bones to target export skeleton by name
		/// </summary>
		private Transform[] RemapBonesToSkeleton(Transform[] sourceBones, ExportSkeletonMapping skeleton, Transform defaultBone)
		{
			var targetBones = new Transform[sourceBones.Length];
			int mappedCount = 0;
			int unmappedCount = 0;
    
			for (int i = 0; i < sourceBones.Length; i++)
			{
				if (sourceBones[i] != null && skeleton.TryGetByBoneName(sourceBones[i].name, out var targetBone))
				{
					targetBones[i] = targetBone;
					mappedCount++;
				}
				else
				{
					targetBones[i] = defaultBone;
					unmappedCount++;
            
					if (sourceBones[i] != null)
						Debug.LogWarning($"    Unmapped bone: '{sourceBones[i].name}'");
				}
			}
    
			Debug.Log($"  Bone mapping: {mappedCount} mapped, {unmappedCount} unmapped");
    
			return targetBones;
		}
		
		/// <summary>
		/// Finds the skeleton root in a wearable hierarchy (looks for Armature, Skeleton, Hips, etc.)
		/// </summary>
		private Transform FindWearableSkeleton(Transform meshTransform, Transform wearableRoot)
		{
			if (wearableRoot == null) return null;
    
			// Common skeleton root names
			string[] skeletonNames = { "Armature", "Skeleton", "Root", "Hips" };
    
			foreach (Transform child in wearableRoot)
			{
				// Skip the mesh itself
				if (child == meshTransform || child == meshTransform.parent) continue;
        
				foreach (string name in skeletonNames)
				{
					if (child.name.Contains(name, StringComparison.OrdinalIgnoreCase))
						return child;
				}
        
				// Also check if it has Hips as a child (skeleton structure)
				Transform hips = child.Find("Hips");
				if (hips != null)
					return child;
			}
    
			// Try to find Hips directly under wearable root
			foreach (Transform child in wearableRoot)
			{
				if (child.name == "Hips")
					return child;
			}
    
			return null;
		}

		/// <summary>
		/// Collects all bones from wearable skeleton by traversing hierarchy
		/// </summary>
		private Transform[] FindWearableBones(Transform skeletonRoot, int expectedCount)
		{
			if (skeletonRoot == null)
				return Array.Empty<Transform>();
    
			var allBones = skeletonRoot.GetComponentsInChildren<Transform>();
    
			Debug.Log($"  Wearable skeleton has {allBones.Length} transforms, mesh expects {expectedCount} bones");
    
			// Return all transforms (the mesh's bone indices should correspond to hierarchy order)
			return allBones;
		}


		private Transform FindAttachmentBone(Transform source, ExportSkeletonMapping skeleton)
		{
			var current = source.parent;

			while (current != null)
			{
				if (skeleton.TryGetByBoneName(current.name, out var targetBone))
					return targetBone;

				current = current.parent;
			}

			return null;
		}

		public Avatar CreateAvatarFromSkeleton(ExportSkeletonMapping skeleton)
		{
			var animator = skeleton.Root.GetComponent<Animator>();
			if (animator == null)
			{
				Debug.LogError("No Animator found on skeleton root!");
				return null;
			}

			var humanBones = skeleton.ToHumanBoneDictionary();

			// Validate minimum required bones
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
				Debug.LogError($"Missing required bones: {string.Join(", ", missingBones)}");
				return null;
			}

			EnforceTPose(humanBones);
			Physics.SyncTransforms();

			var avatarDescription = AvatarDescription.Create();
			avatarDescription.SetHumanBones(humanBones);

			Avatar avatar = avatarDescription.CreateAvatar(skeleton.Root.transform);

			if (!avatar.isValid)
			{
				Debug.LogError("Created avatar is invalid!");
				return null;
			}

			avatar.name = "DCL_Export_Avatar";
			animator.avatar = avatar;

			Debug.Log($"Successfully created avatar with {humanBones.Count} bones");

			return avatar;
		}

		// TODO: move this to utils
		// private GameObject DuplicateSkeletonFromAvatarBase(AvatarBase avatarBase, out Dictionary<HumanBodyBones, Transform> humanBones)
		// {
		//     // Create root
		//     GameObject duplicateRoot = new GameObject("DCL_Avatar");
		//     Transform armature = avatarBase.Armature;
		//     duplicateRoot.transform.position = armature.position;
		//     duplicateRoot.transform.localScale = armature.localScale;
		//
		//     duplicateRoot.AddComponent<Animator>();
		//     var boneRenderer = duplicateRoot.AddComponent<BoneRenderer>();
		//
		//     humanBones = new Dictionary<HumanBodyBones, Transform>();
		//
		//     // Define hierarchy structure: (bone, parent, source transform)
		//     // Ordered so parents are always created before children
		//     var boneHierarchy = new (HumanBodyBones bone, HumanBodyBones? parent, Transform source)[]
		//     {
		//         // ============== CORE CHAIN ==============
		//         (HumanBodyBones.Hips, null, avatarBase.HipAnchorPoint),
		//         (HumanBodyBones.Spine, HumanBodyBones.Hips, avatarBase.SpineAnchorPoint),
		//         (HumanBodyBones.Chest, HumanBodyBones.Spine, avatarBase.Spine1AnchorPoint),
		//         (HumanBodyBones.UpperChest, HumanBodyBones.Chest, avatarBase.Spine2AnchorPoint),
		//         (HumanBodyBones.Neck, HumanBodyBones.UpperChest, avatarBase.NeckAnchorPoint),
		//         (HumanBodyBones.Head, HumanBodyBones.Neck, avatarBase.HeadAnchorPoint),
		//
		//         // Head (optional)
		//         // (HumanBodyBones.Jaw, HumanBodyBones.Head, avatarBase.JawAnchorPoint),
		//         // (HumanBodyBones.LeftEye, HumanBodyBones.Head, avatarBase.LeftEyeAnchorPoint),
		//         // (HumanBodyBones.RightEye, HumanBodyBones.Head, avatarBase.RightEyeAnchorPoint),
		//
		//         // Left Arm
		//         (HumanBodyBones.LeftShoulder, HumanBodyBones.UpperChest, avatarBase.LeftShoulderAnchorPoint),
		//         (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftShoulder, avatarBase.LeftArmAnchorPoint),
		//         (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftUpperArm, avatarBase.LeftForearmAnchorPoint),
		//         (HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm, avatarBase.LeftHandAnchorPoint),
		//
		//         // Left Thumb (optional)
		//         (HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.ThumbProximalAnchorPoint),
		//         (HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbProximal, avatarBase.LeftHandFingers.ThumbIntermediateAnchorPoint),
		//         (HumanBodyBones.LeftThumbDistal, HumanBodyBones.LeftThumbIntermediate, avatarBase.LeftHandFingers.ThumbDistalAnchorPoint),
		//
		//         // Left Index (optional)
		//         (HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.IndexProximalAnchorPoint),
		//         (HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexProximal, avatarBase.LeftHandFingers.IndexIntermediateAnchorPoint),
		//         (HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftIndexIntermediate, avatarBase.LeftHandFingers.IndexDistalAnchorPoint),
		//
		//         // Left Middle (optional)
		//         (HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.MiddleProximalAnchorPoint),
		//         (HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleProximal, avatarBase.LeftHandFingers.MiddleIntermediateAnchorPoint),
		//         (HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftMiddleIntermediate, avatarBase.LeftHandFingers.MiddleDistalAnchorPoint),
		//
		//         // Left Ring (optional)
		//         (HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.RingProximalAnchorPoint),
		//         (HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingProximal, avatarBase.LeftHandFingers.RingIntermediateAnchorPoint),
		//         (HumanBodyBones.LeftRingDistal, HumanBodyBones.LeftRingIntermediate, avatarBase.LeftHandFingers.RingDistalAnchorPoint),
		//
		//         // Left Little/Pinky (optional)
		//         (HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.LittleProximalAnchorPoint),
		//         (HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleProximal, avatarBase.LeftHandFingers.LittleIntermediateAnchorPoint),
		//         (HumanBodyBones.LeftLittleDistal, HumanBodyBones.LeftLittleIntermediate, avatarBase.LeftHandFingers.LittleDistalAnchorPoint),
		//
		//         // Right Arm
		//         (HumanBodyBones.RightShoulder, HumanBodyBones.UpperChest, avatarBase.RightShoulderAnchorPoint),
		//         (HumanBodyBones.RightUpperArm, HumanBodyBones.RightShoulder, avatarBase.RightArmAnchorPoint),
		//         (HumanBodyBones.RightLowerArm, HumanBodyBones.RightUpperArm, avatarBase.RightForearmAnchorPoint),
		//         (HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, avatarBase.RightHandAnchorPoint),
		//
		//         // Right Thumb (optional)
		//         (HumanBodyBones.RightThumbProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.ThumbProximalAnchorPoint),
		//         (HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbProximal, avatarBase.RightHandFingers.ThumbIntermediateAnchorPoint),
		//         (HumanBodyBones.RightThumbDistal, HumanBodyBones.RightThumbIntermediate, avatarBase.RightHandFingers.ThumbDistalAnchorPoint),
		//
		//         // Right Index (optional)
		//         (HumanBodyBones.RightIndexProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.IndexProximalAnchorPoint),
		//         (HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexProximal, avatarBase.RightHandFingers.IndexIntermediateAnchorPoint),
		//         (HumanBodyBones.RightIndexDistal, HumanBodyBones.RightIndexIntermediate, avatarBase.RightHandFingers.IndexDistalAnchorPoint),
		//
		//         // Right Middle (optional)
		//         (HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.MiddleProximalAnchorPoint),
		//         (HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleProximal, avatarBase.RightHandFingers.MiddleIntermediateAnchorPoint),
		//         (HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightMiddleIntermediate, avatarBase.RightHandFingers.MiddleDistalAnchorPoint),
		//
		//         // Right Ring (optional)
		//         (HumanBodyBones.RightRingProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.RingProximalAnchorPoint),
		//         (HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingProximal, avatarBase.RightHandFingers.RingIntermediateAnchorPoint),
		//         (HumanBodyBones.RightRingDistal, HumanBodyBones.RightRingIntermediate, avatarBase.RightHandFingers.RingDistalAnchorPoint),
		//
		//         // Right Little/Pinky (optional)
		//         (HumanBodyBones.RightLittleProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.LittleProximalAnchorPoint),
		//         (HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleProximal, avatarBase.RightHandFingers.LittleIntermediateAnchorPoint),
		//         (HumanBodyBones.RightLittleDistal, HumanBodyBones.RightLittleIntermediate, avatarBase.RightHandFingers.LittleDistalAnchorPoint),
		//
		//         // Left Leg
		//         (HumanBodyBones.LeftUpperLeg, HumanBodyBones.Hips, avatarBase.LeftUpLegAnchorPoint),
		//         (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftUpperLeg, avatarBase.LeftLegAnchorPoint),
		//         (HumanBodyBones.LeftFoot, HumanBodyBones.LeftLowerLeg, avatarBase.LeftFootAnchorPoint),
		//         (HumanBodyBones.LeftToes, HumanBodyBones.LeftFoot, avatarBase.LeftToeBaseAnchorPoint),
		//
		//         // Right Leg
		//         (HumanBodyBones.RightUpperLeg, HumanBodyBones.Hips, avatarBase.RightUpLegAnchorPoint),
		//         (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightUpperLeg, avatarBase.RightLegAnchorPoint),
		//         (HumanBodyBones.RightFoot, HumanBodyBones.RightLowerLeg, avatarBase.RightFootAnchorPoint),
		//         (HumanBodyBones.RightToes, HumanBodyBones.RightFoot, avatarBase.RightToeBaseAnchorPoint),
		//     };
		//
		//     foreach (var (bone, parent, source) in boneHierarchy)
		//     {
		//         // Skip optional bones that aren't assigned
		//         if (source == null)
		//             continue;
		//
		//         // Find parent - if parent bone doesn't exist, skip this bone (broken chain)
		//         Transform newParent;
		//         if (parent.HasValue)
		//         {
		//             if (!humanBones.TryGetValue(parent.Value, out newParent))
		//                 continue;
		//         }
		//         else
		//         {
		//             newParent = duplicateRoot.transform;
		//         }
		//
		//         GameObject newBoneGO = new GameObject(bone.ToString());
		//         Transform newBone = newBoneGO.transform;
		//         newBone.SetParent(newParent);
		//
		//         newBone.SetPositionAndRotation(source.position, source.rotation);
		//         newBone.localScale = source.localScale;
		//
		//         humanBones[bone] = newBone;
		//     }
		//
		//     // TODO: Debug value, remove it
		//     duplicateRoot.transform.position += new Vector3(2f, 0f, 0f);
		//
		//     boneRenderer.transforms = humanBones.Values.ToArray();
		//
		//     Debug.Log($"Created skeleton with {humanBones.Count} humanoid bones");
		//
		//     return duplicateRoot;
		// }
		//
		// // TODO: move this to utils
		// public Avatar CreateAvatarFromSkeleton(GameObject skeletonRoot, Dictionary<HumanBodyBones, Transform> humanBones)
		// {
		//     var animator = skeletonRoot.GetComponent<Animator>();
		//     if (animator == null)
		//     {
		//         Debug.LogError("No Animator found on skeleton root!");
		//         return null;
		//     }
		//
		//     // Validate minimum required bones
		//     var requiredBones = new[]
		//     {
		//         HumanBodyBones.Hips,
		//         HumanBodyBones.Spine,
		//         HumanBodyBones.Chest,
		//         HumanBodyBones.Neck,
		//         HumanBodyBones.Head,
		//         HumanBodyBones.LeftUpperArm,
		//         HumanBodyBones.LeftLowerArm,
		//         HumanBodyBones.LeftHand,
		//         HumanBodyBones.RightUpperArm,
		//         HumanBodyBones.RightLowerArm,
		//         HumanBodyBones.RightHand,
		//         HumanBodyBones.LeftUpperLeg,
		//         HumanBodyBones.LeftLowerLeg,
		//         HumanBodyBones.LeftFoot,
		//         HumanBodyBones.RightUpperLeg,
		//         HumanBodyBones.RightLowerLeg,
		//         HumanBodyBones.RightFoot,
		//     };
		//
		//     var missingBones = requiredBones.Where(b => !humanBones.ContainsKey(b)).ToList();
		//     if (missingBones.Count > 0)
		//     {
		//         Debug.LogError($"Missing required bones: {string.Join(", ", missingBones)}");
		//         return null;
		//     }
		//
		//     // Enforce T-pose before creating avatar
		//     EnforceTPose(humanBones);
		//
		//     // Force Unity to process transform changes
		//     Physics.SyncTransforms();
		//
		//     // Debug bone orientations before avatar creation
		//     //DebugBoneOrientations(humanBones);
		//
		//     // Create avatar description
		//     var avatarDescription = AvatarDescription.Create();
		//     avatarDescription.SetHumanBones(humanBones);
		//
		//     // Create avatar from the skeleton root (not hips)
		//     Avatar avatar = avatarDescription.CreateAvatar(skeletonRoot.transform);
		//
		//     if (!avatar.isValid)
		//     {
		//         Debug.LogError("Created avatar is invalid!");
		//         DebugAvatarCreationFailure(humanBones);
		//         return null;
		//     }
		//
		//     avatar.name = "DCL_Export_Avatar";
		//     animator.avatar = avatar;
		//
		//     Debug.Log($"Successfully created avatar with {humanBones.Count} bones");
		//
		//     return avatar;
		// }

		private void EnforceTPose(Dictionary<HumanBodyBones, Transform> bones)
		{
			// Reset all bones to identity rotation first
			foreach (var bone in bones.Values) bone.localRotation = Quaternion.identity;

			// Apply DCL-specific T-pose corrections
			// These rotations compensate for DCL's skeleton rest pose

			if (bones.TryGetValue(HumanBodyBones.Hips, out var hips))
				hips.localRotation = Quaternion.Euler(0, 0, 180);

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
		}

		// private void DebugAvatarCreationFailure(Dictionary<HumanBodyBones, Transform> bones)
		// {
		//     var sb = new StringBuilder();
		//     sb.AppendLine("=== AVATAR CREATION FAILURE DEBUG ===");
		//
		//     foreach (var kvp in bones)
		//     {
		//         var bone = kvp.Value;
		//         sb.AppendLine($"{kvp.Key}:");
		//         sb.AppendLine($"Position: {bone.position}");
		//         sb.AppendLine($"Local Position: {bone.localPosition}");
		//         sb.AppendLine($"Rotation: {bone.rotation.eulerAngles}");
		//         sb.AppendLine($"Local Rotation: {bone.localRotation.eulerAngles}");
		//         sb.AppendLine($"Parent: {(bone.parent != null ? bone.parent.name : "NULL")}");
		//     }
		//
		//     Debug.LogError(sb.ToString());
		// }

		// private void CopyMeshesToSkeleton(
		//     AvatarBase sourceAvatar,
		//     GameObject targetSkeletonRoot,
		//     Dictionary<HumanBodyBones, Transform> targetHumanBones)
		// {
		//     // Build mapping from source transforms to target transforms
		//     var sourceToTargetBoneMap = BuildBoneMapping(sourceAvatar, targetHumanBones);
		//
		//     // Copy SkinnedMeshRenderers
		//     CopySkinnedMeshRenderers(sourceAvatar, targetSkeletonRoot, sourceToTargetBoneMap, targetHumanBones);
		//
		//     // Copy MeshRenderers (rigid attachments like glasses, hats, etc.)
		//     CopyMeshRenderers(sourceAvatar, sourceToTargetBoneMap);
		// }
		//
		// private Dictionary<Transform, Transform> BuildBoneMapping(
		//     AvatarBase sourceAvatar,
		//     Dictionary<HumanBodyBones, Transform> targetHumanBones)
		// {
		//     var mapping = new Dictionary<Transform, Transform>();
		//
		//     // Map source anchor points to target bones
		//     var sourceToHumanBone = new Dictionary<Transform, HumanBodyBones>
		//     {
		//         // Core
		//         { sourceAvatar.HipAnchorPoint, HumanBodyBones.Hips },
		//         { sourceAvatar.SpineAnchorPoint, HumanBodyBones.Spine },
		//         { sourceAvatar.Spine1AnchorPoint, HumanBodyBones.Chest },
		//         { sourceAvatar.Spine2AnchorPoint, HumanBodyBones.UpperChest },
		//         { sourceAvatar.NeckAnchorPoint, HumanBodyBones.Neck },
		//         { sourceAvatar.HeadAnchorPoint, HumanBodyBones.Head },
		//
		//         // Left Arm
		//         { sourceAvatar.LeftShoulderAnchorPoint, HumanBodyBones.LeftShoulder },
		//         { sourceAvatar.LeftArmAnchorPoint, HumanBodyBones.LeftUpperArm },
		//         { sourceAvatar.LeftForearmAnchorPoint, HumanBodyBones.LeftLowerArm },
		//         { sourceAvatar.LeftHandAnchorPoint, HumanBodyBones.LeftHand },
		//
		//         // Right Arm
		//         { sourceAvatar.RightShoulderAnchorPoint, HumanBodyBones.RightShoulder },
		//         { sourceAvatar.RightArmAnchorPoint, HumanBodyBones.RightUpperArm },
		//         { sourceAvatar.RightForearmAnchorPoint, HumanBodyBones.RightLowerArm },
		//         { sourceAvatar.RightHandAnchorPoint, HumanBodyBones.RightHand },
		//
		//         // Left Leg
		//         { sourceAvatar.LeftUpLegAnchorPoint, HumanBodyBones.LeftUpperLeg },
		//         { sourceAvatar.LeftLegAnchorPoint, HumanBodyBones.LeftLowerLeg },
		//         { sourceAvatar.LeftFootAnchorPoint, HumanBodyBones.LeftFoot },
		//         { sourceAvatar.LeftToeBaseAnchorPoint, HumanBodyBones.LeftToes },
		//
		//         // Right Leg
		//         { sourceAvatar.RightUpLegAnchorPoint, HumanBodyBones.RightUpperLeg },
		//         { sourceAvatar.RightLegAnchorPoint, HumanBodyBones.RightLowerLeg },
		//         { sourceAvatar.RightFootAnchorPoint, HumanBodyBones.RightFoot },
		//         { sourceAvatar.RightToeBaseAnchorPoint, HumanBodyBones.RightToes },
		//     };
		//
		//         AddFingerMapping(sourceToHumanBone, sourceAvatar.LeftHandFingers, true);
		//     AddFingerMapping(sourceToHumanBone, sourceAvatar.RightHandFingers, false);
		//     
		//
		//     // Build the final source -> target mapping
		//     foreach (var kvp in sourceToHumanBone)
		//     {
		//         if (kvp.Key != null && targetHumanBones.TryGetValue(kvp.Value, out var targetBone))
		//         {
		//             mapping[kvp.Key] = targetBone;
		//         }
		//     }
		//
		//     Debug.Log($"Built bone mapping with {mapping.Count} bones");
		//
		//     return mapping;
		//
		//     void AddFingerMapping(Dictionary<Transform, HumanBodyBones> mapping, AvatarBase.HandFingerBones fingers, bool isLeft)
		//     {
		//         if (isLeft)
		//         {
		//             if (fingers.ThumbProximalAnchorPoint != null) mapping[fingers.ThumbProximalAnchorPoint] = HumanBodyBones.LeftThumbProximal;
		//             if (fingers.ThumbIntermediateAnchorPoint != null) mapping[fingers.ThumbIntermediateAnchorPoint] = HumanBodyBones.LeftThumbIntermediate;
		//             if (fingers.ThumbDistalAnchorPoint != null) mapping[fingers.ThumbDistalAnchorPoint] = HumanBodyBones.LeftThumbDistal;
		//
		//             if (fingers.IndexProximalAnchorPoint != null) mapping[fingers.IndexProximalAnchorPoint] = HumanBodyBones.LeftIndexProximal;
		//             if (fingers.IndexIntermediateAnchorPoint != null) mapping[fingers.IndexIntermediateAnchorPoint] = HumanBodyBones.LeftIndexIntermediate;
		//             if (fingers.IndexDistalAnchorPoint != null) mapping[fingers.IndexDistalAnchorPoint] = HumanBodyBones.LeftIndexDistal;
		//
		//             if (fingers.MiddleProximalAnchorPoint != null) mapping[fingers.MiddleProximalAnchorPoint] = HumanBodyBones.LeftMiddleProximal;
		//             if (fingers.MiddleIntermediateAnchorPoint != null) mapping[fingers.MiddleIntermediateAnchorPoint] = HumanBodyBones.LeftMiddleIntermediate;
		//             if (fingers.MiddleDistalAnchorPoint != null) mapping[fingers.MiddleDistalAnchorPoint] = HumanBodyBones.LeftMiddleDistal;
		//
		//             if (fingers.RingProximalAnchorPoint != null) mapping[fingers.RingProximalAnchorPoint] = HumanBodyBones.LeftRingProximal;
		//             if (fingers.RingIntermediateAnchorPoint != null) mapping[fingers.RingIntermediateAnchorPoint] = HumanBodyBones.LeftRingIntermediate;
		//             if (fingers.RingDistalAnchorPoint != null) mapping[fingers.RingDistalAnchorPoint] = HumanBodyBones.LeftRingDistal;
		//
		//             if (fingers.LittleProximalAnchorPoint != null) mapping[fingers.LittleProximalAnchorPoint] = HumanBodyBones.LeftLittleProximal;
		//             if (fingers.LittleIntermediateAnchorPoint != null) mapping[fingers.LittleIntermediateAnchorPoint] = HumanBodyBones.LeftLittleIntermediate;
		//             if (fingers.LittleDistalAnchorPoint != null) mapping[fingers.LittleDistalAnchorPoint] = HumanBodyBones.LeftLittleDistal;
		//         }
		//         else
		//         {
		//             if (fingers.ThumbProximalAnchorPoint != null) mapping[fingers.ThumbProximalAnchorPoint] = HumanBodyBones.RightThumbProximal;
		//             if (fingers.ThumbIntermediateAnchorPoint != null) mapping[fingers.ThumbIntermediateAnchorPoint] = HumanBodyBones.RightThumbIntermediate;
		//             if (fingers.ThumbDistalAnchorPoint != null) mapping[fingers.ThumbDistalAnchorPoint] = HumanBodyBones.RightThumbDistal;
		//
		//             if (fingers.IndexProximalAnchorPoint != null) mapping[fingers.IndexProximalAnchorPoint] = HumanBodyBones.RightIndexProximal;
		//             if (fingers.IndexIntermediateAnchorPoint != null) mapping[fingers.IndexIntermediateAnchorPoint] = HumanBodyBones.RightIndexIntermediate;
		//             if (fingers.IndexDistalAnchorPoint != null) mapping[fingers.IndexDistalAnchorPoint] = HumanBodyBones.RightIndexDistal;
		//
		//             if (fingers.MiddleProximalAnchorPoint != null) mapping[fingers.MiddleProximalAnchorPoint] = HumanBodyBones.RightMiddleProximal;
		//             if (fingers.MiddleIntermediateAnchorPoint != null) mapping[fingers.MiddleIntermediateAnchorPoint] = HumanBodyBones.RightMiddleIntermediate;
		//             if (fingers.MiddleDistalAnchorPoint != null) mapping[fingers.MiddleDistalAnchorPoint] = HumanBodyBones.RightMiddleDistal;
		//
		//             if (fingers.RingProximalAnchorPoint != null) mapping[fingers.RingProximalAnchorPoint] = HumanBodyBones.RightRingProximal;
		//             if (fingers.RingIntermediateAnchorPoint != null) mapping[fingers.RingIntermediateAnchorPoint] = HumanBodyBones.RightRingIntermediate;
		//             if (fingers.RingDistalAnchorPoint != null) mapping[fingers.RingDistalAnchorPoint] = HumanBodyBones.RightRingDistal;
		//
		//             if (fingers.LittleProximalAnchorPoint != null) mapping[fingers.LittleProximalAnchorPoint] = HumanBodyBones.RightLittleProximal;
		//             if (fingers.LittleIntermediateAnchorPoint != null) mapping[fingers.LittleIntermediateAnchorPoint] = HumanBodyBones.RightLittleIntermediate;
		//             if (fingers.LittleDistalAnchorPoint != null) mapping[fingers.LittleDistalAnchorPoint] = HumanBodyBones.RightLittleDistal;
		//         }
		//     }
		// }
		//
		// private void CopySkinnedMeshRenderers(
		//     AvatarBase sourceAvatar,
		//     GameObject targetRoot,
		//     Dictionary<Transform, Transform> boneMapping,
		//     Dictionary<HumanBodyBones, Transform> targetHumanBones)
		// {
		//     var sourceRenderers = sourceAvatar.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
		//
		//     int copiedCount = 0;
		//     int skippedCount = 0;
		//
		//     foreach (var sourceRenderer in sourceRenderers)
		//     {
		//         // Skip disabled renderers
		//         if (!sourceRenderer.enabled || !sourceRenderer.gameObject.activeInHierarchy)
		//         {
		//             skippedCount++;
		//             continue;
		//         }
		//
		//         // Skip renderers without mesh
		//         if (sourceRenderer.sharedMesh == null)
		//         {
		//             Debug.LogWarning($"SkinnedMeshRenderer '{sourceRenderer.name}' has no mesh, skipping");
		//             skippedCount++;
		//             continue;
		//         }
		//
		//         // Create new GameObject for the mesh
		//         var meshGO = new GameObject($"Mesh_{sourceRenderer.name}");
		//         meshGO.transform.SetParent(targetRoot.transform);
		//         meshGO.transform.localPosition = Vector3.zero;
		//         meshGO.transform.localRotation = Quaternion.identity;
		//         meshGO.transform.localScale = Vector3.one;
		//
		//         var targetRenderer = meshGO.AddComponent<SkinnedMeshRenderer>();
		//         targetRenderer.sharedMesh = sourceRenderer.sharedMesh;
		//         targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
		//
		//         // Remap bones
		//         var sourceBones = sourceRenderer.bones;
		//         var targetBones = new Transform[sourceBones.Length];
		//         int unmappedBones = 0;
		//
		//         for (int i = 0; i < sourceBones.Length; i++)
		//         {
		//             if (sourceBones[i] != null && boneMapping.TryGetValue(sourceBones[i], out var targetBone))
		//             {
		//                 targetBones[i] = targetBone;
		//             }
		//             else
		//             {
		//                 // Bone not in our mapping - try to find closest parent that is mapped
		//                 targetBones[i] = FindClosestMappedParent(sourceBones[i], boneMapping);
		//                 unmappedBones++;
		//             }
		//         }
		//
		//         targetRenderer.bones = targetBones;
		//
		//         // Remap root bone
		//         if (sourceRenderer.rootBone != null && boneMapping.TryGetValue(sourceRenderer.rootBone, out var targetRootBone))
		//         {
		//             targetRenderer.rootBone = targetRootBone;
		//         }
		//         else if (targetHumanBones.TryGetValue(HumanBodyBones.Hips, out var hips))
		//         {
		//             targetRenderer.rootBone = hips;
		//         }
		//
		//         // Copy bounds
		//         targetRenderer.localBounds = sourceRenderer.localBounds;
		//
		//         // Copy blend shape weights if any
		//         if (sourceRenderer.sharedMesh.blendShapeCount > 0)
		//         {
		//             for (int i = 0; i < sourceRenderer.sharedMesh.blendShapeCount; i++)
		//             {
		//                 targetRenderer.SetBlendShapeWeight(i, sourceRenderer.GetBlendShapeWeight(i));
		//             }
		//         }
		//
		//         copiedCount++;
		//
		//         if (unmappedBones > 0)
		//         {
		//             Debug.LogWarning($"Mesh '{sourceRenderer.name}' has {unmappedBones}/{sourceBones.Length} unmapped bones");
		//         }
		//     }
		//
		//     Debug.Log($"Copied {copiedCount} SkinnedMeshRenderers, skipped {skippedCount}");
		// }
		//
		// private void CopyMeshRenderers(
		//     AvatarBase sourceAvatar,
		//     Dictionary<Transform, Transform> boneMapping)
		// {
		//     var sourceRenderers = sourceAvatar.gameObject.GetComponentsInChildren<MeshRenderer>(true);
		//
		//     int copiedCount = 0;
		//     int skippedCount = 0;
		//
		//     foreach (var sourceRenderer in sourceRenderers)
		//     {
		//         // Skip disabled renderers
		//         if (!sourceRenderer.enabled || !sourceRenderer.gameObject.activeInHierarchy)
		//         {
		//             skippedCount++;
		//             continue;
		//         }
		//
		//         // Get MeshFilter
		//         var sourceMeshFilter = sourceRenderer.GetComponent<MeshFilter>();
		//         if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
		//         {
		//             Debug.LogWarning($"MeshRenderer '{sourceRenderer.name}' has no MeshFilter or mesh, skipping");
		//             skippedCount++;
		//             continue;
		//         }
		//
		//         // Find the bone this mesh should be attached to
		//         Transform targetParent = FindClosestMappedParent(sourceRenderer.transform, boneMapping);
		//
		//         if (targetParent == null)
		//         {
		//             Debug.LogWarning($"MeshRenderer '{sourceRenderer.name}' has no valid parent bone, skipping");
		//             skippedCount++;
		//             continue;
		//         }
		//
		//         // Create new GameObject for the mesh
		//         var meshGO = new GameObject($"Rigid_{sourceRenderer.name}");
		//         meshGO.transform.SetParent(targetParent);
		//
		//         // Preserve local transform relative to parent bone
		//         meshGO.transform.localPosition = sourceRenderer.transform.localPosition;
		//         meshGO.transform.localRotation = sourceRenderer.transform.localRotation;
		//         meshGO.transform.localScale = sourceRenderer.transform.localScale;
		//
		//         // Add components
		//         var targetMeshFilter = meshGO.AddComponent<MeshFilter>();
		//         var targetRenderer = meshGO.AddComponent<MeshRenderer>();
		//
		//         // Copy mesh and materials
		//         targetMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;
		//         targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
		//
		//         copiedCount++;
		//     }
		//
		//     Debug.Log($"Copied {copiedCount} MeshRenderers (rigid), skipped {skippedCount}");
		// }
		//
		// private Transform FindClosestMappedParent(Transform source, Dictionary<Transform, Transform> boneMapping)
		// {
		//     if (source == null) return null;
		//
		//     Transform current = source;
		//
		//     while (current != null)
		//     {
		//         if (boneMapping.TryGetValue(current, out var mapped))
		//         {
		//             return mapped;
		//         }
		//
		//         current = current.parent;
		//     }
		//
		//     return null;
		// }

		private void DebugAvatar(Entity entity, ref AvatarShapeComponent avatarShape, AvatarBase avatarBase)
		{
			var sb = new StringBuilder();

			sb.AppendLine("╔═════════════════════════════════════════════════════════╗");
			sb.AppendLine("║               AVATAR EXPORT DEBUG REPORT                      ║");
			sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
			sb.AppendLine();

			// Basic Info
			sb.AppendLine("┌─ BASIC INFO ─────────────────────────────────────────────────┐");
			sb.AppendLine($"│ Entity:      {entity}");
			sb.AppendLine($"│ Name:        {avatarShape.Name}");
			sb.AppendLine($"│ Body Shape:  {avatarShape.BodyShape.Value}");
			sb.AppendLine($"│ Is Preview:  {avatarShape.IsPreview}");
			sb.AppendLine($"│ Skin Color:  {avatarShape.SkinColor}");
			sb.AppendLine($"│ Hair Color:  {avatarShape.HairColor}");
			sb.AppendLine($"│ Eyes Color:  {avatarShape.EyesColor}");
			sb.AppendLine("└──────────────────────────────────────────────────────────────┘");
			sb.AppendLine();

			// Main Renderer
			DebugMainRenderer(avatarBase, sb);

			// All Renderers
			DebugAllRenderers(avatarBase, sb);

			// Skeleton
			DebugSkeleton(avatarBase, sb);

			// Wearables (if storage available)
			DebugWearables(ref avatarShape, sb);

			ReportHub.Log(GetReportCategory(), sb.ToString());
		}

		private void DebugMainRenderer(AvatarBase avatarBase, StringBuilder sb)
		{
			sb.AppendLine("┌─ MAIN SKINNED MESH RENDERER ─────────────────────────────────┐");

			var mainRenderer = avatarBase.AvatarSkinnedMeshRenderer;

			if (mainRenderer == null || mainRenderer.sharedMesh == null)
			{
				sb.AppendLine("│ ⚠ No main renderer or mesh found!");
				sb.AppendLine("└──────────────────────────────────────────────────────────────┘");
				sb.AppendLine();
				return;
			}

			bool isActive = mainRenderer.gameObject.activeInHierarchy;
			string color = isActive ? "green" : "red";

			var mesh = mainRenderer.sharedMesh;
			sb.AppendLine($"│ Mesh Name:      {mesh.name}");
			sb.AppendLine($"│ Vertices:       <color={color}>{mesh.vertexCount}</color>");
			sb.AppendLine($"│ Triangles:      <color={color}>{mesh.triangles.Length / 3}</color>");
			sb.AppendLine($"│ SubMeshes:      <color={color}>{mesh.subMeshCount}</color>");
			sb.AppendLine($"│ Bones:          <color={color}>{mainRenderer.bones.Length}</color>");
			sb.AppendLine($"│ Materials:      <color={color}>{mainRenderer.sharedMaterials.Length}</color>");
			sb.AppendLine($"│ Root Bone:      {(mainRenderer.rootBone ? mainRenderer.rootBone.name : "NULL")}");

			sb.AppendLine("│");
			sb.AppendLine("│ Materials:");
			for (int i = 0; i < mainRenderer.sharedMaterials.Length; i++)
			{
				var mat = mainRenderer.sharedMaterials[i];
				if (mat != null)
					sb.AppendLine($"│   <color={{color}}>[{{i}}]</color> {mat.name} (Shader: {mat.shader.name})");
			}

			sb.AppendLine("└──────────────────────────────────────────────────────────────┘");
			sb.AppendLine();
		}

		private void DebugAllRenderers(AvatarBase avatarBase, StringBuilder sb)
		{
			sb.AppendLine("┌─ ALL SKINNED MESH RENDERERS ─────────────────────────────────┐");

			var allRenderers = avatarBase.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			sb.AppendLine($"│ Total Count: {allRenderers.Length}");
			sb.AppendLine("│");

			for (int i = 0; i < allRenderers.Length; i++)
			{
				var renderer = allRenderers[i];
				bool isActive = renderer.gameObject.activeInHierarchy;
				string color = isActive ? "green" : "red";

				sb.AppendLine($"│ <color={color}>[{i}]</color> {renderer.gameObject.name}");
				sb.AppendLine($"│     Path: {GetGameObjectPath(renderer.gameObject)}");
				sb.AppendLine($"│     Active: {renderer.gameObject.activeInHierarchy}, Enabled: {renderer.enabled}");

				if (renderer.sharedMesh != null)
				{
					var mesh = renderer.sharedMesh;
					sb.AppendLine($"│     Mesh: {mesh.name} (<color={color}>{mesh.vertexCount} verts</color>, <color={color}>{renderer.bones.Length} bones</color>)");
					sb.AppendLine($"│     Root Bone: {(renderer.rootBone ? renderer.rootBone.name : "NULL")}");

					// Sample bones
					if (renderer.bones.Length > 0)
					{
						int boneCount = Mathf.Min(3, renderer.bones.Length);
						sb.Append($"│     Bones: ");
						for (int b = 0; b < boneCount; b++)
							if (renderer.bones[b] != null)
								sb.Append($"{renderer.bones[b].name}, ");
						if (renderer.bones.Length > 3)
							sb.Append($"... +<color={color}>{renderer.bones.Length - 3}</color> more");
						sb.AppendLine();
					}
				}
				else
				{
					sb.AppendLine("│     ⚠ NO MESH!");
				}

				if (i < allRenderers.Length - 1)
					sb.AppendLine("│");
			}

			sb.AppendLine("└──────────────────────────────────────────────────────────────┘");
			sb.AppendLine();
		}

		private void DebugSkeleton(AvatarBase avatarBase, StringBuilder sb)
		{
			sb.AppendLine("┌─ SKELETON STRUCTURE ─────────────────────────────────────────┐");

			if (avatarBase.Armature == null)
			{
				sb.AppendLine("│ ⚠ No Armature found!");
				sb.AppendLine("└──────────────────────────────────────────────────────────────┘");
				sb.AppendLine();
				return;
			}

			sb.AppendLine($"│ Armature Root: {avatarBase.Armature.name}");
			sb.AppendLine($"│ Position:      {avatarBase.Armature.position}");
			sb.AppendLine($"│ Rotation:      {avatarBase.Armature.eulerAngles}");

			var allBones = avatarBase.Armature.GetComponentsInChildren<Transform>();
			sb.AppendLine($"│ Total Bones:   {allBones.Length}");
			sb.AppendLine("│");
			sb.AppendLine("│ Hierarchy (first 20 bones):");

			for (int i = 0; i < Mathf.Min(20, allBones.Length); i++)
			{
				int depth = GetDepth(allBones[i], avatarBase.Armature);
				string indent = new ('─', depth);
				bool isActive = allBones[i].gameObject.activeInHierarchy;
				string color = isActive ? "green" : "red";
				sb.AppendLine($"│ {indent}<color={color}>{allBones[i].name}</color>");
			}

			if (allBones.Length > 20)
				sb.AppendLine($"│ ... and {allBones.Length - 20} more bones");

			sb.AppendLine("│");
			sb.AppendLine("│ Key Bones (VRM mapping check):");
			DebugKeyBone(sb, "Head", avatarBase.HeadAnchorPoint);
			DebugKeyBone(sb, "Neck", avatarBase.NeckAnchorPoint);
			DebugKeyBone(sb, "Spine", avatarBase.SpineAnchorPoint);
			DebugKeyBone(sb, "Hips", avatarBase.HipAnchorPoint);
			DebugKeyBone(sb, "Left Hand", avatarBase.LeftHandAnchorPoint);
			DebugKeyBone(sb, "Right Hand", avatarBase.RightHandAnchorPoint);
			DebugKeyBone(sb, "Left Foot", avatarBase.LeftFootAnchorPoint);
			DebugKeyBone(sb, "Right Foot", avatarBase.RightFootAnchorPoint);

			sb.AppendLine("└──────────────────────────────────────────────────────────────┘");
			sb.AppendLine();
		}

		private void DebugKeyBone(StringBuilder sb, string label, Transform bone)
		{
			if (bone != null)
			{
				bool isActive = bone.gameObject.activeInHierarchy;
				string color = isActive ? "green" : "red";
				sb.AppendLine($"│   ✓ {label,-12}: <color={color}>{bone.name}</color>");
			}
			else
			{
				sb.AppendLine($"│   ✗ {label,-12}: NULL");
			}
		}

		private void DebugWearables(ref AvatarShapeComponent avatarShape, StringBuilder sb)
		{
			if (wearableStorage == null)
			{
				sb.AppendLine("┌─ WEARABLES ──────────────────────────────────────────────────┐");
				sb.AppendLine("│ ⚠ Wearable storage not available");
				sb.AppendLine("└──────────────────────────────────────────────────────────────┘");
				return;
			}

			sb.AppendLine("┌─ LOADED WEARABLES ───────────────────────────────────────────┐");

			var wearablesList = GetLoadedWearables(ref avatarShape);

			if (wearablesList.Count == 0)
				sb.AppendLine("│ No wearables loaded");
			else
				foreach (var wearableInfo in wearablesList)
				{
					sb.AppendLine($"│ • {wearableInfo.InstanceName}");
					sb.AppendLine($"│   MainAssetInfo:  {wearableInfo.MainAssetInfo}");
					sb.AppendLine($"│   Renderers:    {wearableInfo.Renderers}");
					sb.AppendLine($"│   Mesh:         {wearableInfo.MeshesNames}");
					sb.AppendLine("│");
				}

			sb.AppendLine("└──────────────────────────────────────────────────────────────┘");
			sb.AppendLine();
		}

		private List<WearableDebugInfo> GetLoadedWearables(ref AvatarShapeComponent avatarShape)
		{
			var result = new List<WearableDebugInfo>();

			foreach (var wearable in avatarShape.InstantiatedWearables) result.Add(new WearableDebugInfo(wearable));

			return result;
		}

		private static string GetGameObjectPath(GameObject obj)
		{
			string path = obj.name;
			var parent = obj.transform.parent;

			while (parent != null)
			{
				path = parent.name + "/" + path;
				parent = parent.parent;
			}

			return path;
		}

		private static int GetDepth(Transform bone, Transform root)
		{
			int depth = 0;
			var current = bone;

			while (current != null && current != root)
			{
				depth++;
				current = current.parent;
			}

			return depth;
		}

		public struct CachedMeshData
		{
			public Mesh Mesh;
			public Material[] Materials;
			public Transform[] Bones;        // null for rigid meshes
			public Transform RootBone;       // null for rigid meshes
			public Transform Transform;      // Transform of the renderer GameObject
			public Renderer Renderer;        // Reference to original renderer (SkinnedMeshRenderer or MeshRenderer)
			public bool IsSkinnedMesh;
			public string Name;
			public string GameObjectPath;
			public bool IsActive;
			public bool IsEnabled;
		}

		private struct WearableDebugInfo
		{
			public string MainAssetInfo;
			public string Renderers;
			public string InstanceName;
			public Mesh[] Meshes;
			public string MeshesNames;

			public WearableDebugInfo(CachedAttachment mainAssetInfo)
			{
				MainAssetInfo = mainAssetInfo.OriginalAsset.MainAsset.ToString();
				Renderers = mainAssetInfo.Renderers.Count > 0 ? mainAssetInfo.Renderers[0].sharedMaterial.name : "";
				for (int i = 1; i < mainAssetInfo.Renderers.Count; i++)
				{
					var renderer = mainAssetInfo.Renderers[i];
					Renderers += $"| {renderer.sharedMaterial.name}";
				}

				InstanceName = mainAssetInfo.Instance.name;

				var meshFilters = mainAssetInfo.Instance.GetComponentsInChildren<MeshFilter>();
				Meshes = new Mesh [meshFilters.Length];
				for (int i = 0; i < meshFilters.Length; i++) Meshes[i] = meshFilters[i].mesh;

				MeshesNames = "";
				for (int i = 0; i < Meshes.Length; i++)
				{
					MeshesNames += Meshes[i].name;
					if (i < Meshes.Length - 1)
						MeshesNames += " | ";
				}
			}
		}
	}
}