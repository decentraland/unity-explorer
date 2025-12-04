using System;
using System.Runtime.InteropServices;

namespace Plugins.NativeAudioAnalysis
{

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct AudioAnalysis
    {
        public float amplitude;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = NativeMethods.BANDS)]
        public float[] bands;

        // Will be covered beyond MVP
        // public float spectral_centroid;
        // public float spectral_flux;

        // [MarshalAs(UnmanagedType.I1)] // Rust bool = 1 byte
        // public bool onset;

        // public float bpm;
    }

    public static class NativeMethods
    {
        public const int BANDS = 8;
        public const float DEFAULT_ONSET_THRESHOLD = 2.5f;

        private const string LIBRARY_NAME = "audio-analysis";

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal extern static unsafe AudioAnalysis audio_analysis_analyze_audio_buffer(
            float* buffer,
            UIntPtr len,
            float sample_rate
            // float onset_threshold = DEFAULT_ONSET_THRESHOLD
        );

        public static AudioAnalysis AnalyzeAudioBuffer(float[] data, float sampleRate) 
        {
            unsafe 
            {
                fixed (float* ptr = data)
                {
                    return NativeMethods.audio_analysis_analyze_audio_buffer(ptr, (UIntPtr) data.Length, sampleRate);
                }
            }
        }
    }
}
