using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Composites the static "camera off" background with the streamer's display name into a texture,
    ///     used by <see cref="UpdateMediaPlayerSystem" /> when a LiveKit video track is muted so the screen
    ///     shows "&lt;avatar&gt; &lt;name&gt;" (like a video call) instead of the frozen last frame.
    ///     The offscreen rig (camera + canvas) is built lazily on first use and shared across scenes;
    ///     composites are cached per name so each one is rendered only once.
    ///     NOTE: the visual result (text size, vertical position, color space) must be verified in the
    ///     Unity editor — it cannot be validated headlessly.
    /// </summary>
    public sealed class CameraOffScreenComposer : IDisposable
    {
        // The background PNG is authored at 1024x1024 (power-of-two); the rig matches it.
        private const int WIDTH = 1024;
        private const int HEIGHT = 1024;
        private const int UI_LAYER = 5; // Unity built-in "UI" layer.
        private const int MAX_CACHED = 16;

        // Placed far from any scene geometry so the offscreen camera only ever renders its own canvas.
        private static readonly Vector3 RIG_POSITION = new (0f, 100000f, 0f);
        private static readonly Color BACKGROUND_COLOR = new (0x20 / 255f, 0x21 / 255f, 0x24 / 255f, 1f);
        private static readonly Color32 LABEL_COLOR = new (0x9a, 0xa0, 0xa6, 0xff);

        private readonly Texture? background;
        private readonly Dictionary<string, RenderTexture> cache = new ();

        private GameObject? rig;
        private Camera? camera;
        private TextMeshProUGUI? label;
        private RectTransform? canvasRect;
        private bool rigCreationFailed;

        public CameraOffScreenComposer(Texture? background)
        {
            this.background = background;
        }

        public void Dispose()
        {
            ClearCache();

            if (rig != null)
                Object.Destroy(rig);

            rig = null;
            camera = null;
            label = null;
            canvasRect = null;
        }

        /// <summary>
        ///     Returns a texture showing the camera-off background plus the streamer name. Falls back to the
        ///     plain background when the name is empty or text rendering is unavailable, and to null when no
        ///     background was configured (the caller then renders black).
        /// </summary>
        public Texture? Compose(string? streamerName)
        {
            // No background configured — return null so the caller can pick its own fallback.
            if (background == null)
                return null;

            if (string.IsNullOrEmpty(streamerName))
                return background;

            if (cache.TryGetValue(streamerName, out RenderTexture cached) && cached != null)
                return cached;

            EnsureRig();

            if (camera == null || label == null || canvasRect == null)
                return background;

            if (cache.Count >= MAX_CACHED)
                ClearCache();

            var composite = new RenderTexture(WIDTH, HEIGHT, 0, RenderTextureFormat.BGRA32) { name = "CameraOffComposite" };
            composite.Create();

            label.text = streamerName;

            // Rebuild only this rig's layout/graphics. The world-space canvas has an explicit size, so it
            // needs no global Canvas.ForceUpdateCanvases() (which would flush every canvas in the scene).
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
            label.ForceMeshUpdate();

            camera.targetTexture = composite;
            camera.Render();
            camera.targetTexture = null;

            cache[streamerName] = composite;
            return composite;
        }

        private void EnsureRig()
        {
            if (rig != null || rigCreationFailed) return;

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;

            if (font == null)
            {
                // No default TMP font configured — fall back to the plain background.
                rigCreationFailed = true;
                return;
            }

            rig = new GameObject("CameraOffComposerRig") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(rig);
            // Far from any scene geometry so the offscreen camera only renders its own canvas.
            rig.transform.position = RIG_POSITION;

            var cameraObject = new GameObject("Camera") { layer = UI_LAYER };
            cameraObject.transform.SetParent(rig.transform, false);

            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = HEIGHT / 2f; // 1 world unit == 1 px, so the canvas maps 1:1 to the RT.
            camera.aspect = (float)WIDTH / HEIGHT;
            camera.cullingMask = 1 << UI_LAYER;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BACKGROUND_COLOR;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.enabled = false; // rendered manually via camera.Render().

            // World-space canvas with an explicit WIDTH x HEIGHT size sits 1 unit in front of the camera and
            // maps 1:1 to the render texture. Unlike a screen-space-camera canvas it needs no global
            // Canvas.ForceUpdateCanvases() to track the viewport (see Compose).
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)) { layer = UI_LAYER };
            canvasObject.transform.SetParent(rig.transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, 0f, 1f);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(WIDTH, HEIGHT);

            var backgroundObject = new GameObject("Background", typeof(RawImage)) { layer = UI_LAYER };
            backgroundObject.transform.SetParent(canvasObject.transform, false);
            var backgroundImage = backgroundObject.GetComponent<RawImage>();
            backgroundImage.texture = background;
            Stretch(backgroundImage.rectTransform, Vector2.zero, Vector2.one);

            var labelObject = new GameObject("Label", typeof(TextMeshProUGUI)) { layer = UI_LAYER };
            labelObject.transform.SetParent(canvasObject.transform, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.color = LABEL_COLOR;
            label.fontSize = 64;
            label.alignment = TextAlignmentOptions.Center;
            label.overflowMode = TextOverflowModes.Ellipsis;
            // Below the avatar circle (which sits in the upper-middle of the background).
            Stretch(label.rectTransform, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.34f));
        }

        private static void Stretch(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private void ClearCache()
        {
            foreach (RenderTexture renderTexture in cache.Values)
            {
                if (renderTexture == null) continue;
                renderTexture.Release();
                Object.Destroy(renderTexture);
            }

            cache.Clear();
        }
    }
}
