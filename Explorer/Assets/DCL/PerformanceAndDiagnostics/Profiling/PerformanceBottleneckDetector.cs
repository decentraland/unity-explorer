using UnityEngine;

namespace DCL.Profiling
{
    public class PerformanceBottleneckDetector
    {
        public enum PerformanceBottleneck
        {
            Indeterminate, // Cannot be determined
            PresentLimited, // Limited by presentation (vsync or framerate cap)
            Cpu, // Limited by CPU (main and/or render thread)
            Gpu, // Limited by GPU
            Balanced, // Limited by both CPU and GPU, i.e. well balanced
        }

        private const float K_NEAR_FULL_FRAME_TIME_THRESHOLD_PERCENT = 0.2f;

        public readonly bool IsFrameTimingSupported = FrameTimingManager.IsFeatureEnabled();

        private readonly FrameTiming[] frameTimings = new FrameTiming[1];
        public FrameTiming FrameTiming => frameTimings[0];

        public bool TryCapture()
        {
            FrameTimingManager.CaptureFrameTimings();
            uint numFrames = FrameTimingManager.GetLatestTimings((uint)frameTimings.Length, frameTimings);
            return numFrames > 0;
        }

        public PerformanceBottleneck DetermineBottleneck() =>
            DetermineBottleneck(frameTimings[0]);

        private static PerformanceBottleneck DetermineBottleneck(FrameTiming timing)
        {
            if (timing.gpuFrameTime == 0)
                return PerformanceBottleneck.Indeterminate;

            float fullFrameTime = Mathf.Max((float)timing.cpuFrameTime, (float)timing.gpuFrameTime);
            double fullFrameTimeWithMargin = (1.0 - K_NEAR_FULL_FRAME_TIME_THRESHOLD_PERCENT) * fullFrameTime;

            // GPU time is close to frame time, CPU times are not
            if (timing.gpuFrameTime > fullFrameTimeWithMargin &&
                timing.cpuMainThreadFrameTime < fullFrameTimeWithMargin &&
                timing.cpuRenderThreadFrameTime < fullFrameTimeWithMargin)
                return PerformanceBottleneck.Gpu;

            // One of the CPU times is close to frame time, GPU is not
            if (timing.gpuFrameTime < fullFrameTimeWithMargin &&
                (timing.cpuMainThreadFrameTime > fullFrameTimeWithMargin || timing.cpuRenderThreadFrameTime > fullFrameTimeWithMargin))
                return PerformanceBottleneck.Cpu;

            // Check if we're limited by vsync or target frame rate
            if (timing.syncInterval > 0)
            {
                // None of the times are close to frame time
                if (timing.gpuFrameTime < fullFrameTimeWithMargin &&
                    timing.cpuMainThreadFrameTime < fullFrameTimeWithMargin &&
                    timing.cpuRenderThreadFrameTime < fullFrameTimeWithMargin)
                    return PerformanceBottleneck.PresentLimited;
            }

            return PerformanceBottleneck.Balanced;
        }
    }
}
