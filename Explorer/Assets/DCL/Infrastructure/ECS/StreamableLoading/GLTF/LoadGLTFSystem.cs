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

            // In player builds, GLTFast cannot produce Mecanim clips at runtime (it relies on
            // AnimationClip.SetCurve, which is Editor-only in runtime). For scene emotes loaded in
            // local-scene-development on a player build, we import clips as Legacy and tag the root
            // with a LegacyImportedAnimationsMarker; EmotePlayer's Playable-graph fork plays the
            // legacy clip directly via AnimationClipPlayable on the avatar's Animator, without ever
            // needing to mutate the clip.
            bool useLegacyImportForMecanim =
                intention.MecanimAnimationClips
                && isLocalSceneDevelopment;
                // && !Application.isEditor;

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

            if (intention.MecanimAnimationClips)
            {
                if (useLegacyImportForMecanim)
                    // Forked path: tag with marker; playback layer handles the legacy clip via PlayableGraph.
                    AttachLegacyEmoteMarker(gltfImport, rootContainer);
                else
                    // Normal Mecanim path: when GLTFast loads a GLB locally without a RuntimeAnimatorController,
                    // build one from BaseAnimatorController and the imported clips.
                    ApplyBaseAnimatorControllerWhenNeeded(gltfImport, rootContainer);
            }

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
        /// If the GLB has an Animator but no RuntimeAnimatorController (e.g. loaded locally by GLTFast),
        /// builds an AnimatorOverrideController from Resources/BaseAnimatorController and the GLTF animation clips.
        /// </summary>
        private static void ApplyBaseAnimatorControllerWhenNeeded(GltfImport gltfImport, GameObject rootContainer)
        {
            Animator? animator = rootContainer.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController != null)
                return;

            RuntimeAnimatorController? baseController = Resources.Load<RuntimeAnimatorController>("BaseAnimatorController");
            if (baseController == null)
                return;

            AnimationClip[]? gltfClips = gltfImport.GetAnimationClips();
            if (gltfClips == null || gltfClips.Length == 0)
                return;

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
        }

        /// <summary>
        /// Tags the imported root with a <see cref="LegacyImportedAnimationsMarker"/> carrying Mecanim-flagged
        /// clones of the imported legacy clips. The Playables API refuses legacy-flagged clips with
        /// "ArgumentException: Legacy clips cannot be used in Playables.", so we clone via
        /// Object.Instantiate and flip clone.legacy = false. AnimationClip.legacy is a playback-system
        /// selector over the same curve data, and a freshly Instantiated clip is not "in use" by any
        /// Animation/Animator, so the setter is accepted. The importer-owned originals are left alone.
        /// Also destroys the UnityEngine.Animation component(s) GLTFast attached for the Legacy import,
        /// since playback runs through the avatar's Animator via PlayableGraph.
        /// </summary>
        private static void AttachLegacyEmoteMarker(GltfImport gltfImport, GameObject rootContainer)
        {
            AnimationClip[]? gltfClips = gltfImport.GetAnimationClips();
            int clipCount = gltfClips == null ? 0 : gltfClips.Length;
            Debug.Log($"(Maurizio) AttachLegacyEmoteMarker: root='{rootContainer.name}' clipCount={clipCount}");

            if (gltfClips == null || gltfClips.Length == 0)
            {
                Debug.Log("(Maurizio) AttachLegacyEmoteMarker: no clips, skipping marker");
                return;
            }

            AnimationClip? avatarClip = null;
            AnimationClip? propClip = null;

            if (gltfClips.Length == 1)
                avatarClip = gltfClips[0];
            else
                foreach (AnimationClip clip in gltfClips)
                    if (clip != null && clip.name.Contains("_avatar", StringComparison.OrdinalIgnoreCase))
                        avatarClip = clip;
                    else if (clip != null && clip.name.Contains("_prop", StringComparison.OrdinalIgnoreCase))
                        propClip = clip;

            // Remove the Animation component(s) GLTFast attached for the Legacy import — the clips stay
            // alive (owned by GltfImport), but we don't want the Animation component auto-playing them
            // and we want them to stop being "in use" before we (would ever) touch the originals.
            Animation[] legacyAnimations = rootContainer.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < legacyAnimations.Length; i++)
                UnityEngine.Object.Destroy(legacyAnimations[i]);

            // Clone + flip legacy on the clones so AnimationClipPlayable.Create accepts them.
            AnimationClip? avatarClipMecanim = CloneAsMecanim(avatarClip);
            AnimationClip? propClipMecanim = CloneAsMecanim(propClip);

            var marker = rootContainer.AddComponent<LegacyImportedAnimationsMarker>();
            marker.AvatarClip = avatarClipMecanim;
            marker.PropClip = propClipMecanim;

            Debug.Log($"(Maurizio) AttachLegacyEmoteMarker: avatarClip='{(avatarClipMecanim != null ? avatarClipMecanim.name : "null")}' avatarLegacy={(avatarClipMecanim != null && avatarClipMecanim.legacy)} propClip='{(propClipMecanim != null ? propClipMecanim.name : "null")}' propLegacy={(propClipMecanim != null && propClipMecanim.legacy)} destroyedAnimationComponents={legacyAnimations.Length}");
        }

        /// <summary>
        /// Clones a legacy-flagged AnimationClip via Object.Instantiate and flips the clone's
        /// <see cref="AnimationClip.legacy"/> to false so it can be fed to the Playables API.
        /// Returns null when the input is null.
        /// </summary>
        private static AnimationClip? CloneAsMecanim(AnimationClip? legacy)
        {
            if (legacy == null) return null;

            AnimationClip clone = UnityEngine.Object.Instantiate(legacy);
            clone.name = legacy.name; // Instantiate appends "(Clone)"
            clone.legacy = false;
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
