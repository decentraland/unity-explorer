using System;
using UnityEngine;
using DCL.Diagnostics;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Static utility class for audio format conversions including mono conversion and resampling.
    /// </summary>
    public static class VoiceChatAudioFormatConverter
    {
        public static void ConvertToMono(Span<float> inputData, int channels, Span<float> outputBuffer, int samplesPerChannel)
        {
            if (channels == 1)
            {
                inputData.Slice(0, samplesPerChannel).CopyTo(outputBuffer);
                return;
            }

            for (int i = 0; i < samplesPerChannel; i++)
            {
                float sum = 0f;
                for (int ch = 0; ch < channels; ch++)
                    sum += inputData[(i * channels) + ch];
                outputBuffer[i] = sum / channels;
            }
        }

        public static int ConvertToLiveKitFormat(Span<float> inputData, int inputSampleRate, Span<float> outputBuffer)
        {
            if (inputSampleRate == VoiceChatConstants.LIVEKIT_SAMPLE_RATE)
            {
                inputData.CopyTo(outputBuffer);
                return inputData.Length;
            }

            var targetSamples = (int)((float)inputData.Length * VoiceChatConstants.LIVEKIT_SAMPLE_RATE / inputSampleRate);

            if (targetSamples > outputBuffer.Length)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Output buffer too small for resampling. Need {targetSamples}, got {outputBuffer.Length}");
                return 0;
            }

            VoiceChatAudioResampler.ResampleCubic(inputData, inputSampleRate, outputBuffer.Slice(0, targetSamples), VoiceChatConstants.LIVEKIT_SAMPLE_RATE);
            return targetSamples;
        }

        public static int CalculateLiveKitBufferSize(int inputSamples, int inputSampleRate)
        {
            if (inputSampleRate == VoiceChatConstants.LIVEKIT_SAMPLE_RATE)
                return inputSamples;

            return (int)((float)inputSamples * VoiceChatConstants.LIVEKIT_SAMPLE_RATE / inputSampleRate);
        }
    }
}
