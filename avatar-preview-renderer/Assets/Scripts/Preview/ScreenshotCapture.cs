using System;
using Loading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Preview
{
    /// <summary>
    /// Renders the preview into a PNG at a caller-chosen size. A second camera set up from the main one
    /// renders for exactly one regular frame into an offscreen target, so it goes through the full URP
    /// stack, post-processing included, exactly as the live view does. Nothing drawn by UI Toolkit
    /// reaches a camera, so the in-canvas controls never appear in the capture.
    /// </summary>
    public class ScreenshotCapture
    {
        private const string CAPTURE_CAMERA_NAME = "ScreenshotCamera";
        private const int BYTES_PER_PIXEL = 4;

        private readonly Camera _mainCamera;
        private readonly AvatarLoader _avatarLoader;
        private readonly WearableLoader _wearableLoader;

        private Camera _captureCamera;

        public bool IsCapturing { get; private set; }

        public ScreenshotCapture(Camera mainCamera, AvatarLoader avatarLoader, WearableLoader wearableLoader)
        {
            _mainCamera = mainCamera;
            _avatarLoader = avatarLoader;
            _wearableLoader = wearableLoader;
        }

        /// <summary>
        /// Captures what the main camera shows right now, at <paramref name="width"/> by
        /// <paramref name="height"/> pixels. The vertical framing is the live one; the horizontal extent
        /// follows the requested aspect, so a square capture of a wide canvas is its centre.
        /// </summary>
        /// <returns>The PNG, base64 encoded.</returns>
        /// <exception cref="InvalidOperationException">A capture is already running or the readback failed.</exception>
        public async Awaitable<string> CaptureAsync(int width, int height)
        {
            if (IsCapturing) throw new InvalidOperationException("A screenshot is already being taken");

            IsCapturing = true;

            var captureCamera = GetCaptureCamera();

            // URP renders a camera with a target texture straight into that texture's format, so the target
            // has to be HDR or bloom finds nothing above its threshold. The blit below into an sRGB texture
            // gives the readback the same 8-bit encoding the screen gets.
            var hdrTarget = RenderTexture.GetTemporary(width, height, 24, GraphicsFormat.R16G16B16A16_SFloat);
            var target = RenderTexture.GetTemporary(width, height, 0, GraphicsFormat.R8G8B8A8_SRGB);

            try
            {
                captureCamera.CopyFrom(_mainCamera);
                captureCamera.transform.SetPositionAndRotation(_mainCamera.transform.position,
                    _mainCamera.transform.rotation);
                captureCamera.targetTexture = hdrTarget;
                captureCamera.ResetAspect();

                // After the main camera, so the live frame keeps its outline and the hook below refills
                // the outline list for this render alone.
                captureCamera.depth = _mainCamera.depth + 1;

                RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
                captureCamera.gameObject.SetActive(true);

                // The request can arrive at any point of a frame, so a whole frame has to pass before the
                // target is guaranteed to hold a render.
                await Awaitable.NextFrameAsync();
                await Awaitable.EndOfFrameAsync();

                captureCamera.gameObject.SetActive(false);
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

                Graphics.Blit(hdrTarget, target);
                var readback = await AsyncGPUReadback.RequestAsync(target);

                if (readback.hasError) throw new InvalidOperationException("The GPU readback failed");

                return EncodePng(readback.GetData<byte>(), width, height);
            }
            finally
            {
                captureCamera.gameObject.SetActive(false);
                captureCamera.targetTexture = null;
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
                RenderTexture.ReleaseTemporary(hdrTarget);
                RenderTexture.ReleaseTemporary(target);
                IsCapturing = false;
            }
        }

        /// <summary>
        /// Turns the premultiplied pixels a transparent capture holds into the straight alpha a PNG
        /// stores. Transparent materials blend their colour by alpha into the clear colour and bloom
        /// adds colour without touching alpha, so alpha is raised to the brightest channel where it
        /// falls short and the colour divided back out. Composited over black, the result is exactly
        /// what the live view shows. Any 4-byte channel order works, as long as alpha comes last.
        /// </summary>
        internal static void RecoverStraightAlpha(NativeArray<byte> pixels)
        {
            for (var i = 0; i < pixels.Length; i += BYTES_PER_PIXEL)
            {
                int c0 = pixels[i];
                int c1 = pixels[i + 1];
                int c2 = pixels[i + 2];
                var alpha = Math.Max(pixels[i + 3], Math.Max(c0, Math.Max(c1, c2)));

                if (alpha == 0) continue;

                pixels[i + 3] = (byte)alpha;

                if (alpha == byte.MaxValue) continue;

                pixels[i] = (byte)((c0 * byte.MaxValue + alpha / 2) / alpha);
                pixels[i + 1] = (byte)((c1 * byte.MaxValue + alpha / 2) / alpha);
                pixels[i + 2] = (byte)((c2 * byte.MaxValue + alpha / 2) / alpha);
            }
        }

        private void OnBeginCameraRendering(ScriptableRenderContext _, Camera camera)
        {
            if (camera != _captureCamera) return;

            _avatarLoader.RefreshOutlineRenderers();
            _wearableLoader.RefreshOutlineRenderers();
        }

        // Built once and kept disabled between captures. Not cloned from the main camera: a clone would
        // wake a second Cinemachine brain and preview controller for a frame, and those act on the scene.
        private Camera GetCaptureCamera()
        {
            if (_captureCamera != null) return _captureCamera;

            var cameraObject = new GameObject(CAPTURE_CAMERA_NAME);
            cameraObject.SetActive(false);
            cameraObject.transform.SetParent(_mainCamera.transform.parent, false);

            _captureCamera = cameraObject.AddComponent<Camera>();
            _captureCamera.CopyFrom(_mainCamera);

            // The renderer, anti-aliasing, post-processing and volume settings live on the URP camera data.
            // Copied as serialized data so a settings change on the main camera never has to be mirrored
            // here field by field.
            var mainData = _mainCamera.GetUniversalAdditionalCameraData();
            var captureData = _captureCamera.GetUniversalAdditionalCameraData();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(mainData), captureData);

            return _captureCamera;
        }

        private static string EncodePng(NativeArray<byte> source, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
                var destination = texture.GetRawTextureData<byte>();
                var rowBytes = width * BYTES_PER_PIXEL;

                // Rows arrive in the texture's own order, so none of the flipping the canvas capture in
                // JSBridge needs: a camera target is not the back buffer. Verified on Direct3D 11 in
                // the Editor; WebGPU shares its top-left texture origin.
                for (var y = 0; y < height; y++)
                {
                    var sourceRow = source.GetSubArray(y * rowBytes, rowBytes);
                    NativeArray<byte>.Copy(sourceRow, 0, destination, y * rowBytes, rowBytes);
                }

                RecoverStraightAlpha(destination);

                return Convert.ToBase64String(texture.EncodeToPNG());
            }
            finally
            {
                Object.Destroy(texture);
            }
        }
    }
}
