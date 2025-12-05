using Unity.Profiling;
using System;
using System.Runtime.InteropServices;

namespace Plugins.NativeAudioAnalysis
{

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct AudioAnalysis
    {
        // Logarithmic
        public float amplitude;

        // Logarithmic
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = NativeMethods.BANDS)]
        public float[] bands;

        // Will be covered beyond MVP
        // public float spectral_centroid;
        // public float spectral_flux;

        // [MarshalAs(UnmanagedType.I1)] // Rust bool = 1 byte
        // public bool onset;

        // public float bpm;
    }

    public enum AnalysisResultMode : byte
    {
        Raw = 0,
        Logarithmic = 1
    }

    public static class NativeMethods
    {
        public static readonly ProfilerMarker AnalyzeMarker = 
            new ProfilerMarker("AudioAnalysis.AnalyzeBuffer");

        public const int BANDS = 8;
        public const float DEFAULT_AMPLITUDE_GAIN = 5f;
        public const float DEFAULT_BANDS_GAIN = 0.05f;

        // Beyond MVP
        // public const float DEFAULT_ONSET_THRESHOLD = 2.5f;

        private const string LIBRARY_NAME = "audio-analysis";

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal extern static unsafe AudioAnalysis audio_analysis_analyze_audio_buffer(
                float* buffer,
                UIntPtr len,
                float sample_rate,

                AnalysisResultMode mode,

                // gain values are used only in the logarithmic mode
                float amplitude_gain,
                float bands_gain

                // float onset_threshold = DEFAULT_ONSET_THRESHOLD
                );

        public static AudioAnalysis AnalyzeAudioBuffer(
                ReadOnlySpan<float> data, 
                float sampleRate,
                AnalysisResultMode mode = AnalysisResultMode.Logarithmic,
                float amplitudeGain = DEFAULT_AMPLITUDE_GAIN,
                float bandsGain = DEFAULT_BANDS_GAIN
                ) 
        {
            using var _ = AnalyzeMarker.Auto();
            unsafe 
            {
                fixed (float* ptr = data)
                {
                    return NativeMethods.audio_analysis_analyze_audio_buffer(
                            ptr, 
                            (UIntPtr) data.Length, 
                            sampleRate,
                            mode,
                            amplitudeGain,
                            bandsGain
                            );
                }
            }
        }
    }
}
