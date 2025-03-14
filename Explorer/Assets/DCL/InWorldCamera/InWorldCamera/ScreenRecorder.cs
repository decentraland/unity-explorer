﻿using DCL.Rendering.GPUInstancing;
using System;
using System.Collections;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.InWorldCamera
{
    public enum RecordingState
    {
        UNKNOWN = 0,
        IDLE = 1,
        CAPTURING = 2,
        SCREENSHOT_READY = 3,
    }

    public sealed class ScreenRecorder : IDisposable
    {
        // Defines the target resolution of the screenshot. Final screenshot is adjusted to this resolution after cropping to the "Rule of three" frame.
        private const int TARGET_FRAME_WIDTH = 1920;
        private const int TARGET_FRAME_HEIGHT = 1080;

        // Relation of screen Canvas to the Scale of the "Rule of three" frame that is an area of the target screenshot. Used in calculation for upscaling screenshot to the target resolution.
        public const float FRAME_SCALE = 0.87f;

        private readonly float targetAspectRatio;
        private readonly RectTransform canvasRectTransform;
        private readonly GPUInstancingService gpuInstancingService;

        private readonly Texture2D screenshot = new (TARGET_FRAME_WIDTH, TARGET_FRAME_HEIGHT, TextureFormat.RGB24, false);

        private RenderTexture originalBaseTargetTexture;

        private ScreenFrameData debugTargetScreenFrame;

        public RecordingState State { get; private set; } = RecordingState.IDLE;

        public ScreenRecorder(RectTransform canvasRectTransform, GPUInstancingService gpuInstancingService)
        {
            targetAspectRatio = (float)TARGET_FRAME_WIDTH / TARGET_FRAME_HEIGHT;
            Debug.Assert(targetAspectRatio != 0, "Target aspect ratio cannot be zero");

            this.canvasRectTransform = canvasRectTransform;
            this.gpuInstancingService = gpuInstancingService;
        }

        public void Dispose()
        {
            if(screenshot != null)
                Object.Destroy(screenshot);

            if (originalBaseTargetTexture != null)
                RenderTexture.ReleaseTemporary(originalBaseTargetTexture);
        }

        public IEnumerator CaptureScreenshot()
        {
            State = RecordingState.CAPTURING;

            yield return GameObjectExtensions.WAIT_FOR_END_OF_FRAME; // for UI to appear on screenshot. Converting to UniTask didn't work :(

            ScreenFrameData currentScreenFrame = CalculateCurrentScreenFrame();
            float targetRescale = CalculateScaleFactorToTargetSize(currentScreenFrame);
            int roundedUpscale = Mathf.CeilToInt(targetRescale);

            ScreenFrameData rescaledScreenFrame = CalculateRoundRescaledScreenFrame(currentScreenFrame, roundedUpscale);

            Texture2D screenshotTexture = ScreenCapture.CaptureScreenshotAsTexture(roundedUpscale); // upscaled Screen Frame resolution
            screenshotTexture = CropTexture2D(screenshotTexture, rescaledScreenFrame.CalculateFrameCorners(), rescaledScreenFrame.FrameWidthInt, rescaledScreenFrame.FrameHeightInt);
            ResizeTexture2D(screenshotTexture);

            Object.Destroy(screenshotTexture);

            State = RecordingState.SCREENSHOT_READY;
        }

        public Texture2D GetScreenshotAndReset()
        {
            if (State != RecordingState.SCREENSHOT_READY)
                return null;

            State = RecordingState.IDLE;
            return screenshot;
        }

        private static Texture2D CropTexture2D(Texture2D texture, Vector2Int startCorner, int width, int height)
        {
            Color[] pixels = texture.GetPixels(startCorner.x, startCorner.y, width, height);

            var result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.SetPixels(pixels);
            result.Apply();

            return result;
        }

        private void ResizeTexture2D(Texture originalTexture)
        {
            var renderTexture = RenderTexture.GetTemporary(TARGET_FRAME_WIDTH, TARGET_FRAME_HEIGHT, 0);
            RenderTexture.active = renderTexture;

            // Copy and scale the original texture into the RenderTexture
            Graphics.Blit(originalTexture, renderTexture);

            // Read the pixel data from the RenderTexture into the Texture2D
            screenshot.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            screenshot.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(renderTexture);
        }

        private ScreenFrameData CalculateCurrentScreenFrame()
        {
            var screenFrameData = new ScreenFrameData
            {
                ScreenWidth = canvasRectTransform.rect.width * canvasRectTransform.lossyScale.x,
                ScreenHeight = canvasRectTransform.rect.height * canvasRectTransform.lossyScale.y,
            };

            // Adjust current by smallest side
            if (screenFrameData.ScreenAspectRatio > targetAspectRatio) // Height is the limiting dimension, so scaling width based on it
            {
                screenFrameData.FrameHeight = screenFrameData.ScreenHeight * FRAME_SCALE;
                screenFrameData.FrameWidth = screenFrameData.FrameHeight * targetAspectRatio;
            }
            else // Width is the limiting dimension, so scaling height based on it
            {
                screenFrameData.FrameWidth = screenFrameData.ScreenWidth * FRAME_SCALE;
                screenFrameData.FrameHeight = screenFrameData.FrameWidth / targetAspectRatio;
            }

            return screenFrameData;
        }

        private static float CalculateScaleFactorToTargetSize(ScreenFrameData currentScreenFrameData)
        {
            var screenFrameData = new ScreenFrameData();

            float upscaleFrameWidth = TARGET_FRAME_WIDTH / currentScreenFrameData.FrameWidth;
            float upscaleFrameHeight = TARGET_FRAME_HEIGHT / currentScreenFrameData.FrameHeight;
            Debug.Assert(Math.Abs(upscaleFrameWidth - upscaleFrameHeight) < 0.01f, "Screenshot upscale factors should be the same");

            screenFrameData.ScreenWidth = currentScreenFrameData.ScreenWidth * upscaleFrameWidth;
            screenFrameData.ScreenHeight = currentScreenFrameData.ScreenHeight * upscaleFrameHeight;
            screenFrameData.FrameWidth = currentScreenFrameData.FrameWidth * upscaleFrameWidth;
            screenFrameData.FrameHeight = currentScreenFrameData.FrameHeight * upscaleFrameHeight;
            Debug.Assert(Math.Abs(screenFrameData.FrameWidth - TARGET_FRAME_WIDTH) < 0.1f, "Calculated screenshot width should be the same as target width");
            Debug.Assert(Math.Abs(screenFrameData.FrameHeight - TARGET_FRAME_HEIGHT) < 0.1f, "Calculated screenshot height should be the same as target height");

            return upscaleFrameWidth;
        }

        private static ScreenFrameData CalculateRoundRescaledScreenFrame(ScreenFrameData rescalingScreenFrame, int roundedRescaleFactor) =>
            new ()
            {
                FrameWidth = rescalingScreenFrame.FrameWidth * roundedRescaleFactor,
                FrameHeight = rescalingScreenFrame.FrameHeight * roundedRescaleFactor,
                ScreenWidth = rescalingScreenFrame.ScreenWidth * roundedRescaleFactor,
                ScreenHeight = rescalingScreenFrame.ScreenHeight * roundedRescaleFactor,
            };
    }
}
