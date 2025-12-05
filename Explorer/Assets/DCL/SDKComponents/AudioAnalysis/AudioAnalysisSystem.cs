using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.Prioritization.Components;
using SceneRunner.Scene;
using Plugins.NativeAudioAnalysis;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.SDKComponents.AudioSources
{
    [UpdateInGroup(typeof(SDKAudioSourceGroup))]
    [LogCategory(ReportCategory.SDK_AUDIO_ANALYSIS)]
    [ThrottlingEnabled]
    public partial class AudioAnalysisSystem : BaseUnityLoopSystem
    {
        public const float DEFAULT_AMPLITUDE_GAIN = 5f;
        public const float DEFAULT_BANDS_GAIN = 0.05f;

        private readonly IPerformanceBudget frameTimeBudgetProvider;

        internal AudioAnalysisSystem(World world, IPerformanceBudget frameTimeBudgetProvider) : base(world)
        {
            this.frameTimeBudgetProvider = frameTimeBudgetProvider;
        }

        protected override void Update(float t)
        {
            HandleAudioAnalysisComponentQuery(World);
        }

        [Query]
        private void HandleAudioAnalysisComponent(ref AudioSourceComponent audioSourceComponent, ref PBAudioAnalysis sdkComponent)
        {
            if (!frameTimeBudgetProvider.TrySpendBudget()) return;

            ThreadSafeLastAudioFrameReadFilter output = null!;
            if (audioSourceComponent.TryAttachLastAudioFrameReadFilterOrUseExisting(out output) == false) 
            {
                ReportHub.LogError(GetReportCategory(), "Cannot attach LastAudioFrameReadFilter");
                return;
            }

            if (output!.TryConsume(out float[]? data, out int outChannels, out int outSampleRate)) 
            {
                AnalysisResultMode mode = sdkComponent.Mode switch
                {
                    PBAudioAnalysisMode.ModeRaw => AnalysisResultMode.Raw,
                    PBAudioAnalysisMode.ModeLogarithmic => AnalysisResultMode.Logarithmic,
                };
                float amplitudeGain = sdkComponent.HasAmplitudeGain ? sdkComponent.AmplitudeGain : DEFAULT_AMPLITUDE_GAIN; 
                float bandsGain = sdkComponent.HasBandsGain ? sdkComponent.BandsGain : DEFAULT_BANDS_GAIN; 

                unsafe 
                {
                    AudioAnalysis result = NativeMethods.AnalyzeAudioBuffer(data!, outSampleRate, mode, amplitudeGain, bandsGain);

                    sdkComponent.Amplitude = result.amplitude;

                    sdkComponent.Band0 = result.bands[0];
                    sdkComponent.Band1 = result.bands[1];
                    sdkComponent.Band2 = result.bands[2];
                    sdkComponent.Band3 = result.bands[3];
                    sdkComponent.Band4 = result.bands[4];
                    sdkComponent.Band5 = result.bands[5];
                    sdkComponent.Band6 = result.bands[6];
                    sdkComponent.Band7 = result.bands[7];
                }
            }
        }
    }
}
