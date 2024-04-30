using System;
using UnityEngine.Profiling;

namespace Utility.CustomSamplers
{
    public readonly struct SamplerMeasureScope : IDisposable
    {
        private readonly CustomSampler sampler;

        public SamplerMeasureScope(CustomSampler sampler)
        {
            (this.sampler = sampler).Begin();
        }

        public void Dispose()
        {
            sampler.End();
        }
    }

    public static class MeasureSamplerExtensions
    {
        public static SamplerMeasureScope MeasureScope(this CustomSampler sampler) =>
            new (sampler);
    }
}
