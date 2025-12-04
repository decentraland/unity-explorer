using System;
using System.Runtime.InteropServices;

namespace Plugins.NativeAudioAnalysis
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AudioAnalysis
    {
        public float amplitude;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public float[] bands;

        public float spectral_centroid;
        public float spectral_flux;

        [MarshalAs(UnmanagedType.I1)] // Rust bool = 1 byte
        public bool onset;

        public float bpm;
    }

    public static class NativeMethods
    {
        private const string LIBRARY_NAME = "audio-analysis";

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal extern static unsafe bool audio_analysis_analyze_audio_buffer(
            float* buffer,
            UIntPtr len,
            float sample_rate,
            float onset_threshold
        );
    }
}
