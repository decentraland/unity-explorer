using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using JetBrains.Annotations;
using Loading;
using Runtime.Wearables;
using UnityEngine;
using UnityEngine.Pool;

namespace Utils
{
    public static class AvatarUtils
    {
        /// <summary>
        /// Processes the wearables that are currently equipped and hides the ones that should be hidden.
        /// </summary>
        /// <param name="bodyShape"></param>
        /// <param name="wearables">Currently equipped wearables</param>
        /// <param name="forceRender">Which parts we shouldn't hide</param>
        /// <returns>A set of all the categories that were hidden.</returns>
        public static HashSet<string> HideWearables(
            BodyShape bodyShape,
            List<EntityDefinition> wearables,
            [CanBeNull] string[] forceRender)
        {
            var combinedHidingList = new HashSet<string>();
            var hiddenCategoriesByCategory = DictionaryPool<string, HashSet<string>>.Get();
        
            // Compose hidden categories lookup
            foreach (var wearable in wearables)
            {
                var hidingList = HashSetPool<string>.Get();
                var rep = wearable[bodyShape];
        
                foreach (var hide in rep.Hides)
                {
                    // Prevent a category from hiding itself (this causes circular reference issues)
                    if (hide != wearable.Category)
                        hidingList.Add(hide);
                }
                
                // Deal with hands - upper body wearables hide hands by default
                if (ShouldHideHands(wearable.Category, rep))
                {
                    // If wearable is forced to be rendered, never remove it
                    if (forceRender == null || !forceRender.Contains(WearableCategories.Categories.HANDS))
                    {
                        hidingList.Add(WearableCategories.Categories.HANDS);
                    }
                }
                
                // Skin has implicit hides
                if (wearable.Category == WearableCategories.Categories.SKIN)
                {
                    foreach (var skinCategory in WearableCategories.SKIN_IMPLICIT_CATEGORIES)
                    {
                        // If wearable is forced to be rendered, never remove it
                        if (forceRender != null && forceRender.Contains(skinCategory)) continue;
            
                        hidingList.Add(skinCategory);
                    }
                }
                
                hiddenCategoriesByCategory[wearable.Category] = hidingList;
            }
            
            WearableUtils.ResolveHidingConflicts(
                hiddenCategoriesByCategory,
                forceRender,
                combinedHidingList);
        
            // Release all HashSet objects back to the pool
            foreach (var hidingList in hiddenCategoriesByCategory.Values)
                HashSetPool<string>.Release(hidingList);
        
            // Release the Dictionary back to the pool
            DictionaryPool<string, HashSet<string>>.Release(hiddenCategoriesByCategory);
        
            return combinedHidingList;
        }
        
        private static bool ShouldHideHands(string category, EntityDefinition.Representation rep)
        {
            // We apply this rule to hide the hands by default if the wearable is an upper body or hides the upper body
            var isOrHidesUpperBody = category == WearableCategories.Categories.UPPER_BODY ||
                                     rep.Hides.Contains(WearableCategories.Categories.UPPER_BODY);

            // The rule is ignored if the wearable contains the removal of this default rule (newer upper bodies since the release of hands)
            var removesHandDefault = rep.RemovesDefaultHiding.Contains(WearableCategories.Categories.HANDS);

            // Why do we do this? Because old upper bodies contain the base hand mesh, and they might clip with the new handwear items
            return isOrHidesUpperBody && !removesHandDefault;
        }

        /// <summary>
        /// Hides parts of the body shape based on which categories are shown and hidden.
        /// </summary>
        /// <param name="bodyShape">The root GO of the body shape</param>
        /// <param name="hiddenCategories">The categories that are being hidden</param>
        /// <param name="loadedCategories">The wearables that are used (equipped)</param>
        public static void HideBodyShape(GameObject bodyShape, HashSet<string> hiddenCategories,
            HashSet<string> loadedCategories)
        {
            var renderers = bodyShape.GetComponentsInChildren<Renderer>(true);

            foreach (var renderer in renderers)
            {
                var name = renderer.name;

                // Support for the old gltf hierarchy for ABs
                if (name.Contains("primitive", StringComparison.OrdinalIgnoreCase))
                    name = renderer.transform.parent.name;

                var isPartMapped = false;

                foreach (var (key, value) in WearablesConstants.BODY_PARTS_MAPPING)
                {
                    if (name.Contains(key, StringComparison.OrdinalIgnoreCase))
                    {
                        renderer.gameObject.SetActive(!(hiddenCategories.Contains(value) ||
                                                        loadedCategories.Contains(value)));
                        isPartMapped = true;
                        break;
                    }
                }

                if (!isPartMapped)
                    Debug.LogWarning($"{name} has not been set-up as a valid body part");
            }
        }

