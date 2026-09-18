using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Lobby
{
    /// <summary>
    ///     3D set the lobby avatar stands in: a floor, the platform, a blob shadow and an upright, opaque backdrop wall standing
    ///     well behind the avatar. The floor dissolves into the backdrop along a line placed at a chosen screen height, which is
    ///     the whole floor-to-image gradient and is independent of how far back the wall stands.
    ///     The avatar is spawned by the character preview, so the stage only has to sit at the same preview position. Once a
    ///     camera is tracked the stage refits itself right before that camera renders, with the lens it actually renders with.
    /// </summary>
    [ExecuteAlways]
    public class LobbyStage : MonoBehaviour
    {
        private static readonly int BASE_MAP = Shader.PropertyToID("_BaseMap");
        private static readonly int BLEND_AXIS = Shader.PropertyToID("_BlendAxis");
        private static readonly int BLEND_START = Shader.PropertyToID("_BlendStart");
        private static readonly int BLEND_END = Shader.PropertyToID("_BlendEnd");
        private static readonly int BLEND_HARDNESS = Shader.PropertyToID("_BlendHardness");

        [SerializeField] private Renderer backdrop = null!;
        [SerializeField] private Renderer floor = null!;

        [Tooltip("Screen height, from the bottom, where the floor has fully dissolved into the backdrop")]
        [SerializeField, Range(0.05f, 0.9f)] private float blendScreenHeight = 0.35f;

        [Tooltip("Length of the dissolve on the floor, in metres towards the camera from the blend line")]
        [SerializeField, Min(0.1f)] private float blendDepth = 4f;

        [Tooltip("How abrupt the dissolve is inside the band: 0 is a smooth gradient over the whole depth, 1 is a hard cut at its middle")]
        [SerializeField, Range(0f, 1f)] private float blendHardness;

        [Tooltip("Fraction of the image height kept below the blend line, so the visible band starts higher up the image")]
        [SerializeField, Range(0f, 0.9f)] private float imageBelowBlend = 0.35f;

        private MaterialPropertyBlock? backdropProperties;
        private MaterialPropertyBlock? floorProperties;
        private Camera? trackedCamera;

        /// <summary>
        ///     The camera the stage keeps itself fitted to; null stops tracking.
        /// </summary>
        public void Track(Camera? camera)
        {
            trackedCamera = camera;
        }

        /// <summary>
        ///     Swaps the backdrop image without instantiating a material.
        /// </summary>
        public void SetBackground(Texture texture)
        {
            backdropProperties ??= new MaterialPropertyBlock();
            backdrop.GetPropertyBlock(backdropProperties);
            backdropProperties.SetTexture(BASE_MAP, texture);
            backdrop.SetPropertyBlock(backdropProperties);
        }

        /// <summary>
        ///     Places the floor dissolve so it completes at <see cref="blendScreenHeight" />, then stands the backdrop upright facing
        ///     the camera at its current horizontal distance and scales it so the part above the blend line covers the frustum up
        ///     to the top edge while keeping the image aspect (the excess is cropped). The lens is read from the camera, so this is
        ///     exact when called as the camera is about to render (see <see cref="Track" />).
        /// </summary>
        public void FitBackdrop(Camera camera)
        {
            Transform cameraTransform = camera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            Vector3 flatForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            float halfHeight = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = (float)camera.pixelWidth / camera.pixelHeight;

            Ray blendRay = ViewportRay(cameraTransform, halfHeight, aspect, 0.5f, blendScreenHeight);
            PlaceFloorBlend(blendRay, flatForward);

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

            Vector2 size = CoverSize(requiredHalfWidth * 2f, requiredTop - blendY, textureAspect, imageBelowBlend);

            Vector3 centre = wallCentre;
            centre.y = blendY - (size.y * imageBelowBlend) + (size.y * 0.5f);

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
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext _, Camera camera)
        {
            if (camera == trackedCamera)
                FitBackdrop(camera);
        }

        // Ray through a viewport point (0..1) for the given lens
        private static Ray ViewportRay(Transform cameraTransform, float halfHeight, float aspect, float viewportX, float viewportY)
        {
            var directionCS = new Vector3(((viewportX * 2f) - 1f) * halfHeight * aspect, ((viewportY * 2f) - 1f) * halfHeight, 1f);
            return new Ray(cameraTransform.position, cameraTransform.TransformDirection(directionCS));
        }

        // The dissolve is a world-space band on the floor, perpendicular to the view, ending where the blend ray meets the floor
        private void PlaceFloorBlend(Ray blendRay, Vector3 flatForward)
        {
            var floorPlane = new Plane(Vector3.up, new Vector3(0f, floor.bounds.max.y, 0f));
            if (!floorPlane.Raycast(blendRay, out float enter)) return;

            float blendEnd = Vector3.Dot(blendRay.GetPoint(enter), flatForward);

            floorProperties ??= new MaterialPropertyBlock();
            floor.GetPropertyBlock(floorProperties);
            floorProperties.SetVector(BLEND_AXIS, flatForward);
            floorProperties.SetFloat(BLEND_START, blendEnd - blendDepth);
            floorProperties.SetFloat(BLEND_END, blendEnd);
            floorProperties.SetFloat(BLEND_HARDNESS, blendHardness);
            floor.SetPropertyBlock(floorProperties);
        }

        private Texture? CurrentBackground()
        {
            backdropProperties ??= new MaterialPropertyBlock();
            backdrop.GetPropertyBlock(backdropProperties);

            Texture? overridden = backdropProperties.GetTexture(BASE_MAP);
            return overridden != null ? overridden : backdrop.sharedMaterial.GetTexture(BASE_MAP);
        }
    }
}
