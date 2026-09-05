using Unity.Profiling;
using System;
using System.Runtime.InteropServices;

namespace Plugins.NativeAudioAnalysis
{

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct AudioAnalysis
    {
        public float amplitude;
        public fixed float bands[NativeMethods.BANDS];
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
