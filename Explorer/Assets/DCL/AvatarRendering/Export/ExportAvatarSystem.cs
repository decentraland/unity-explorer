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
			if (avatarShape.IsDirty || !avatarShape.WearablePromise.IsConsumed)
			{
				ReportHub.LogWarning(GetReportCategory(), $"Avatar {avatarShape.Name} is not ready for export yet. IsDirty={avatarShape.IsDirty}");
				return;
			}

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

		private UniTaskVoid Export(ref AvatarShapeComponent avatarShape, AvatarBase avatarBase)
		{
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

				byte[] vrmBytes = exportService.ExportToVRM(in avatarShape, avatarBase, wearables, exporterReferences);

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

				ReportHub.Log(GetReportCategory(), $"VRM exported successfully to: {savePath}");
			}
			catch (Exception e)
			{
				ReportHub.LogException(e, GetReportCategory());
				throw;
			}
			
			return default;
		}

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