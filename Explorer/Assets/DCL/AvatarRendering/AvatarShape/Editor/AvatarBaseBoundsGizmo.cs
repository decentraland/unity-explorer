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
    ///     Labels every avatar with the culling verdict FinishAvatarMatricesCalculationSystem acts on, and draws the
    ///     three boxes an avatar carries so their fit can be compared: the one that system frustum-tests, the
    ///     ghost-renderer box that used to be tested, and the union of Renderer.bounds that Unity culls the drawn
    ///     geometry with. Every box is read from the source its own consumer reads, and the verdict comes from that
    ///     system's own predicate, so what is shown here is what the runtime decided.
    /// </summary>
    public static class AvatarBaseBoundsGizmo
    {
        private const string MENU_PATH = "Decentraland/Debug/Draw AvatarBase Bounds";
        private const string ENABLED_PREF_KEY = "DCL.AvatarBaseBoundsGizmo.Enabled";
        private const float ROOT_BONE_MARKER_RADIUS = 0.06f;

        // Yellow / red: the box the frustum test reads, coloured by the culling verdict
        private static readonly Color SKINNING_COLOR = new (1f, 0.85f, 0.1f, 1f);
        private static readonly Color CULLED_COLOR = new (1f, 0.25f, 0.2f, 1f);

        // Magenta: the ghost-renderer box this test read before, kept for comparison
        private static readonly Color GHOST_BOUNDS_COLOR = new (1f, 0.3f, 0.9f, 1f);

        // Cyan: what Unity culls the drawn geometry with, which is the hardcoded cube from SetupMesh
        private static readonly Color DRAWN_GEOMETRY_COLOR = new (0.2f, 0.85f, 1f, 1f);
        private static readonly Color ROOT_BONE_COLOR = Color.white;

        private static readonly QueryDescription CULLABLE_AVATARS =
            new QueryDescription().WithAll<AvatarBase, AvatarCustomSkinningComponent, AvatarShapeComponent, AvatarTransformMatrixComponent>();

        private static readonly Plane[] FRUSTUM_PLANES = new Plane[6];
        private static readonly List<Renderer> CHILD_RENDERERS = new ();
        private static readonly Dictionary<AvatarBase, CullingInputs> CULLING_INPUTS = new ();

        private static GUIStyle verdictLabel;
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

            RefreshCullingInputs();

            // The system reads the ECS camera singleton, which DCL.Editor cannot reference; in a scene with one
            // tagged MainCamera these are the same camera.
            Camera mainCamera = Camera.main;

            bool hasInputs = CULLING_INPUTS.TryGetValue(avatarBase, out CullingInputs inputs);
            bool hasVerdict = hasInputs && mainCamera != null;

            // Hands the rule the same argument the system does, short-circuiting the frustum for exempt avatars
            bool culled = hasVerdict
                          && AvatarCullingRule.IsCulled(inputs.ExemptFromCulling, inputs.IsVisible,
                              inputs.ExemptFromCulling || IsInsideFrustum(mainCamera, inputs.Bounds));

            if (hasInputs)
            {
                Gizmos.color = culled ? CULLED_COLOR : SKINNING_COLOR;
                Gizmos.DrawWireCube(inputs.Bounds.center, inputs.Bounds.size);
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

            DrawVerdict(avatarBase, inputs, hasInputs, culled, mainCamera != null);

            if ((gizmoType & GizmoType.Selected) != 0)
                DrawDetails(avatarBase, inputs, hasInputs, drawn, hasDrawnGeometry);
        }

        /// <summary>
        ///     Collects, once per frame, everything FinishAvatarMatricesCalculationSystem feeds its culling rule:
        ///     the LocalBounds snapshot the skinning component holds placed in the world, plus the exemption and
        ///     visibility flags. The runtime gets its bounds from BoneMatrixCalculationJob instead; that array is
        ///     not reachable from editor code, so the identical formula is applied here through ToWorldBounds. The
        ///     only way the two can differ is the avatar moving between the job gather and this draw.
        /// </summary>
        private static void RefreshCullingInputs()
        {
            if (cachedFrame == Time.frameCount) return;

            cachedFrame = Time.frameCount;
            CULLING_INPUTS.Clear();

            World world = GlobalWorld.ECSWorldInstance;

            // Null until the global world is built, and in edit mode
            if (world == null) return;

            foreach (ref Chunk chunk in world.Query(CULLABLE_AVATARS))
            {
                AvatarBase[] avatars = chunk.GetArray<AvatarBase>();
                AvatarCustomSkinningComponent[] skinnings = chunk.GetArray<AvatarCustomSkinningComponent>();
                AvatarShapeComponent[] shapes = chunk.GetArray<AvatarShapeComponent>();
                AvatarTransformMatrixComponent[] matrices = chunk.GetArray<AvatarTransformMatrixComponent>();

                foreach (int entityIndex in chunk)
                {
                    AvatarBase avatar = avatars[entityIndex];

                    if (avatar == null) continue;

                    CULLING_INPUTS[avatar] = new CullingInputs(
                        skinnings[entityIndex].ToWorldBounds(avatar.transform),
                        matrices[entityIndex].IsMainPlayer,
                        shapes[entityIndex].IsPreview,
                        shapes[entityIndex].IsVisible);
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

        /// <summary>
        ///     Always-on one-liner above the avatar, so the culling state is readable from the scene view without
        ///     selecting anything. Names the reason as well as the verdict, since an avatar can skip culling for
        ///     three different reasons.
        /// </summary>
        private static void DrawVerdict(AvatarBase avatarBase, CullingInputs inputs, bool hasInputs, bool culled, bool hasCamera)
        {
            string verdict;

            if (!hasInputs)
                verdict = "no skinning component, never culled";
            else if (!hasCamera)
                verdict = "no Camera.main, verdict unknown";
            else if (culled)
                verdict = inputs.IsVisible ? "CULLED - out of frustum" : "CULLED - hidden";
            else if (inputs.IsMainPlayer)
                verdict = "SKINNING - main player";
            else if (inputs.IsPreview)
                verdict = "SKINNING - preview";
            else
                verdict = "SKINNING - in frustum";

            // Built lazily: a GUIStyle constructed during static initialisation carries no font and draws nothing
            verdictLabel ??= new GUIStyle(EditorStyles.label);
            verdictLabel.normal.textColor = culled ? CULLED_COLOR : SKINNING_COLOR;

            Handles.Label(LabelAnchor(avatarBase, inputs, hasInputs, above: true),
                $"{avatarBase.name}  {verdict}", verdictLabel);
        }

        private static void DrawDetails(AvatarBase avatarBase, CullingInputs inputs, bool hasInputs, Bounds drawn, bool hasDrawnGeometry)
        {
            string testedLine = hasInputs
                ? $"tested bounds  size {Format(inputs.Bounds.size)}  center {Format(inputs.Bounds.center)}"
                : "tested bounds  none";

            string drawnLine = hasDrawnGeometry
                ? $"cyan    Renderer.bounds union  size {Format(drawn.size)}  center {Format(drawn.center)}"
                : "cyan    Renderer.bounds union  none enabled";

            Handles.Label(LabelAnchor(avatarBase, inputs, hasInputs, above: false),
                $@"{testedLine}
{drawnLine}
magenta ghost renderer bounds, tested before");
        }

        private static Vector3 LabelAnchor(AvatarBase avatarBase, CullingInputs inputs, bool hasInputs, bool above)
        {
            if (!hasInputs)
                return avatarBase.transform.position;

            float offset = above ? inputs.Bounds.extents.y : -inputs.Bounds.extents.y;
            return inputs.Bounds.center + (Vector3.up * offset);
        }

        private static string Format(Vector3 value) =>
            $"({value.x:0.00}, {value.y:0.00}, {value.z:0.00})";

        /// <summary>
        ///     Everything the culling rule consumes for one avatar, snapshotted once per frame.
        /// </summary>
        private readonly struct CullingInputs
        {
            public readonly Bounds Bounds;
            public readonly bool IsMainPlayer;
            public readonly bool IsPreview;
            public readonly bool IsVisible;

            public bool ExemptFromCulling => IsMainPlayer || IsPreview;

            public CullingInputs(Bounds bounds, bool isMainPlayer, bool isPreview, bool isVisible)
            {
                Bounds = bounds;
                IsMainPlayer = isMainPlayer;
                IsPreview = isPreview;
                IsVisible = isVisible;
            }
        }
    }
}
