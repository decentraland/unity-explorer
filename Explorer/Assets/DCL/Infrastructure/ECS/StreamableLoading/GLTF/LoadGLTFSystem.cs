using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.GLTFast.Wrappers;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.GLTF.DownloadProvider;
using GLTFast;
using GLTFast.Materials;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.GLTF
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    public partial class LoadGLTFSystem: LoadSystemBase<GLTFData, GetGLTFIntention>
    {
        private static MaterialGenerator gltfMaterialGenerator = new DecentralandMaterialGenerator("DCL/Scene");

        private readonly IWebRequestController webRequestController;
        private readonly GltFastReportHubLogger gltfConsoleLogger = new GltFastReportHubLogger();
        private readonly bool patchTexturesFormat;
        private readonly bool importFilesByHash;
        private readonly bool isLocalSceneDevelopment;
        private readonly IGltFastDownloadStrategy downloadStrategy;

        internal LoadGLTFSystem(World world,
            IStreamableCache<GLTFData, GetGLTFIntention> cache,
            IWebRequestController webRequestController,
            bool patchTexturesFormat,
            bool importFilesByHash,
            bool isLocalSceneDevelopment,
            IGltFastDownloadStrategy downloadStrategy) : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.patchTexturesFormat = patchTexturesFormat;
            this.importFilesByHash = importFilesByHash;
            this.isLocalSceneDevelopment = isLocalSceneDevelopment;
            this.downloadStrategy = downloadStrategy;
        }

        protected override async UniTask<StreamableLoadingResult<GLTFData>> FlowInternalAsync(GetGLTFIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            var reportData = new ReportData(GetReportCategory());

            // Acquired budget is released inside GLTFastDownloadedProvider once the GLTF has been fetched
            // Cannot inject DownloadProvider from outside, because it needs the AcquiredBudget and PartitionComponent
            using IGLTFastDisposableDownloadProvider gltFastDownloadProvider = downloadStrategy.CreateDownloadProvider(World, intention, partition, reportData, webRequestController, state.AcquiredBudget!);
            gltFastDownloadProvider.SetContentMappings(intention.ContentMappings);

            var gltfImport = new GltfImport(
                downloadProvider: gltFastDownloadProvider,
                logger: gltfConsoleLogger,
                materialGenerator: gltfMaterialGenerator);

            // In player builds, GLTFast cannot produce Mecanim clips at runtime (needs AnimationClip.SetCurve,
            // which is Editor-only in runtime). When a scene emote is loaded in local-scene-development on a
            // build, import as Legacy and convert the resulting clips to Mecanim after load.
            bool useLegacyImportForMecanim =
                intention.MecanimAnimationClips
                && isLocalSceneDevelopment
                && !Application.isEditor;

            AnimationMethod animationMethod = intention.MecanimAnimationClips && !useLegacyImportForMecanim
                ? AnimationMethod.Mecanim
                : AnimationMethod.Legacy;

            Debug.Log($"(Maurizio) LoadGLTFSystem: name='{intention.Name}' hash='{intention.Hash}' isLocalSceneDevelopment={isLocalSceneDevelopment} isEditor={Application.isEditor} mecanimRequested={intention.MecanimAnimationClips} useLegacyImportForMecanim={useLegacyImportForMecanim} finalAnimationMethod={animationMethod}");

            var gltFastSettings = new ImportSettings
            {
                NodeNameMethod = NameImportMethod.OriginalUnique,
                AnisotropicFilterLevel = 0,
                GenerateMipMaps = false,
                AnimationMethod = animationMethod,
                TexturesReadable = true,
            };

            bool success = await gltfImport.Load(importFilesByHash ? intention.Hash : intention.Name, gltFastSettings, ct);
            if (!success) return new StreamableLoadingResult<GLTFData>(reportData, new Exception("The content to download couldn't be found"));

            // We do the GameObject instantiation in this system since 'InstantiateMainSceneAsync()' is async.
            var rootContainer = new GameObject(gltfImport.GetSceneName(0));

            // Let the upper layer decide what to do with the root
            rootContainer.SetActive(false);

            await InstantiateGltfAsync(gltfImport, rootContainer.transform);

            // When GLTFast loads a GLB locally with Mecanim it may not create a RuntimeAnimatorController;
            // in that case we build one from BaseAnimatorController and the imported clips.
            if (intention.MecanimAnimationClips)
                ApplyBaseAnimatorControllerWhenNeeded(gltfImport, rootContainer, useLegacyImportForMecanim);

            // Ensure the tex ends up being RGBA32 for all wearable textures that come from raw GLTFs
            if (patchTexturesFormat)
                PatchTexturesForWearable(gltfImport);

            // Capture hierarchy paths for local scene development debugging
            var hierarchyPaths = isLocalSceneDevelopment ? CaptureHierarchyPaths(rootContainer) : null;

            var gltfData = new GLTFData(gltfImport, rootContainer, hierarchyPaths);
            gltfData.AddReference();
            return new StreamableLoadingResult<GLTFData>(gltfData);

        }

        /// <summary>
        /// Builds an AnimatorOverrideController from Resources/BaseAnimatorController and the GLTF animation
        /// clips when the imported root lacks one.
        ///
        /// When <paramref name="convertFromLegacy"/> is true, the GLB was imported via AnimationMethod.Legacy
        /// (GLTFast attaches a UnityEngine.Animation component with legacy clips instead of an Animator). We
        /// remove that component, add an Animator, and for each clip that gets assigned to an override slot
        /// we clone it via Object.Instantiate and flip clone.legacy = false — AnimationClip.legacy is a
        /// playback-system selector over the same curve data, and the importer-marked original is effectively
        /// read-only.
        /// </summary>
        private static void ApplyBaseAnimatorControllerWhenNeeded(GltfImport gltfImport, GameObject rootContainer, bool convertFromLegacy)
        {
            AnimationClip[]? gltfClips = gltfImport.GetAnimationClips();
            Debug.Log($"(Maurizio) ApplyBaseAnimatorControllerWhenNeeded: root='{rootContainer.name}' convertFromLegacy={convertFromLegacy} clipCount={(gltfClips == null ? 0 : gltfClips.Length)}");

            if (gltfClips == null || gltfClips.Length == 0)
            {
                Debug.Log("(Maurizio) ApplyBaseAnimatorControllerWhenNeeded: no clips, aborting");
                return;
            }

            Animator? animator = rootContainer.GetComponentInChildren<Animator>(true);

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                Debug.Log($"(Maurizio) ApplyBaseAnimatorControllerWhenNeeded: animator '{animator.name}' already has controller, leaving it alone");
                return;
            }

            if (animator == null)
            {
                Animation? legacyAnim = rootContainer.GetComponentInChildren<Animation>(true);
                GameObject host = legacyAnim != null ? legacyAnim.gameObject : rootContainer;

                Debug.Log($"(Maurizio) ApplyBaseAnimatorControllerWhenNeeded: no Animator found. legacyAnimationFound={(legacyAnim != null)} host='{host.name}'");

                if (legacyAnim != null)
                    UnityEngine.Object.Destroy(legacyAnim);

                animator = host.AddComponent<Animator>();
            }
            else
                Debug.Log($"(Maurizio) ApplyBaseAnimatorControllerWhenNeeded: animator '{animator.name}' found without controller, will assign one");

            RuntimeAnimatorController? baseController = Resources.Load<RuntimeAnimatorController>("BaseAnimatorController");
            if (baseController == null)
            {
                Debug.Log("(Maurizio) ApplyBaseAnimatorControllerWhenNeeded: BaseAnimatorController not found in Resources, aborting");
                return;
            }

            AnimationClip? avatarClip = null;
            AnimationClip? propClip = null;

            if (gltfClips.Length == 1)
                avatarClip = gltfClips[0];
            else if (gltfClips.Length > 1)
                foreach (AnimationClip clip in gltfClips)
                    if (clip != null && clip.name.Contains("_avatar", StringComparison.OrdinalIgnoreCase))
                        avatarClip = clip;
                    else if (clip != null && clip.name.Contains("_prop", StringComparison.OrdinalIgnoreCase))
                        propClip = clip;

            Debug.Log($"(Maurizio) ApplyBaseAnimatorControllerWhenNeeded: picked avatarClip='{(avatarClip != null ? avatarClip.name : "null")}' propClip='{(propClip != null ? propClip.name : "null")}'");

            if (convertFromLegacy)
            {
                if (avatarClip != null) avatarClip = ToMecanim(avatarClip);
                if (propClip != null) propClip = ToMecanim(propClip);
            }

            var overrideController = new AnimatorOverrideController(baseController);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);

            for (int i = 0; i < overrides.Count; i++)
            {
                KeyValuePair<AnimationClip, AnimationClip> kv = overrides[i];
                AnimationClip original = kv.Key;
                if (original == null) continue;

                bool isAvatarSlot = original.name.Contains("AvatarAnimationPlaceholder", StringComparison.OrdinalIgnoreCase);
                bool isPropSlot = original.name.Contains("PropAnimationPlaceholder", StringComparison.OrdinalIgnoreCase);

                if (isAvatarSlot && avatarClip != null)
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, avatarClip);
                else if (isPropSlot && propClip != null)
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, propClip);
            }

            overrideController.ApplyOverrides(overrides);
            animator.runtimeAnimatorController = overrideController;

            Debug.Log($"(Maurizio) ApplyBaseAnimatorControllerWhenNeeded: assigned AnimatorOverrideController to '{animator.name}' (overrideSlots={overrides.Count})");
        }

        private static AnimationClip ToMecanim(AnimationClip legacyClip)
        {
            AnimationClip clone = UnityEngine.Object.Instantiate(legacyClip);
            clone.name = legacyClip.name;
            clone.legacy = false;
            Debug.Log($"(Maurizio) ToMecanim: cloned '{legacyClip.name}' (sourceLegacy={legacyClip.legacy}) -> cloneLegacy={clone.legacy}");
            return clone;
        }

        private void PatchTexturesForWearable(GltfImport gltfImport)
        {
            for (int i = 0; i < gltfImport.TextureCount; i++)
            {
                var originalTexture = gltfImport.GetTexture(i);

                // Note: BC7 (asset bundle textures optimization) cannot be compressed in runtime with
                // Unity so a different format was chosen: RGBA32
                var compressedTexture = TextureUtilities.EnsureRGBA32Format(originalTexture);
                if (compressedTexture == originalTexture)
                    continue;

                // Copy properties from original texture
                compressedTexture.wrapMode = originalTexture.wrapMode;
                compressedTexture.filterMode = originalTexture.filterMode;
                compressedTexture.anisoLevel = originalTexture.anisoLevel;

                // Replace texture in all materials that use it
                for (int matIndex = 0; matIndex < gltfImport.MaterialCount; matIndex++)
                {
                    var material = gltfImport.GetMaterial(matIndex);

                    // Check all texture properties in the material
                    foreach (string propertyName in material.GetTexturePropertyNames())
                    {
                        if (material.GetTexture(propertyName) == originalTexture)
                        {
                            material.SetTexture(propertyName, compressedTexture);
                        }
                    }
                }

                // Clean up original texture
                UnityEngine.Object.Destroy(originalTexture);
            }
        }

        private async UniTask InstantiateGltfAsync(GltfImport gltfImport, Transform rootContainerTransform)
        {
            if (gltfImport.SceneCount > 1)
                for (int i = 0; i < gltfImport.SceneCount; i++)
                {
                    var targetTransform = rootContainerTransform;

                    if (i != 0)
                    {
                        var go = new GameObject($"{rootContainerTransform.name}_{i.ToString()}");
                        Transform goTransform = go.transform;
                        goTransform.SetParent(rootContainerTransform, false);
                        targetTransform = goTransform;
                    }

                    await gltfImport.InstantiateSceneAsync(targetTransform, i);
                }
            else
                await gltfImport.InstantiateSceneAsync(rootContainerTransform);
        }

        /// <summary>
        /// Captures all possible paths in the GLTF GameObject hierarchy for LSD debugging purposes
        /// </summary>
        private static List<string> CaptureHierarchyPaths(GameObject rootContainer)
        {
            var paths = new List<string>();

            // Start from the GLTF root's children
            if (rootContainer.transform.childCount > 0)
            {
                var gltfRoot = rootContainer.transform.GetChild(0);
                for (int i = 0; i < gltfRoot.childCount; i++)
                {
                    CaptureHierarchyPathsRecursive(gltfRoot.GetChild(i), "", paths);
                }
            }

            // Sort paths for easier reading in debug output
            paths.Sort();
            return paths;
        }

        /// <summary>
        /// Recursively captures all paths in the hierarchy
        /// </summary>
        private static void CaptureHierarchyPathsRecursive(Transform transform, string currentPath, List<string> paths)
        {
            // Build the path for this transform
            string transformPath = string.IsNullOrEmpty(currentPath)
                ? transform.name
                : $"{currentPath}/{transform.name}";

            // Add this path if it has a Renderer
            if (transform.GetComponent<Renderer>() != null)
                paths.Add(transformPath);

            // Recursively process children
            for (int i = 0; i < transform.childCount; i++)
            {
                CaptureHierarchyPathsRecursive(transform.GetChild(i), transformPath, paths);
            }
        }
    }
}
