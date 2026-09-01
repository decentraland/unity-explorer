using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using Global.Dynamic;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Editor
{
    // Declared inside the namespace on purpose: aliases at file scope lose to the DCL.Gizmos and DCL.Time
    // namespaces, which name lookup reaches first while walking out of DCL.AvatarRendering.AvatarShape.Editor
    using Gizmos = UnityEngine.Gizmos;
    using Time = UnityEngine.Time;

    /// <summary>
    ///     Draws the three boxes an avatar carries so their fit can be compared: the one
    ///     AvatarShapeVisibilitySystem frustum-tests, the ghost-renderer box it used to test, and the union of
    ///     Renderer.bounds that Unity culls the drawn geometry with. Each is read from the same source its own
    ///     consumer reads, so a box shown here is the box that decision was made on.
    /// </summary>
    public static class AvatarBaseBoundsGizmo
    {
        private const string MENU_PATH = "Decentraland/Debug/Draw AvatarBase Bounds";
        private const string ENABLED_PREF_KEY = "DCL.AvatarBaseBoundsGizmo.Enabled";
        private const float ROOT_BONE_MARKER_RADIUS = 0.06f;

        // Yellow / red: the box the frustum test reads, coloured by its verdict
        private static readonly Color TESTED_IN_FRUSTUM_COLOR = new (1f, 0.85f, 0.1f, 1f);
        private static readonly Color TESTED_CULLED_COLOR = new (1f, 0.25f, 0.2f, 1f);
        private static readonly Color TESTED_NO_CAMERA_COLOR = new (0.8f, 0.8f, 0.8f, 1f);

        // Magenta: the ghost-renderer box this test read before, kept for comparison
        private static readonly Color GHOST_BOUNDS_COLOR = new (1f, 0.3f, 0.9f, 1f);

        // Cyan: what Unity culls the drawn geometry with, which is the hardcoded cube from SetupMesh
        private static readonly Color DRAWN_GEOMETRY_COLOR = new (0.2f, 0.85f, 1f, 1f);
        private static readonly Color ROOT_BONE_COLOR = Color.white;

        private static readonly QueryDescription AVATARS_WITH_SKINNING =
            new QueryDescription().WithAll<AvatarBase, AvatarCustomSkinningComponent>();

        private static readonly Plane[] FRUSTUM_PLANES = new Plane[6];
        private static readonly List<Renderer> CHILD_RENDERERS = new ();
        private static readonly Dictionary<AvatarBase, Bounds> TESTED_BOUNDS = new ();

        private static int cachedFrame = -1;

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(ENABLED_PREF_KEY, true);
            set => EditorPrefs.SetBool(ENABLED_PREF_KEY, value);
        }

        [MenuItem(MENU_PATH)]
        private static void ToggleEnabled()
        {
            Enabled = !Enabled;
            Menu.SetChecked(MENU_PATH, Enabled);
            SceneView.RepaintAll();
        }

        [MenuItem(MENU_PATH, true)]
        private static bool ValidateToggleEnabled()
        {
            Menu.SetChecked(MENU_PATH, Enabled);
            return true;
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        private static void DrawAvatarBaseBounds(AvatarBase avatarBase, GizmoType gizmoType)
        {
            if (!Enabled) return;

            RefreshTestedBounds();

            Camera mainCamera = Camera.main;
            bool hasTested = TESTED_BOUNDS.TryGetValue(avatarBase, out Bounds tested);
            bool inFrustum = hasTested && mainCamera != null && IsInsideFrustum(mainCamera, tested);

            if (hasTested)
            {
                Gizmos.color = mainCamera == null ? TESTED_NO_CAMERA_COLOR : inFrustum ? TESTED_IN_FRUSTUM_COLOR : TESTED_CULLED_COLOR;
                Gizmos.DrawWireCube(tested.center, tested.size);
            }

            SkinnedMeshRenderer ghostRenderer = avatarBase.AvatarSkinnedMeshRenderer;

            // The reference is serialized, so a prefab that was never wired up can leave it empty
            if (ghostRenderer != null)
            {
                Bounds ghost = ghostRenderer.bounds;
                Gizmos.color = GHOST_BOUNDS_COLOR;
                Gizmos.DrawWireCube(ghost.center, ghost.size);

                Transform rootBone = ghostRenderer.rootBone;

                if (rootBone != null)
                {
                    Gizmos.color = ROOT_BONE_COLOR;
                    Gizmos.DrawWireSphere(rootBone.position, ROOT_BONE_MARKER_RADIUS);
                }
            }

            bool hasDrawnGeometry = TryGetDrawnGeometryBounds(avatarBase, ghostRenderer, out Bounds drawn);

            if (hasDrawnGeometry)
            {
                Gizmos.color = DRAWN_GEOMETRY_COLOR;
                Gizmos.DrawWireCube(drawn.center, drawn.size);
            }

            if ((gizmoType & GizmoType.Selected) != 0)
                DrawReadout(avatarBase, tested, hasTested, drawn, hasDrawnGeometry, mainCamera, inFrustum);
        }

        /// <summary>
        ///     Collects, once per frame, the exact box AvatarShapeVisibilitySystem tests: ToWorldBounds over the
        ///     LocalBounds snapshot the entity's skinning component holds. Recomputing the union from the live
        ///     hierarchy instead would let this gizmo disagree with the culling it exists to explain — the snapshot
        ///     is taken at instantiation, so any drift between the two is precisely what needs to stay visible.
        /// </summary>
        private static void RefreshTestedBounds()
        {
            if (cachedFrame == Time.frameCount) return;

            cachedFrame = Time.frameCount;
            TESTED_BOUNDS.Clear();

            World world = GlobalWorld.ECSWorldInstance;

            // Null until the global world is built, and in edit mode
            if (world == null) return;

            foreach (ref Chunk chunk in world.Query(AVATARS_WITH_SKINNING))
            {
                AvatarBase[] avatars = chunk.GetArray<AvatarBase>();
                AvatarCustomSkinningComponent[] skinnings = chunk.GetArray<AvatarCustomSkinningComponent>();

                foreach (int entityIndex in chunk)
                {
                    if (entityIndex < 0) continue;

                    AvatarBase avatar = avatars[entityIndex];

                    if (avatar == null) continue;

                    TESTED_BOUNDS[avatar] = skinnings[entityIndex].ToWorldBounds(avatar.transform);
                }
            }
        }

        private static bool IsInsideFrustum(Camera camera, Bounds bounds)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, FRUSTUM_PLANES);
            return GeometryUtility.TestPlanesAABB(FRUSTUM_PLANES, bounds);
        }

        /// <summary>
        ///     Unions the bounds of every renderer under the avatar except the ghost. With the compute-shader
        ///     skinning pipeline these are MeshRenderers whose local bounds were replaced by a fixed 5 m cube, so
        ///     the union reports that cube rather than the real silhouette.
        /// </summary>
        private static bool TryGetDrawnGeometryBounds(AvatarBase avatarBase, Renderer ghostRenderer, out Bounds drawn)
        {
            drawn = default(Bounds);
            var found = false;

            avatarBase.GetComponentsInChildren(true, CHILD_RENDERERS);

            foreach (Renderer renderer in CHILD_RENDERERS)
            {
                if (renderer == ghostRenderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (found)
                    drawn.Encapsulate(renderer.bounds);
                else
                {
                    drawn = renderer.bounds;
                    found = true;
                }
            }

            CHILD_RENDERERS.Clear();
            return found;
        }

        private static void DrawReadout(AvatarBase avatarBase, Bounds tested, bool hasTested, Bounds drawn,
            bool hasDrawnGeometry, Camera mainCamera, bool inFrustum)
        {
            string verdict = mainCamera == null ? "no Camera.main" : inFrustum ? "IN FRUSTUM" : "CULLED";

            string testedLine = hasTested
                ? $"yellow  tested bounds  size {Format(tested.size)}  center {Format(tested.center)}"
                : "yellow  tested bounds  no skinning component, never stamped";

            string drawnLine = hasDrawnGeometry
                ? $"cyan    Renderer.bounds union  size {Format(drawn.size)}  center {Format(drawn.center)}"
                : "cyan    Renderer.bounds union  none enabled";

            Vector3 labelAnchor = hasTested
                ? tested.center + (Vector3.up * tested.extents.y)
                : avatarBase.transform.position;

            Handles.Label(labelAnchor, $@"{avatarBase.name}
{testedLine}
{drawnLine}
magenta ghost renderer bounds, tested before
frustum: {verdict}");
        }

        private static string Format(Vector3 value) =>
            $"({value.x:0.00}, {value.y:0.00}, {value.z:0.00})";
    }
}