        /// <summary>
        /// Hides facial features on the body shape
        /// </summary>
        public static void HideBodyShapeFacialFeatures(GameObject bodyShape, bool hideEyes, bool hideEyebrows,
            bool hideMouth)
        {
            var renderers = bodyShape.GetComponentsInChildren<Renderer>(true);

            foreach (var renderer in renderers)
            {
                var name = renderer.name;

                // Support for the old gltf hierarchy for ABs
                if (name.Contains("primitive", StringComparison.OrdinalIgnoreCase))
                    name = renderer.transform.parent.name;

                if (hideEyes && name.Contains("eyes", StringComparison.OrdinalIgnoreCase))
                {
                    renderer.gameObject.SetActive(false);
                }

                if (hideEyebrows && name.Contains("eyebrows", StringComparison.OrdinalIgnoreCase))
                {
                    renderer.gameObject.SetActive(false);
                }

                if (hideMouth && name.Contains("mouth", StringComparison.OrdinalIgnoreCase))
                {
                    renderer.gameObject.SetActive(false);
                }
            }
        }

        public static void SetupWearable(GameObject go, AvatarColors colors,
            List<Renderer> outlineRenderers, Transform avatarRootBone = null, Transform[] avatarBones = null)
        {
            Dictionary<string, Transform> avatarBoneMap = null;
            var renderers = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in renderers)
            {
                var materials = renderer.materials;
                foreach (var material in materials)
                {
                    if (material.name.Contains("skin", StringComparison.OrdinalIgnoreCase))
                    {
                        material.SetColor(WearablesConstants.Shaders.BASE_COLOR_ID, colors.Skin);
                    }
                    else if (material.name.Contains("hair", StringComparison.OrdinalIgnoreCase))
                    {
                        material.SetColor(WearablesConstants.Shaders.BASE_COLOR_ID, colors.Hair);
                    }
                }

                if (avatarRootBone != null && avatarBones != null)
                {
                    // Always remap by name — preserves any extra bones (spring chains, attachment
                    // nodes) regardless of bone count vs avatar skeleton. The previous fast path
                    // (renderer.bones = avatarBones) only worked when wearables shipped with the
                    // canonical skeleton intact and dropped extras for any wearable with fewer bones.
                    avatarBoneMap ??= BuildBoneMap(avatarBones);
                    RemapBonesPreservingExtras(renderer, avatarRootBone, avatarBoneMap);
                }

                foreach (var sharedMaterial in renderer.sharedMaterials)
                {
                    if (sharedMaterial != null && sharedMaterial.shader.name == "DCL/DCL_Toon"
                                               && sharedMaterial.renderQueue is >= 2000 and < 3000)
                    {
                        outlineRenderers.Add(renderer);
                        break;
                    }
                }
            }

            // Reparent extra-bone chain tops under live avatar bones so wearable extras (e.g.
            // ponytail rigs) follow animation by default — independent of spring-bone tagging.
            // Without this, untagged or partially-tagged chains stay anchored to the wearable's
            // static skeleton copy and don't move. When chains ARE tagged, SpringBonesDriver
            // sees rootBone.parent == avatarParent and skips its wearable-parent snap (no-op).
            if (avatarBoneMap != null)
                ReparentExtraBonesUnderAvatarSkeleton(go, avatarBoneMap);
        }

