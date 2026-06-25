using System;
using DCL.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Provides the "camera off" placeholder texture shown on LiveKit screens when a streamer turns
    ///     their camera off: the static avatar background composited with the streamer's display name
    ///     (like a video call). <see cref="LivekitPlayer" /> returns it in place of the frozen last frame.
    ///     A single offscreen rig (camera + world-space canvas) renders into one reusable render texture;
    ///     the composite is re-rendered only when the streamer name changes.
    ///     Not thread-safe — main-thread only.
    /// </summary>
    public sealed class AvatarPlaceHolderTextureSource : IDisposable
    {
        // The background PNG is authored at 1024x1024 (power-of-two); the rig matches it.
        private const int WIDTH = 1024;
        private const int HEIGHT = 1024;
        private const int UI_LAYER = 5; // Unity built-in "UI" layer.
        private const int LABEL_FONT_SIZE = 64;
        private const float NEAR_CLIP_PLANE = 0.1f;
        private const float FAR_CLIP_PLANE = 100f;

        private static readonly Color BACKGROUND_COLOR = new (0x20 / 255f, 0x21 / 255f, 0x24 / 255f, 1f);
        private static readonly Color32 LABEL_COLOR = new (0x9a, 0xa0, 0xa6, 0xff);

        // Label band below the avatar circle (which sits in the upper-middle of the background).
        private static readonly Vector2 LABEL_ANCHOR_MIN = new (0.06f, 0.18f);
        private static readonly Vector2 LABEL_ANCHOR_MAX = new (0.94f, 0.34f);

        private readonly GameObject rig;
        private readonly Camera camera;
        private readonly TextMeshProUGUI label;
        private readonly RectTransform canvasRect;
        private readonly RenderTexture composite;

        private string? composedName;
        private bool hasComposed;

        public AvatarPlaceHolderTextureSource(Texture? background)
        {
            composite = new RenderTexture(WIDTH, HEIGHT, 0, RenderTextureFormat.BGRA32) { name = "AvatarPlaceholderComposite" };
            composite.Create();

            rig = new GameObject("AvatarPlaceholderRig") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(rig);
            // Far from any scene geometry so the offscreen camera only ever renders its own canvas.
            rig.transform.position = MordorConstants.SCENE_MORDOR_POSITION;

            var cameraObject = new GameObject("Camera") { layer = UI_LAYER };
            cameraObject.transform.SetParent(rig.transform, false);

            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = HEIGHT / 2f; // 1 world unit == 1 px, so the canvas maps 1:1 to the RT.
            camera.aspect = (float)WIDTH / HEIGHT;
            camera.cullingMask = 1 << UI_LAYER;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BACKGROUND_COLOR;
            camera.nearClipPlane = NEAR_CLIP_PLANE;
            camera.farClipPlane = FAR_CLIP_PLANE;
            camera.enabled = false; // Rendered manually via camera.Render().

            // World-space canvas with an explicit WIDTH x HEIGHT size sits 1 unit in front of the camera and
            // maps 1:1 to the render texture. Unlike a screen-space-camera canvas it needs no global
            // Canvas.ForceUpdateCanvases() to track the viewport (see TextureFor).
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)) { layer = UI_LAYER };
            canvasObject.transform.SetParent(rig.transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, 0f, 1f);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(WIDTH, HEIGHT);

            // No background texture configured: the camera's solid-color clear shows through, so the
            // placeholder degrades to a plain dark screen with the name instead of a separate code path.
            if (background != null)
            {
                var backgroundObject = new GameObject("Background", typeof(RawImage)) { layer = UI_LAYER };
                backgroundObject.transform.SetParent(canvasObject.transform, false);
                var backgroundImage = backgroundObject.GetComponent<RawImage>();
                backgroundImage.texture = background;
                Stretch(backgroundImage.rectTransform, Vector2.zero, Vector2.one);
            }

            var labelObject = new GameObject("Label", typeof(TextMeshProUGUI)) { layer = UI_LAYER };
            labelObject.transform.SetParent(canvasObject.transform, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();

            // The project always ships a default TMP font; warn (rather than silently rendering no name
            // text) if a headless/test setup is missing one.
            if (TMP_Settings.defaultFontAsset == null)
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"{nameof(AvatarPlaceHolderTextureSource)}: no default TMP font configured — the camera-off placeholder will show no streamer name.");

            label.font = TMP_Settings.defaultFontAsset;
            label.color = LABEL_COLOR;
            label.fontSize = LABEL_FONT_SIZE;
            label.alignment = TextAlignmentOptions.Center;
            label.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(label.rectTransform, LABEL_ANCHOR_MIN, LABEL_ANCHOR_MAX);
        }

        public void Dispose()
        {
            composite.Release();
            Object.Destroy(composite);
            Object.Destroy(rig);
        }

        /// <summary>
        ///     Returns the placeholder texture showing the background plus <paramref name="streamerName" />
        ///     (or just the background when the name is empty). The same render texture instance is reused
        ///     across calls and re-rendered only when the name changes.
        /// </summary>
        public Texture TextureFor(string? streamerName)
        {
            if (hasComposed && composedName == streamerName)
                return composite;

            label.text = streamerName ?? string.Empty;

            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
            label.ForceMeshUpdate();

            camera.targetTexture = composite;
            camera.Render();
            camera.targetTexture = null;

            composedName = streamerName;
            hasComposed = true;
            return composite;
        }

        private static void Stretch(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
