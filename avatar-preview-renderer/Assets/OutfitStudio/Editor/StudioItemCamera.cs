using System;
using System.Collections.Generic;
using System.Linq;
using Loading;
using Runtime.Wearables;
using Unity.Cinemachine;
using UnityEngine;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Frames the camera on whatever is currently visible under the avatar rig, for Single-Item mode's
    /// item-card shots. One-shot positioning rather than a per-frame component (unlike
    /// <see cref="StudioFlyCamera"/>): nothing needs to run every tick, so this stays a static helper
    /// the window calls after an apply.
    ///
    /// **It moves the camera, never the rig.** The obvious-looking alternative,
    /// <c>GameObjectUtils.CenterAndFit</c> (what the marketplace wearable card uses), scales and
    /// recentres the subject instead: <c>root.localScale *= scaleFactor</c> compounds across calls, and
    /// re-centring puts the rig's origin off the item's centre, which turns the turntable into an orbit
    /// instead of a spin. Leaving the rig untouched is what keeps drag-rotate, snap-rotate and
    /// <see cref="TurntableDriver"/> behaving exactly as they do in avatar mode — and it works because
    /// wearables sit on the avatar's vertical axis, so a Y-spin about the rig root is already correct.
    ///
    /// Distance is varied, field of view is never touched: <see cref="StudioCardFrame"/> sizes the card
    /// from <c>fieldOfView</c>, so holding fov constant keeps the card stable between items, and it
    /// keeps the item's perspective consistent shot to shot.
    /// </summary>
    public static class StudioItemCamera
    {
        /// <summary>
        /// Every name <c>GLTFLoader.LoadModel</c> can give a loaded root, i.e. every wearable category.
        /// Same union <c>OutfitDefinition.EffectiveForceRender</c> uses, so the two can't drift: the
        /// priority list misses <c>hands</c>/<c>head</c>, which only appear in the skin-implicit set.
        /// </summary>
        private static readonly HashSet<string> WEARABLE_ROOT_NAMES =
            WearableCategories.CATEGORIES_PRIORITY
                .Union(WearableCategories.SKIN_IMPLICIT_CATEGORIES)
                .ToHashSet();

        /// <summary>
        /// Positions the studio camera so the item fills the frame — or the card rect, with
        /// <c>soloFitToCard</c> — scaled by <c>soloZoomPct</c> (100 = exactly touching the rect, more
        /// crops, less leaves margin) and centred on whichever rect it fitted, then nudged by
        /// <c>soloOffsetXPx</c>/<c>soloOffsetYPx</c>. Keeps the camera's current orientation, so an angle
        /// chosen by dragging or flying survives a re-frame. Returns false with a reason when there's
        /// nothing to frame; on success <paramref name="report"/> describes the result for the status line,
        /// which is what makes a wrong framing diagnosable from a screenshot.
        ///
        /// Fitting to the **frame** is the default because the job this exists for is rendering an item
        /// tight and large to composite a card around it in an image editor — the frame is what bounds
        /// that. Fitting to the card was the original behaviour and produced items at ~52% of frame width
        /// (the card is 0.55 of frame height wide, so that's exactly what it should have produced),
        /// sitting low by the card's own vertical offset. Both are wanted, so it's a switch, not a guess.
        /// </summary>
        public static bool FrameItem(OutfitDefinition outfit, float frameHeightPx, out string error,
            out string report)
        {
            error = null;
            report = null;

            var camera = StudioCardFrame.FindCamera();
            if (camera == null)
            {
                error = "No studio camera found";
                return false;
            }

            if (!TryGetVisibleBounds(outfit.soloFitGarmentOnly, out var bounds))
            {
                error = "Nothing visible to frame — is an item loaded?";
                return false;
            }

            // Cinemachine drives this transform every frame, so it has to be muted first or the write
            // is overwritten before the next render. Same takeover StudioFlyCamera performs; handed
            // back by ReleaseToCinemachine (the Debug tab's "Reset View").
            var brain = camera.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = false;

            // The rect to fill, in frame-HEIGHT units on both axes — the same convention
            // StudioCardFrame.Layout uses, and the part that trips people up: the card's width is stored
            // relative to frame height (so the card keeps its shape across resolutions), which is why
            // cardW isn't multiplied by aspect while the frame's width is.
            var aspect = Mathf.Max(camera.aspect, 1e-4f);
            var toCard = outfit.soloFitToCard && StudioCardFrame.Enabled && !StudioCardFrame.DisableMiddleCard;
            var rectH = toCard ? StudioCardFrame.CardHeightFraction : 1f;
            var rectW = toCard ? Mathf.Max(0.01f, StudioCardFrame.CardWidth) : aspect;

            // Zoom scales both axes of the target rect equally, so the item's on-screen size scales by
            // exactly that factor and nothing about which axis binds changes. Over 100% the target grows
            // past the rect, which is how the item overspills and crops — what the old negative margins
            // were for. Clamped off zero so the division below can't blow up.
            var px = Mathf.Max(frameHeightPx, 1f);
            var zoom = Mathf.Max(outfit.soloZoomPct / 100f, 0.01f);
            var targetH = rectH * zoom;
            var targetW = rectW * zoom;

            // Per-axis rather than one cube-ified max: cube-ifying to max(x,y,z) throws away most of a
            // portrait rect (a wide span would set the distance for a tall narrow item). Using max(x,z)
            // horizontally is still stable under the only rotation this tool applies — drag, snap and the
            // turntable all spin about Y — so re-framing after a drag doesn't breathe.
            var size = bounds.size;
            var itemH = Mathf.Max(size.y, 1e-4f);
            var itemW = Mathf.Max(Mathf.Max(size.x, size.z), 1e-4f);

            // frustumHeight(d) = 2·d·tan(fov/2), and the target rect is a fraction of it. Solve each axis
            // for the distance that just fits and take whichever must be further away.
            var tan = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var distance = Mathf.Max(itemH / (2f * tan * targetH), itemW / (2f * tan * targetW));

            var frustumHeight = 2f * distance * tan;

            // Two vertical shifts plus a horizontal one, all in world units at the chosen distance. The
            // card's own offset only applies when fitting to it (its top and bottom margins differ, so a
            // frame-centred item would sit off-centre in the card); the artist's nudges always apply.
            //
            // Both nudges divide by frame HEIGHT and scale by frustumHeight, the horizontal one included:
            // pixels are square, so one px is the same world distance on either axis.
            //
            // Sign: subtracting along `up` lowers the camera, which raises the item in frame, and
            // subtracting along `right` moves the item right. So the vertical nudge is negated (positive
            // soloOffsetYPx means DOWN, image-editor coordinates) and the horizontal one is not (positive
            // soloOffsetXPx means RIGHT, which those coordinates agree with).
            var cardOffsetY = toCard
                ? ((StudioCardFrame.MarginBottom + (1f - StudioCardFrame.MarginTop)) * 0.5f - 0.5f) * frustumHeight
                : 0f;
            var nudgeY = -outfit.soloOffsetYPx / px * frustumHeight;
            var nudgeX = outfit.soloOffsetXPx / px * frustumHeight;

            camera.transform.position = bounds.center
                                        - camera.transform.forward * distance
                                        - camera.transform.up * (cardOffsetY + nudgeY)
                                        - camera.transform.right * nudgeX;

            var fillH = itemH / frustumHeight;
            var fillW = itemW / (frustumHeight * aspect);
            report = $"{(toCard ? "card" : "frame")}-fit: item {itemW:0.00}×{itemH:0.00} m at {distance:0.00} m "
                     + $"— {fillW * 100f:0}% w, {fillH * 100f:0}% h"
                     + (outfit.soloFitGarmentOnly ? " (garment only)" : "");

            // The bound axis is still worth naming even now that one zoom drives both: it's the axis that
            // sets the size, so it's the edge that crops first on the way past 100%. (Zoom scales both
            // targets equally, so the tag depends only on the item's aspect against the rect's — it can't
            // flip as the slider moves.)
            report += itemH / targetH > itemW / targetW ? " [height-bound]" : " [width-bound]";

            return true;
        }

        /// <summary>
        /// Hands framing back to Cinemachine, so the vcam's authored shot returns. Routed through
        /// <see cref="StudioFlyCamera.ReleaseToCinemachine"/> when the fly camera is attached (it owns
        /// the brain in that case), otherwise re-enables the brain directly.
        /// </summary>
        public static void Release()
        {
            var camera = StudioCardFrame.FindCamera();
            if (camera == null) return;

            var fly = camera.GetComponent<StudioFlyCamera>();
            if (fly != null)
            {
                fly.ReleaseToCinemachine();
                return;
            }

            var brain = camera.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = true;
        }

        /// <summary>
        /// Combined world bounds of the loaded wearables, and nothing else.
        ///
        /// This deliberately **whitelists** the wearable GLB roots instead of scanning everything under
        /// the rig, because the rig itself carries geometry that would wreck the framing:
        /// <c>Avatar_Model_Idle.glb</c> ships a single skinned mesh, <c>M_uBody_BaseMesh</c>, spanning
        /// 1.83 m × 1.91 m (a T-posed reference body). It is active, its renderer is enabled, and
        /// nothing disables it — it goes unnoticed only because its material is
        /// <c>baseColorFactor [0,0,0,1]</c>, i.e. pure black. Encapsulating it made every item frame ~3×
        /// too far away and pulled the centre down to mid-torso, so items sat small and high in the card.
        ///
        /// The whitelist works because <c>GLTFLoader.LoadModel</c> names each root after
        /// <c>entityDefinition.Category</c>, so a loaded wearable always lives under a
        /// category-named ancestor. That also excludes emote props (root <c>"emote"</c>), which
        /// shouldn't size an item shot either, and it holds in edit mode, where the roots sit one level
        /// deeper inside <c>__OutfitStudio_EditPreview</c>.
        ///
        /// Hidden geometry needs no special handling: <c>GetComponentsInChildren&lt;Renderer&gt;()</c>
        /// without <c>includeInactive</c> skips inactive GameObjects, and body parts are suppressed with
        /// <c>SetActive(false)</c> (whole-root in Single-Item mode, per-part by
        /// <c>AvatarUtils.HideBodyShape</c>). glTFast imports skinned meshes with
        /// <c>skinUpdateWhenOffscreen = true</c>, so <c>renderer.bounds</c> is real posed-vertex bounds
        /// rather than the bind-pose box — which is why this must run after a frame has rendered with the
        /// current pose applied.
        ///
        /// **Bare skin is excluded**, so the garment reads at the same size in every pose. Fitting the
        /// whole posed silhouette made the shirt swing ~1.6× between arms-down and arm-outstretched, and
        /// dragged the centre toward whichever limb was extended, because the arms and hands belong to the
        /// <c>upper_body</c> mesh and move far more than the garment does. Skin is identified by material
        /// name, the same convention <c>AvatarUtils.SetupWearable</c> already uses to tint it. The cost is
        /// that an extended limb can now overspill the card (and crop, if the masks are on) — accepted
        /// deliberately: a consistent garment size across a card sheet matters more.
        /// </summary>
        private static bool TryGetVisibleBounds(bool garmentOnly, out Bounds bounds)
        {
            // When excluding skin, fall back to measuring everything if that finds nothing: every material
            // on the item being skin-named is realistic for a `skin`-category full-body costume, and
            // reporting "nothing to frame" at an item plainly on screen would be nonsense.
            if (garmentOnly && TryGetBounds(true, out bounds)) return true;

            return TryGetBounds(false, out bounds);
        }

        private static bool TryGetBounds(bool excludeSkin, out Bounds bounds)
        {
            bounds = default;

            // Excludes inactive by default, which is what we want: the studio scene ships a second
            // (Configurator) rig whose branch is never activated in builder mode.
            // UnityEngine.Object qualified explicitly: `using System` + `using UnityEngine` makes a bare
            // `Object` CS0104-ambiguous.
            var loader = UnityEngine.Object.FindAnyObjectByType<AvatarLoader>();
            if (loader == null) return false;

            var root = loader.transform;
            var found = false;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                // Skinned renderers with no mesh report a degenerate box at the origin, which would
                // drag the framing off the item
                if (renderer is SkinnedMeshRenderer { sharedMesh: null }) continue;
                if (!renderer.enabled) continue;
                if (!IsUnderWearableRoot(renderer.transform, root)) continue;

                Bounds rendererBounds;
                if (excludeSkin)
                {
                    if (!TryGetGarmentBounds(renderer, out rendererBounds)) continue;
                }
                else
                {
                    rendererBounds = renderer.bounds;
                }

                if (!found)
                {
                    bounds = rendererBounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return found && bounds.size.sqrMagnitude > 0f;
        }

        /// <summary>
        /// One renderer's world bounds with bare-skin geometry left out. Returns false when the renderer
        /// is nothing but skin.
        ///
        /// Three cases, because a wearable's fabric and skin usually are <b>not</b> separate renderers:
        /// glTFast turns a glTF mesh's primitives into submeshes of a single
        /// <see cref="SkinnedMeshRenderer"/>, so the common shape is one renderer carrying both a fabric
        /// material and a skin material. Whole-renderer exclusion alone would therefore do nothing on the
        /// exact meshes this is meant to fix, which is why the mixed case bakes the posed mesh and
        /// measures only the non-skin submeshes' vertices.
        /// </summary>
        private static bool TryGetGarmentBounds(Renderer renderer, out Bounds bounds)
        {
            bounds = renderer.bounds;

            var materials = renderer.sharedMaterials;
            if (materials.Length == 0) return true;

            var skinCount = 0;
            foreach (var material in materials)
            {
                if (IsSkinMaterial(material)) skinCount++;
            }

            if (skinCount == 0) return true;              // all garment — the cheap, common path
            if (skinCount == materials.Length) return false; // all skin (a body part, or a bare-arm mesh)

            // Mixed. Only a skinned renderer can be split cheaply; a static mixed mesh keeps its whole box.
            if (renderer is SkinnedMeshRenderer skinned)
                NarrowToNonSkinSubmeshes(skinned, materials, ref bounds);

            return true;
        }

        /// <summary>
        /// Narrows <paramref name="bounds"/> to a skinned renderer's non-skin submeshes, measured from the
        /// <em>posed</em> vertices via <c>BakeMesh</c>. Leaves it untouched if that can't be done
        /// confidently — including a guard for the result not sitting sensibly inside the renderer's own
        /// box, which is what would happen if the assumption about BakeMesh's output space were wrong for
        /// this Unity version. Better a slightly loose frame than one computed from garbage.
        /// </summary>
        private static void NarrowToNonSkinSubmeshes(SkinnedMeshRenderer skinned, Material[] materials,
            ref Bounds bounds)
        {
            var whole = skinned.bounds;

            var baked = new Mesh();
            try
            {
                // No useScale: the snapshot comes out in the renderer's local space, so the renderer's
                // own transform (scale included) is applied below.
                skinned.BakeMesh(baked);

                var vertices = baked.vertices;
                if (vertices.Length == 0) return;

                var toWorld = skinned.transform.localToWorldMatrix;
                var found = false;
                var result = default(Bounds);

                // Unity maps submesh i to material i; a shorter material array means the trailing
                // submeshes reuse the last one, so clamping to the mesh's own count is what matters.
                var submeshes = Mathf.Min(baked.subMeshCount, materials.Length);
                for (var i = 0; i < submeshes; i++)
                {
                    if (IsSkinMaterial(materials[i])) continue;

                    foreach (var index in baked.GetIndices(i))
                    {
                        if (index < 0 || index >= vertices.Length) continue;

                        var point = toWorld.MultiplyPoint3x4(vertices[index]);
                        if (!found)
                        {
                            result = new Bounds(point, Vector3.zero);
                            found = true;
                        }
                        else
                        {
                            result.Encapsulate(point);
                        }
                    }
                }

                if (!found || result.size.sqrMagnitude <= 0f) return;

                // Sanity guard described above: the garment must live inside the whole mesh's box (with
                // slack for AABB-of-OBB conservatism) and can't be bigger than it.
                var slack = whole.size.magnitude * 0.25f + 0.05f;
                var padded = new Bounds(whole.center, whole.size + Vector3.one * slack);
                if (!padded.Contains(result.center) || result.size.magnitude > whole.size.magnitude * 1.05f)
                    return;

                bounds = result;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baked);
            }
        }

        /// <summary>
        /// Whether a material paints bare skin. Same name test <c>AvatarUtils.SetupWearable</c> uses to
        /// decide what to tint with the skin colour, so the two agree by construction.
        /// </summary>
        private static bool IsSkinMaterial(Material material) =>
            material != null && material.name.Contains("skin", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Whether a renderer sits inside a loaded wearable GLB root, i.e. has an ancestor (at or below
        /// <paramref name="stopAt"/>) named after a wearable category — the name
        /// <c>GLTFLoader.LoadModel</c> gives every root it creates.
        /// </summary>
        private static bool IsUnderWearableRoot(Transform transform, Transform stopAt)
        {
            for (var t = transform; t != null && t != stopAt; t = t.parent)
            {
                if (WEARABLE_ROOT_NAMES.Contains(t.name)) return true;
            }

            return false;
        }
    }
}
