using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Capture backend for the Outfit Studio window.
    ///
    /// Both stills and video go through the Unity Recorder package's camera/Game-view capture hook
    /// (the SRP "after render" callback), which is what makes them match the live Game view exactly,
    /// bloom and all. An earlier version of the still path drove `RenderPipeline.SubmitRenderRequest`
    /// manually into an offscreen RenderTexture — geometry and lighting matched, but Bloom (and any
    /// additive-blended VFX) came out dimmed or missing entirely, since that path doesn't go through
    /// the same per-camera capture hook the interactive render (and Recorder) use. Routing stills
    /// through the same `CameraInputSettings` mechanism as video sidesteps that gap entirely.
    /// </summary>
    public static class OutfitCapture
    {
        private static RecorderController _recorderController;
        private static RecorderController _stillController;
        private static string _stillOutputFolder;
        private static string[] _stillFilesBefore;
        private static Action<string> _stillCompletionCallback;
        private static EditorApplication.CallbackFunction _stillPoll;

        public static bool IsRecording => _recorderController != null && _recorderController.IsRecording();
        public static bool IsCapturingStill => _stillController != null;

        /// <summary>Where captures land by default, relative to the project root.</summary>
        public const string DEFAULT_OUTPUT_FOLDER = "Captures";

        /// <summary>
        /// Captures a single still asynchronously (one Recorder frame — typically completes within a
        /// frame or two of the next Editor update) and invokes <paramref name="onComplete"/> with the
        /// resulting PNG path, or null on failure.
        /// </summary>
        public static void CaptureStill(int width, int height, bool transparentBackground, string outputFolder,
            int upsampleFactor, Action<string> onComplete)
        {
            if (IsRecording || IsCapturingStill)
            {
                Debug.LogWarning("[OutfitStudio] Already capturing");
                onComplete?.Invoke(null);
                return;
            }

            // Rendered at width*factor/height*factor and box-downsampled back down after (see
            // DownsampleFileInPlace) — cheap supersampled AA. A direct render at `width` only ever
            // gets one sample per exported pixel; this gives every edge factor² of them, which is
            // what actually fixes a thin outline reading as eroded/noisy at low capture resolutions.
            upsampleFactor = Mathf.Max(1, upsampleFactor);
            var renderWidth = width * upsampleFactor;
            var renderHeight = height * upsampleFactor;

            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("[OutfitStudio] No main camera found - are you in play mode?");
                onComplete?.Invoke(null);
                return;
            }

            // The card frame has no background layer at all (2026-07-30), so whenever it's on the
            // capture wants alpha: clearing at alpha 0 leaves everything outside the card transparent
            // while the card panel and avatar (both opaque) are unaffected.
            var wantsAlpha = transparentBackground || StudioCardFrame.Enabled;

            var previousFlags = camera.clearFlags;
            var previousColor = camera.backgroundColor;
            if (wantsAlpha)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }

            // The card frame (if active) sizes its quads from camera.aspect on a 0.5 s poll, which
            // tracks the Game view. Force the capture aspect and re-lay-out so a still at a different
            // resolution still frames the card correctly. Restored once the capture completes.
            camera.aspect = width / (float)height;
            StudioCardFrame.RelayoutFor(camera);

            // The avatar outline's renderer list is repopulated each frame by AvatarLoader.Update and
            // cleared by the outline pass after every camera render. The Recorder issues its own
            // extra render for the capture frame, so refresh the list first — otherwise the still has
            // no outline even though the live Game view shows it (it was consumed by this frame's
            // regular Game-view render already).
            var avatarLoader = UnityEngine.Object.FindAnyObjectByType<Loading.AvatarLoader>();
            if (avatarLoader != null) avatarLoader.RefreshOutlineRenderers();

            var folder = ResolveOutputFolder(outputFolder);
            _stillOutputFolder = folder;
            _stillFilesBefore = Directory.GetFiles(folder, "*.png");
            _stillCompletionCallback = onComplete;

            var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();

            var imageSettings = ScriptableObject.CreateInstance<ImageRecorderSettings>();
            imageSettings.name = "Outfit Studio Still";
            imageSettings.Enabled = true;
            imageSettings.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
            imageSettings.CaptureAlpha = wantsAlpha;
            imageSettings.imageInputSettings = new CameraInputSettings
            {
                Source = ImageSource.MainCamera,
                OutputWidth = renderWidth,
                OutputHeight = renderHeight,
                RecordTransparency = wantsAlpha
            };
            imageSettings.OutputFile = GetOutputPath(outputFolder, null); // Recorder appends _<frame>.png

            controllerSettings.AddRecorderSettings(imageSettings);
            controllerSettings.SetRecordModeToSingleFrame(0);
            controllerSettings.FrameRate = 30; // required by settings; irrelevant for a single frame
            controllerSettings.CapFrameRate = false;

            _stillController = new RecorderController(controllerSettings);
            _stillController.PrepareRecording();
            _stillController.StartRecording();

            _stillPoll = () =>
            {
                if (_stillController != null && _stillController.IsRecording()) return;

                EditorApplication.update -= _stillPoll;
                _stillPoll = null;

                camera.clearFlags = previousFlags;
                camera.backgroundColor = previousColor;
                camera.ResetAspect();
                StudioCardFrame.RelayoutFor(camera);

                var newFile = Directory.GetFiles(_stillOutputFolder, "*.png")
                    .Except(_stillFilesBefore)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();

                if (newFile != null)
                {
                    // See RecoverAdditiveAlpha: Bloom/additive VFX write RGB without ever touching
                    // alpha, so over a transparent clear that glow keeps alpha 0 and vanishes on
                    // export even though its color is genuinely there. The Recorder writes straight
                    // to disk, so this has to run as a post-pass on the saved file.
                    if (wantsAlpha) RecoverAdditiveAlphaInFile(newFile);

                    // Runs after alpha recovery, not before: it reconstructs alpha from the raw,
                    // full-resolution bloom brightness, which the downsample would otherwise have
                    // already blurred together with its neighbours.
                    if (upsampleFactor > 1) DownsampleFileInPlace(newFile, width, height);

                    Debug.Log($"[OutfitStudio] Screenshot saved: {newFile}");
                }
                else
                {
                    Debug.LogWarning("[OutfitStudio] Still capture finished but no new PNG was found in the output folder.");
                }

                _stillController = null;
                _stillOutputFolder = null;
                _stillFilesBefore = null;

                var callback = _stillCompletionCallback;
                _stillCompletionCallback = null;
                callback?.Invoke(newFile);
            };
            EditorApplication.update += _stillPoll;
        }

        private static void RecoverAdditiveAlphaInFile(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                tex.LoadImage(File.ReadAllBytes(path));
                RecoverAdditiveAlpha(tex);
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static void RecoverAdditiveAlpha(Texture2D tex)
        {
            var pixels = tex.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                var brightness = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
                if (brightness <= p.a) continue;

                // Fully un-premultiplying (scaling every touched pixel so its brightest channel hits
                // 255) is "physically correct" additive-over-background, but it also fully saturates
                // the dim outer fringe of the bloom blur — which is often a blend of two or more
                // nearby sparkles' colors mixing (e.g. green+yellow bleeding into a muddy olive) and
                // was invisible before purely because its alpha was near 0. Maxing that fringe out
                // makes the mixed hue loudly visible instead of subtle. Curving alpha by brightness^2
                // suppresses those faint/mixed fringe pixels back toward transparent (same as they'd
                // fade to black in the editor) while barely touching genuinely bright sparkle cores,
                // and scaling RGB by the SAME curved fraction (not all the way to 255) keeps cores
                // vivid without forcing the muddy edges into full saturation.
                var brightnessNorm = brightness / 255f;
                var newAlpha = Mathf.RoundToInt(brightnessNorm * brightnessNorm * 255f);
                if (newAlpha <= p.a) continue;
                var scale = newAlpha / (float)brightness;
                pixels[i] = new Color32(
                    (byte)Mathf.Min(255, Mathf.RoundToInt(p.r * scale)),
                    (byte)Mathf.Min(255, Mathf.RoundToInt(p.g * scale)),
                    (byte)Mathf.Min(255, Mathf.RoundToInt(p.b * scale)),
                    (byte)newAlpha);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
        }

        /// <summary>
        /// Box-downsamples the saved PNG from its captured (upsampled) resolution back down to
        /// targetWidth×targetHeight in place. See CaptureStill's upsampleFactor.
        /// </summary>
        private static void DownsampleFileInPlace(string path, int targetWidth, int targetHeight)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D downsampled = null;
            try
            {
                src.LoadImage(File.ReadAllBytes(path));
                if (src.width == targetWidth && src.height == targetHeight) return;

                downsampled = BoxDownsample(src, targetWidth, targetHeight);
                File.WriteAllBytes(path, downsampled.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(src);
                if (downsampled != null) UnityEngine.Object.DestroyImmediate(downsampled);
            }
        }

        /// <summary>
        /// Averages factorX×factorY source texels into each destination texel. Premultiplies by
        /// alpha before averaging and un-premultiplies after — straight averaging would blend a fully
        /// transparent pixel's arbitrary RGB into a partially-covered edge pixel, fringing exactly the
        /// silhouette/outline edges supersampling is meant to clean up with a dark or discoloured halo.
        /// </summary>
        private static Texture2D BoxDownsample(Texture2D src, int dstWidth, int dstHeight)
        {
            var srcWidth = src.width;
            var factorX = srcWidth / dstWidth;
            var factorY = src.height / dstHeight;
            var sampleCount = factorX * factorY;
            var srcPixels = src.GetPixels();
            var dstPixels = new Color[dstWidth * dstHeight];

            for (var y = 0; y < dstHeight; y++)
            {
                for (var x = 0; x < dstWidth; x++)
                {
                    float pmR = 0f, pmG = 0f, pmB = 0f, sumA = 0f;
                    for (var sy = 0; sy < factorY; sy++)
                    {
                        var rowStart = (y * factorY + sy) * srcWidth + x * factorX;
                        for (var sx = 0; sx < factorX; sx++)
                        {
                            var p = srcPixels[rowStart + sx];
                            pmR += p.r * p.a;
                            pmG += p.g * p.a;
                            pmB += p.b * p.a;
                            sumA += p.a;
                        }
                    }

                    var avgA = sumA / sampleCount;
                    dstPixels[y * dstWidth + x] = avgA > 0.0001f
                        ? new Color(pmR / sampleCount / avgA, pmG / sampleCount / avgA, pmB / sampleCount / avgA, avgA)
                        : new Color(0f, 0f, 0f, 0f);
                }
            }

            var dst = new Texture2D(dstWidth, dstHeight, TextureFormat.RGBA32, false);
            dst.SetPixels(dstPixels);
            dst.Apply();
            return dst;
        }

        public static string StartVideo(int width, int height, int frameRate, string outputFolder)
        {
            if (IsRecording)
            {
                Debug.LogWarning("[OutfitStudio] Already recording");
                return null;
            }

            var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();

            var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieSettings.name = "Outfit Studio Video";
            movieSettings.Enabled = true;
            movieSettings.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
            };
            movieSettings.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = width,
                OutputHeight = height
            };

            var path = GetOutputPath(outputFolder, null); // Recorder appends the extension
            movieSettings.OutputFile = path;

            controllerSettings.AddRecorderSettings(movieSettings);
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRate = frameRate;
            controllerSettings.CapFrameRate = true;

            _recorderController = new RecorderController(controllerSettings);
            _recorderController.PrepareRecording();
            _recorderController.StartRecording();

            return path + ".mp4";
        }

        public static void StopVideo()
        {
            if (_recorderController == null) return;

            if (_recorderController.IsRecording())
            {
                _recorderController.StopRecording();
                Debug.Log("[OutfitStudio] Video recording stopped");
            }

            _recorderController = null;
        }

        private static string ResolveOutputFolder(string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(outputFolder)) outputFolder = DEFAULT_OUTPUT_FOLDER;

            // Relative paths land next to the project (outside Assets so Unity doesn't import them)
            var folder = Path.IsPathRooted(outputFolder)
                ? outputFolder
                : Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, outputFolder);

            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string GetOutputPath(string outputFolder, string extension)
        {
            var folder = ResolveOutputFolder(outputFolder);
            var fileName = $"outfit_{DateTime.Now:yyyyMMdd_HHmmss}";
            var path = Path.Combine(folder, fileName);

            return extension != null ? $"{path}.{extension}" : path;
        }

        public static void RevealInFinder(string path)
        {
            if (!string.IsNullOrEmpty(path)) EditorUtility.RevealInFinder(path);
        }
    }
}
