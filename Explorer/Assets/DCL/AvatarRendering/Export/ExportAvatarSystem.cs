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
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using UniHumanoid;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using VRM;
using VRMShaders;

namespace DCL.AvatarRendering.Export
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.DEBUG)]
    public partial class ExportAvatarSystem : BaseUnityLoopSystem
    {
        private readonly IWearableStorage wearableStorage;
        private readonly UniGLTF.GltfExportSettings settings = new ();
        private readonly RuntimeTextureSerializer textureSerializer = new ();
        
        public ExportAvatarSystem(World world, IWearableStorage wearableStorage, VRMBonesMappingSO bonesMapping) : base(world)
        {
            this.wearableStorage = wearableStorage;
        }
        
        protected override void Update(float deltaTime)
        {
            ProcessExportIntentionsQuery(World);
        }

        [Query]
        [All(typeof(ExportAvatarIntention))]
        private void ProcessExportIntentions(in Entity entity, ref ExportAvatarIntention exportIntention, ref AvatarShapeComponent avatarShape)
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
            VRMExporterReferences exporterReferences = new VRMExporterReferences();
            exporterReferences.metaObject = new ()
            {
                Version = "1.0, UniVRM v0.112.0",
                Author = "TODO: Get author name",
                Reference = "TODO: Get asset reference"
            };
            Dictionary<string, Transform> boneMapping;
            var duplicatedAvatar = 
                DuplicateSkeletonObject(avatarBase.Armature, avatarBase.HipAnchorPoint, out boneMapping);

            //return default;
            if (avatarBase.AvatarAnimator.avatar == null)
            {
                avatarBase.AvatarAnimator.runtimeAnimatorController = null;
                //avatarBase.AvatarAnimator.avatar = CreateAvatarFromSkeleton(avatarBase);
            }

            CreateAvatarFromSkeleton(avatarBase);
            return default;
            
            GameObject bonesNormalized = VRMBoneNormalizer.Execute(avatarBase.AvatarAnimator.gameObject, false);
            var vrmNormalized = VRMExporter.Export(settings, bonesNormalized, textureSerializer);
            
            string fileName = $"Avatar_{DateTime.Now:yyyyMMddhhmmss}";
            //string savePath = FileBrowser.Instance.SaveFile("Save avatar VRM", Application.persistentDataPath, fileName, new ExtensionFilter("vrm", "vrm"));
            string savePath = "C://VRM/" + fileName;
            
            if(!string.IsNullOrEmpty(savePath))
            {
                try
                {
                    File.WriteAllBytes(savePath, vrmNormalized.ToGlbBytes());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            
            //var cachedMeshes = new List<CachedMeshData>();
            //
            // // Cache SkinnedMeshRenderers (deformable wearables)
            // SkinnedMeshRenderer[] skinnedRenderers = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
            //
            // ReportHub.Log(GetReportCategory(), $"Found {skinnedRenderers.Length} SkinnedMeshRenderers");
            //
            // foreach (SkinnedMeshRenderer skinnedRenderer in skinnedRenderers)
            // {
            //     if (skinnedRenderer.sharedMesh == null)
            //     {
            //         ReportHub.LogWarning(GetReportCategory(), $"SkinnedMeshRenderer on {skinnedRenderer.gameObject.name} has no mesh");
            //         continue;
            //     }
            //
            //     cachedMeshes.Add(new CachedMeshData
            //     {
            //         Mesh = skinnedRenderer.sharedMesh,
            //         Materials = skinnedRenderer.sharedMaterials,
            //         Bones = skinnedRenderer.bones,
            //         RootBone = skinnedRenderer.rootBone,
            //         Transform = skinnedRenderer.transform,
            //         Renderer = skinnedRenderer,
            //         IsSkinnedMesh = true,
            //         Name = skinnedRenderer.gameObject.name,
            //         GameObjectPath = GetGameObjectPath(skinnedRenderer.gameObject),
            //         IsActive = skinnedRenderer.gameObject.activeInHierarchy,
            //         IsEnabled = skinnedRenderer.enabled
            //     });
            // }
            //
            // // Cache MeshRenderers (rigid accessories like glasses, hats, etc.)
            // MeshRenderer[] meshRenderers = avatarRoot.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            //
            // ReportHub.Log(GetReportCategory(), $"Found {meshRenderers.Length} MeshRenderers");
            //
            // foreach (MeshRenderer meshRenderer in meshRenderers)
            // {
            //     // Get MeshFilter - this is the only GetComponent call per renderer
            //     MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
            //     
            //     if (meshFilter == null || meshFilter.sharedMesh == null)
            //     {
            //         ReportHub.LogWarning(GetReportCategory(), $"MeshRenderer on {meshRenderer.gameObject.name} has no MeshFilter or mesh");
            //         continue;
            //     }
            //
            //     cachedMeshes.Add(new CachedMeshData
            //     {
            //         Mesh = meshFilter.sharedMesh,
            //         Materials = meshRenderer.sharedMaterials,
            //         Bones = null, // Rigid meshes have no bones
            //         RootBone = null,
            //         Transform = meshRenderer.transform,
            //         Renderer = meshRenderer,
            //         IsSkinnedMesh = false,
            //         Name = meshRenderer.gameObject.name,
            //         GameObjectPath = GetGameObjectPath(meshRenderer.gameObject),
            //         IsActive = meshRenderer.gameObject.activeInHierarchy,
            //         IsEnabled = meshRenderer.enabled
            //     });
            // }
            //
            // ReportHub.Log(GetReportCategory(), $"Cached {cachedMeshes.Count} total meshes ({skinnedRenderers.Length} skinned, {meshRenderers.Length} rigid)");
            //
            //return cachedMeshes;
            return default;
        }

        private GameObject DuplicateSkeletonObject(Transform armatureTransform, Transform hipsTransform, out Dictionary<string, Transform> bones)
        {
            // TODO: Remove this offset
            Vector3 debugAvatarSpawnOffset = new Vector3(-2f, 0f, 0f);

            GameObject duplicateRoot = new GameObject("DCL_Avatar");
            duplicateRoot.transform.SetPositionAndRotation(armatureTransform.position + debugAvatarSpawnOffset, armatureTransform.rotation);
            duplicateRoot.transform.localScale = armatureTransform.transform.localScale;
            duplicateRoot.AddComponent<Animator>();
            var boneRenderer = duplicateRoot.AddComponent<BoneRenderer>();

            // Creating hips manually to avoid coping M_Head_BaseMesh which is added to the bone structure.
            Transform hips = new GameObject(hipsTransform.gameObject.name).transform;
            hips.parent = duplicateRoot.transform;
            hips.SetLocalPositionAndRotation(hipsTransform.localPosition, hipsTransform.localRotation);
            hips.transform.localScale = hipsTransform.localScale;
            
            bones = new Dictionary<string, Transform>();
            bones[hips.name] = hips.transform;
            
            CopyChildrenRecursive(hipsTransform, hips, bones);
            boneRenderer.transforms = new Transform[bones.Count];
            int i = 0;
            foreach (var bone in bones.Values)
            {
                boneRenderer.transforms[i] = bone.transform;
                i++;
            }
            return duplicateRoot;
            
            void CopyChildrenRecursive(Transform source, Transform destParent, Dictionary<string, Transform> bones)
            {
                foreach (Transform sourceChild in source)
                {
                    Transform child = new GameObject(sourceChild.name).transform;
                    child.SetParent(destParent);
                    child.SetLocalPositionAndRotation(sourceChild.localPosition, sourceChild.localRotation);
                    child.localScale = sourceChild.localScale;
                    bones[sourceChild.name] = child;
        
                    // Recurse
                    CopyChildrenRecursive(sourceChild, child.transform, bones);
                }
            }
        }
        
        // TODO: move this to utils
        public Avatar CreateAvatarFromSkeleton(AvatarBase avatarBase)
        {
            var animator = avatarBase.AvatarAnimator;
            if (animator == null)
            {
                Debug.LogError("No Animator found!");
                return null;
            }

            // Create human bone mappings using DCL anchor points
            var humanBones = new Dictionary<HumanBodyBones, Transform>
            {
                // Core
                { HumanBodyBones.Hips, avatarBase.HipAnchorPoint },
                { HumanBodyBones.Spine, avatarBase.SpineAnchorPoint },
                { HumanBodyBones.Chest, avatarBase.Spine1AnchorPoint },
                { HumanBodyBones.UpperChest, avatarBase.Spine2AnchorPoint },
                { HumanBodyBones.Neck, avatarBase.NeckAnchorPoint },
                { HumanBodyBones.Head, avatarBase.HeadAnchorPoint },
                
                // Left Arm
                { HumanBodyBones.LeftShoulder, avatarBase.LeftShoulderAnchorPoint },
                { HumanBodyBones.LeftUpperArm, avatarBase.LeftArmAnchorPoint },
                { HumanBodyBones.LeftLowerArm, avatarBase.LeftForearmAnchorPoint },
                { HumanBodyBones.LeftHand, avatarBase.LeftHandAnchorPoint },
                
                // Right Arm
                { HumanBodyBones.RightShoulder, avatarBase.RightShoulderAnchorPoint },
                { HumanBodyBones.RightUpperArm, avatarBase.RightArmAnchorPoint },
                { HumanBodyBones.RightLowerArm, avatarBase.RightForearmAnchorPoint },
                { HumanBodyBones.RightHand, avatarBase.RightHandAnchorPoint },
                
                // Left Leg
                { HumanBodyBones.LeftUpperLeg, avatarBase.LeftUpLegAnchorPoint },
                { HumanBodyBones.LeftLowerLeg, avatarBase.LeftLegAnchorPoint },
                { HumanBodyBones.LeftFoot, avatarBase.LeftFootAnchorPoint },
                { HumanBodyBones.LeftToes, avatarBase.LeftToeBaseAnchorPoint },
                
                // Right Leg
                { HumanBodyBones.RightUpperLeg, avatarBase.RightUpLegAnchorPoint },
                { HumanBodyBones.RightLowerLeg, avatarBase.RightLegAnchorPoint },
                { HumanBodyBones.RightFoot, avatarBase.RightFootAnchorPoint },
                { HumanBodyBones.RightToes, avatarBase.RightToeBaseAnchorPoint },
                
                // TODO: Got to add finger mapping to our avatar, since we only have 2 fingers right now
                // Fingers
                // { HumanBodyBones.LeftIndexProximal, avatarBase.LeftHandIndexAnchorPoint },
                // { HumanBodyBones.RightIndexProximal, avatarBase.RightHandIndexAnchorPoint },
            };

            var validBones = new Dictionary<HumanBodyBones, Transform>();
            foreach (var kvp in humanBones)
            {
                if (kvp.Value != null) 
                    validBones.Add(kvp.Key, kvp.Value);
            }

            if (validBones.Count < 15) // Minimum required bones for humanoid
            {
                Debug.LogError($"Not enough bones found! Only {validBones.Count} bones available.");
                return null;
            }

            // We have to reset to T-pose since our implementation does not work well with UniHumanoid's structure.
            EnforceTPose(validBones);

            return null;
            var avatarDescription = AvatarDescription.Create();
            avatarDescription.SetHumanBones(validBones);
            
            Avatar avatar = avatarDescription.CreateAvatar(animator.transform);
            if (!avatar.isValid)
            {
                Debug.LogError("Created avatar is invalid! Checking HumanDescription...");
                return null;
            }
            animator.avatar = avatar;
            
            return avatar;
        }
        
        private void EnforceTPose(Dictionary<HumanBodyBones, Transform> bones)
        {
            // Reset all bones to identity rotation first
            foreach (var bone in bones.Values)
            {
                bone.localRotation = Quaternion.identity;
            }
            
            // Apply specific T-pose rotations for exceptions
            if (bones.TryGetValue(HumanBodyBones.Hips, out var hips))
            {
                // Resetting armature rotation
                hips.parent.transform.localRotation = Quaternion.identity;
                hips.localRotation = Quaternion.Euler(0, 0, 180);
            }
            
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
            
            SkinnedMeshRenderer mainRenderer = avatarBase.AvatarSkinnedMeshRenderer;
            
            if (mainRenderer == null || mainRenderer.sharedMesh == null)
            {
                sb.AppendLine("│ ⚠ No main renderer or mesh found!");
                sb.AppendLine("└──────────────────────────────────────────────────────────────┘");
                sb.AppendLine();
                return;
            }

            bool isActive = mainRenderer.gameObject.activeInHierarchy;
            string color = isActive ? "green" : "red";
            
            Mesh mesh = mainRenderer.sharedMesh;
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
                Material mat = mainRenderer.sharedMaterials[i];
                if (mat != null)
                    sb.AppendLine($"│   <color={{color}}>[{{i}}]</color> {mat.name} (Shader: {mat.shader.name})");
            }
            
            sb.AppendLine("└──────────────────────────────────────────────────────────────┘");
            sb.AppendLine();
        }

        private void DebugAllRenderers(AvatarBase avatarBase, StringBuilder sb)
        {
            sb.AppendLine("┌─ ALL SKINNED MESH RENDERERS ─────────────────────────────────┐");
            
            SkinnedMeshRenderer[] allRenderers = avatarBase.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            sb.AppendLine($"│ Total Count: {allRenderers.Length}");
            sb.AppendLine("│");
            
            for (int i = 0; i < allRenderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = allRenderers[i];
                bool isActive = renderer.gameObject.activeInHierarchy;
                string color = isActive ? "green" : "red";
                
                sb.AppendLine($"│ <color={color}>[{i}]</color> {renderer.gameObject.name}");
                sb.AppendLine($"│     Path: {GetGameObjectPath(renderer.gameObject)}");
                sb.AppendLine($"│     Active: {renderer.gameObject.activeInHierarchy}, Enabled: {renderer.enabled}");
                
                if (renderer.sharedMesh != null)
                {
                    Mesh mesh = renderer.sharedMesh;
                    sb.AppendLine($"│     Mesh: {mesh.name} (<color={color}>{mesh.vertexCount} verts</color>, <color={color}>{renderer.bones.Length} bones</color>)");
                    sb.AppendLine($"│     Root Bone: {(renderer.rootBone ? renderer.rootBone.name : "NULL")}");
                    
                    // Sample bones
                    if (renderer.bones.Length > 0)
                    {
                        int boneCount = Mathf.Min(3, renderer.bones.Length);
                        sb.Append($"│     Bones: ");
                        for (int b = 0; b < boneCount; b++)
                        {
                            if (renderer.bones[b] != null)
                                sb.Append($"{renderer.bones[b].name}, ");
                        }
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
            
            Transform[] allBones = avatarBase.Armature.GetComponentsInChildren<Transform>();
            sb.AppendLine($"│ Total Bones:   {allBones.Length}");
            sb.AppendLine("│");
            sb.AppendLine("│ Hierarchy (first 20 bones):");
            
            for (int i = 0; i < Mathf.Min(20, allBones.Length); i++)
            {
                int depth = GetDepth(allBones[i], avatarBase.Armature);
                string indent = new string('─', depth);
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
                sb.AppendLine($"│   ✗ {label,-12}: NULL");
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
            {
                sb.AppendLine("│ No wearables loaded");
            }
            else
            {
                foreach (var wearableInfo in wearablesList)
                {
                    sb.AppendLine($"│ • {wearableInfo.InstanceName}");
                    sb.AppendLine($"│   MainAssetInfo:  {wearableInfo.MainAssetInfo}");
                    sb.AppendLine($"│   Renderers:    {wearableInfo.Renderers}");
                    sb.AppendLine($"│   Mesh:         {wearableInfo.MeshesNames}");
                    sb.AppendLine("│");
                }
            }
            
            sb.AppendLine("└──────────────────────────────────────────────────────────────┘");
            sb.AppendLine();
        }

        private List<WearableDebugInfo> GetLoadedWearables(ref AvatarShapeComponent avatarShape)
        {
            var result = new List<WearableDebugInfo>();

            foreach (var wearable in avatarShape.InstantiatedWearables)
            {
                result.Add(new WearableDebugInfo(wearable));
            }
            
            return result;
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            
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
            Transform current = bone;
            
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
                for (int i = 0; i < meshFilters.Length; i++)
                {
                    Meshes[i] = meshFilters[i].mesh;
                }

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