        public static void SetupFacialFeatures(GameObject go, AvatarColors colors,
            Dictionary<string, LoadedFacialFeature> loadedFacialFeatures,
            Dictionary<string, (Texture2D main, Texture2D mask)> defaultBodyFacialFeatures)
        {
            // Setup facial features
            foreach (var cat in WearableCategories.FACIAL_FEATURES)
            {
                var ffRenderer = GetFacialFeatureRenderer(cat, go);

                var color = GetFacialFeatureColor(cat, colors);
                ffRenderer.material.SetColor(WearablesConstants.Shaders.BASE_COLOR_ID, color);

                // Save the default ones so we can revert
                if (!defaultBodyFacialFeatures.ContainsKey(cat))
                {
                    defaultBodyFacialFeatures[cat] = (
                        (Texture2D)ffRenderer.material.GetTexture(WearablesConstants.Shaders.MAIN_TEX_ID),
                        (Texture2D)ffRenderer.material.GetTexture(WearablesConstants.Shaders.MASK_TEX_ID));
                }

                var loadedFeature = loadedFacialFeatures.Values.FirstOrDefault(ff => ff.Entity.Category == cat);

                var main = loadedFeature.Entity != null ? loadedFeature.Main : defaultBodyFacialFeatures[cat].main;
                var mask = loadedFeature.Entity != null ? loadedFeature.Mask : defaultBodyFacialFeatures[cat].mask;

                // The default mask for eyes is all white
                if (cat == WearableCategories.Categories.EYES && mask == null)
                {
                    mask = Texture2D.whiteTexture;
                }

                ffRenderer.material.SetTexture(WearablesConstants.Shaders.MAIN_TEX_ID, main);
                ffRenderer.material.SetTexture(WearablesConstants.Shaders.MASK_TEX_ID, mask);
            }
        }

        private static SkinnedMeshRenderer GetFacialFeatureRenderer(string category, GameObject bodyGO)
        {
            var suffix = category switch
            {
                WearableCategories.Categories.EYEBROWS => "Mask_Eyebrows",
                WearableCategories.Categories.EYES => "Mask_Eyes",
                WearableCategories.Categories.MOUTH => "Mask_Mouth",
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };

            var meshRenderers = bodyGO.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            return meshRenderers.FirstOrDefault(mr => mr.name.EndsWith(suffix));
        }

        private static Color GetFacialFeatureColor(string category, AvatarColors colors)
        {
            return category switch
            {
                WearableCategories.Categories.EYEBROWS => colors.Hair,
                WearableCategories.Categories.EYES => colors.Eyes,
                WearableCategories.Categories.MOUTH => colors.Skin,
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static Dictionary<string, Transform> BuildBoneMap(Transform[] bones)
        {
            var map = new Dictionary<string, Transform>(bones.Length);
            foreach (var bone in bones)
            {
                if (bone != null)
                    map[bone.name] = bone;
            }
            return map;
        }

        /// <summary>
        /// Remaps a skinned mesh's bones to the avatar skeleton by name,
        /// preserving any extra bones (e.g. spring bone chains) that don't
        /// exist in the avatar skeleton.
        /// </summary>
        private static void RemapBonesPreservingExtras(SkinnedMeshRenderer renderer,
            Transform avatarRootBone, Dictionary<string, Transform> avatarBoneMap)
        {
            var meshBones = renderer.bones;
            var remapped = new Transform[meshBones.Length];

            for (var i = 0; i < meshBones.Length; i++)
            {
                remapped[i] = meshBones[i] != null && avatarBoneMap.TryGetValue(meshBones[i].name, out var avatarBone)
                    ? avatarBone
                    : meshBones[i];
            }

            renderer.rootBone = avatarRootBone;
            renderer.bones = remapped;
        }

        /// <summary>
        /// Re-parents the roots of extra-bone chains (e.g. spring bone chains) under their
        /// nearest avatar skeleton ancestor so they follow the avatar during emotes.
        /// Only chain roots are re-parented; descendants stay under their chain parent,
        /// preserving the chain hierarchy. Authored local pose is preserved (worldPositionStays=false)
        /// — the wearable-copy parent world transform is stale, so we want the authored local
        /// applied against the live avatar bone's world transform instead.
        /// </summary>
        private static void ReparentExtraBonesUnderAvatarSkeleton(GameObject wearableRoot,
            Dictionary<string, Transform> avatarBoneMap)
        {
            var allTransforms = wearableRoot.GetComponentsInChildren<Transform>(true);

            foreach (var transform in allTransforms)
            {
                if (transform == wearableRoot.transform) continue;
                if (avatarBoneMap.ContainsKey(transform.name)) continue;

                if (transform.parent != null
                    && avatarBoneMap.TryGetValue(transform.parent.name, out var liveParent)
                    && transform.parent != liveParent)
                {
                    transform.SetParent(liveParent, false);
                }
            }
        }
    }
}
