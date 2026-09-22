using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Lobby
{
    /// <summary>
    ///     3D set the lobby avatar stands in: a floor, the platform, a blob shadow, a mist plane and an upright, opaque backdrop
    ///     wall standing well behind the avatar. Everything that depends on the backdrop image comes from a
    ///     <see cref="LobbyStagePreset" />; the floor dissolves into the image along a line placed at the preset's screen height,
    ///     independent of how far back the wall stands.
    ///     The avatar is spawned by the character preview, so the stage only has to sit at the same preview position. Once a
    ///     camera is tracked the stage refits itself right before that camera renders, with the lens it actually renders with.
    /// </summary>
    [ExecuteAlways]
    public class LobbyStage : MonoBehaviour
    {
        private static readonly int BASE_MAP = Shader.PropertyToID("_BaseMap");
        private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
        private static readonly int SPOT_COLOR = Shader.PropertyToID("_SpotColor");
        private static readonly int COLOR = Shader.PropertyToID("_Color");
        private static readonly int BLEND_AXIS = Shader.PropertyToID("_BlendAxis");
        private static readonly int BLEND_START = Shader.PropertyToID("_BlendStart");
        private static readonly int BLEND_END = Shader.PropertyToID("_BlendEnd");
        private static readonly int BLEND_HARDNESS = Shader.PropertyToID("_BlendHardness");

        [SerializeField] private Renderer backdrop = null!;
        [SerializeField] private Renderer floor = null!;
        [SerializeField] private Renderer mist = null!;
        [SerializeField] private Transform propsRoot = null!;
        [SerializeField] private LobbyStagePreset? preset;

        private MaterialPropertyBlock? backdropProperties;
        private MaterialPropertyBlock? floorProperties;
        private MaterialPropertyBlock? mistProperties;
        private Texture? backgroundOverride;
        private Camera? trackedCamera;
        private LobbyStagePreset? spawnedPropsPreset;
        private int spawnedPropsVersion;

        /// <summary>
        ///     Switches the whole look: image, blend, tints and props.
        /// </summary>
        public void ApplyPreset(LobbyStagePreset newPreset)
        {
            preset = newPreset;
            ApplyPresetValues();
            RebuildPropsIfStale();
        }

        /// <summary>
        ///     The camera the stage keeps itself fitted to; null stops tracking.
        /// </summary>
        public void Track(Camera? camera)
        {
            trackedCamera = camera;
        }

        /// <summary>
        ///     Shows this image instead of the preset's one until the next <see cref="ApplyPreset" />; null goes back to the preset.
        /// </summary>
        public void SetBackground(Texture? texture)
        {
            backgroundOverride = texture;
            ApplyPresetValues();
        }

        /// <summary>
        ///     Places the floor dissolve so it completes at the preset's screen height, then stands the backdrop upright facing the
        ///     camera at its current horizontal distance and scales it so the part above the blend line covers the frustum up to
        ///     the top edge while keeping the image aspect (the excess is cropped). The lens is read from the camera, so this is
        ///     exact when called as the camera is about to render (see <see cref="Track" />).
        /// </summary>
        public void FitBackdrop(Camera camera)
        {
            if (preset == null) return;

            Transform cameraTransform = camera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            Vector3 flatForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            float halfHeight = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = (float)camera.pixelWidth / camera.pixelHeight;

            Ray blendRay = ViewportRay(cameraTransform, halfHeight, aspect, 0.5f, preset.BlendScreenHeight);
            PlaceFloorBlend(blendRay, flatForward, preset);

            Transform backdropTransform = backdrop.transform;
            float distance = Vector3.Dot(backdropTransform.position - cameraPosition, flatForward);
            var wall = new Plane(-flatForward, cameraPosition + (flatForward * distance));
            Vector3 wallCentre = wall.ClosestPointOnPlane(cameraPosition);
            Vector3 right = Vector3.Cross(Vector3.up, flatForward);

            float floorY = floor.bounds.max.y;
            float blendY = wall.Raycast(blendRay, out float blendEnter) ? Mathf.Max(blendRay.GetPoint(blendEnter).y, floorY) : floorY;
            float requiredTop = blendY;
            float requiredHalfWidth = 0f;

            for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
            {
                Ray ray = ViewportRay(cameraTransform, halfHeight, aspect, x, y);
                if (!wall.Raycast(ray, out float enter)) continue;

                Vector3 hit = ray.GetPoint(enter);
                requiredTop = Mathf.Max(requiredTop, hit.y);
                requiredHalfWidth = Mathf.Max(requiredHalfWidth, Mathf.Abs(Vector3.Dot(hit - wallCentre, right)));
            }

            Texture? texture = CurrentBackground();
            float textureAspect = texture != null ? (float)texture.width / texture.height : aspect;

            Vector2 size = CoverSize(requiredHalfWidth * 2f, requiredTop - blendY, textureAspect, preset.ImageBelowBlend);

            Vector3 centre = wallCentre;
            centre.y = blendY - (size.y * preset.ImageBelowBlend) + (size.y * 0.5f);

            backdropTransform.SetPositionAndRotation(centre, Quaternion.LookRotation(flatForward));
            backdropTransform.localScale = new Vector3(size.x, size.y, 1f);
        }

        /// <summary>
        ///     Smallest size with the texture aspect whose part above the blend line (1 - belowBlend of the height) covers the
        ///     required width and height.
        /// </summary>
        public static Vector2 CoverSize(float requiredWidth, float requiredHeightAboveBlend, float textureAspect, float belowBlend)
        {
            float height = Mathf.Max(requiredHeightAboveBlend / (1f - belowBlend), requiredWidth / textureAspect);
            return new Vector2(height * textureAspect, height);
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            ApplyPresetValues();
            RebuildPropsIfStale();

#if UNITY_EDITOR
            LobbyStagePreset.AnyChanged += OnAnyPresetChanged;
#endif
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

#if UNITY_EDITOR
            LobbyStagePreset.AnyChanged -= OnAnyPresetChanged;
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyPresetValues();
        }

        // Objects cannot be created or destroyed inside a validation callback, so the refresh runs on the next editor tick
        private void OnAnyPresetChanged(LobbyStagePreset changed)
        {
            if (changed != preset) return;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isActiveAndEnabled) return;

                ApplyPresetValues();
                RebuildPropsIfStale();
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            };
        }
