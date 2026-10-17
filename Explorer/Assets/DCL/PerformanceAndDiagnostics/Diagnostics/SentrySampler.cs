using Sentry;
using System.Collections.Generic;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Container to provide custom logic for sampling
    /// </summary>
    public class SentrySampler
    {
        private readonly List<ISampler> samplers = new ();

        public void Add(ISampler sampler)
        {
            samplers.Add(sampler);
        }

        public double? Execute(TransactionSamplingContext transactionSamplingContext)
        {
            foreach (ISampler sampler in samplers)
            {
                double? sampleRate = sampler.Execute(transactionSamplingContext);

                if (sampleRate != null)
                    return sampleRate;
            }

            return null;
        }

        public interface ISampler
        {
            public double? Execute(TransactionSamplingContext transactionSamplingContext);
        }
    }
}
