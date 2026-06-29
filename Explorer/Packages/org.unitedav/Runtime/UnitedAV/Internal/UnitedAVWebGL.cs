// SPDX-License-Identifier: Apache-2.0
// P/Invoke layer over the Unity WebGL backend (Plugins/WebGL/UnitedAV.jslib). On
// WebGL the native FFmpeg plugin is unavailable; this drives the browser's own
// HTMLVideoElement and uploads frames into Unity GL textures. Counterpart of
// UnitedAVNative.cs (which targets the native "UnitedAV" library elsewhere).
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace UnitedAV.Internal
{
    internal static class UnitedAVWebGL
    {
        [DllImport("__Internal")] public static extern int   UAV_Web_Create(string url);
        [DllImport("__Internal")] public static extern void  UAV_Web_Play(int h);
        [DllImport("__Internal")] public static extern void  UAV_Web_Pause(int h);
        [DllImport("__Internal")] public static extern void  UAV_Web_Seek(int h, double t);
        [DllImport("__Internal")] public static extern void  UAV_Web_SetLooping(int h, int on);
        [DllImport("__Internal")] public static extern void  UAV_Web_SetVolume(int h, float vol);
        [DllImport("__Internal")] public static extern void  UAV_Web_SetMuted(int h, int on);
        [DllImport("__Internal")] public static extern int   UAV_Web_GetWidth(int h);
        [DllImport("__Internal")] public static extern int   UAV_Web_GetHeight(int h);
        [DllImport("__Internal")] public static extern double UAV_Web_GetDuration(int h);
        [DllImport("__Internal")] public static extern double UAV_Web_GetPosition(int h);
        [DllImport("__Internal")] public static extern int   UAV_Web_HasVideo(int h);
        [DllImport("__Internal")] public static extern int   UAV_Web_GetState(int h);
        [DllImport("__Internal")] public static extern int   UAV_Web_HasNewFrame(int h);
        [DllImport("__Internal")] public static extern int   UAV_Web_UploadTexture(int h, int glTex);
        // WebGPU upload path (design-only; only meaningful in a Unity WebGPU player
        // build). wgpuTex is the GPUTexture handle from Texture.GetNativeTexturePtr().
        [DllImport("__Internal")] public static extern int   UAV_Web_UploadTextureWGPU(int h, int wgpuTex);
        [DllImport("__Internal")] public static extern void  UAV_Web_Destroy(int h);

        // Mirrors the native C ABI UAVState.
        public enum State { Idle = 0, Opening = 1, Ready = 2, Playing = 3, Paused = 4, Buffering = 5, Finished = 6, Error = 7 }
    }
}
#endif