#endif

        private void OnBeginCameraRendering(ScriptableRenderContext _, Camera camera)
        {
            if (camera != trackedCamera) return;

            // Re-applied every frame so edits to the preset asset show up live; three property blocks is negligible,
            // props are only rebuilt when the preset or its edit version changed
            ApplyPresetValues();
            RebuildPropsIfStale();
            FitBackdrop(camera);
        }

        // Instances are never saved: the preset is the only source of truth, so scenes and the prefab stay clean
        private void RebuildPropsIfStale()
        {
            if (preset == spawnedPropsPreset && (preset == null || preset.Version == spawnedPropsVersion)) return;

            for (int i = propsRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = propsRoot.GetChild(i).gameObject;

                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            spawnedPropsPreset = preset;
            spawnedPropsVersion = preset != null ? preset.Version : 0;
            if (preset == null) return;

            float floorLocalY = floor.transform.localPosition.y;

            foreach (LobbyStageProp prop in preset.Props)
            {
                if (prop.Prefab == null) continue;

                GameObject instance = Instantiate(prop.Prefab, propsRoot);
                instance.hideFlags = HideFlags.DontSave;
                instance.transform.SetLocalPositionAndRotation(prop.Position + (Vector3.up * floorLocalY), Quaternion.Euler(prop.Rotation));
                instance.transform.localScale = prop.Scale;

                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = gameObject.layer;
            }
        }

        private void ApplyPresetValues()
        {
            if (preset == null) return;

            backdropProperties ??= new MaterialPropertyBlock();
            backdrop.GetPropertyBlock(backdropProperties);
            Texture? background = CurrentBackground();

            if (background != null)
                backdropProperties.SetTexture(BASE_MAP, background);

            backdrop.SetPropertyBlock(backdropProperties);

            floorProperties ??= new MaterialPropertyBlock();
            floor.GetPropertyBlock(floorProperties);
            floorProperties.SetColor(BASE_COLOR, preset.FloorColor);
            floorProperties.SetColor(SPOT_COLOR, preset.SpotColor);
            floor.SetPropertyBlock(floorProperties);

            mistProperties ??= new MaterialPropertyBlock();
            mist.GetPropertyBlock(mistProperties);
            mistProperties.SetColor(COLOR, preset.MistColor);
            mist.SetPropertyBlock(mistProperties);
        }

        // Ray through a viewport point (0..1) for the given lens
        private static Ray ViewportRay(Transform cameraTransform, float halfHeight, float aspect, float viewportX, float viewportY)
        {
            var directionCS = new Vector3(((viewportX * 2f) - 1f) * halfHeight * aspect, ((viewportY * 2f) - 1f) * halfHeight, 1f);
            return new Ray(cameraTransform.position, cameraTransform.TransformDirection(directionCS));
        }

        // The dissolve is a world-space band on the floor, perpendicular to the view, ending where the blend ray meets the floor
        private void PlaceFloorBlend(Ray blendRay, Vector3 flatForward, LobbyStagePreset activePreset)
        {
            var floorPlane = new Plane(Vector3.up, new Vector3(0f, floor.bounds.max.y, 0f));
            if (!floorPlane.Raycast(blendRay, out float enter)) return;

            float blendEnd = Vector3.Dot(blendRay.GetPoint(enter), flatForward);

            floorProperties ??= new MaterialPropertyBlock();
            floor.GetPropertyBlock(floorProperties);
            floorProperties.SetVector(BLEND_AXIS, flatForward);
            floorProperties.SetFloat(BLEND_START, blendEnd - activePreset.BlendDepth);
            floorProperties.SetFloat(BLEND_END, blendEnd);
            floorProperties.SetFloat(BLEND_HARDNESS, activePreset.BlendHardness);
            floor.SetPropertyBlock(floorProperties);
        }

        private Texture? CurrentBackground()
        {
            if (backgroundOverride != null) return backgroundOverride;
            if (preset != null && preset.Backdrop != null) return preset.Backdrop;

            return backdrop.sharedMaterial.GetTexture(BASE_MAP);
        }
    }
}
