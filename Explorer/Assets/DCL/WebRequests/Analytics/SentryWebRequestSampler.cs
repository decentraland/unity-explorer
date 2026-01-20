using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.ThreadSafePool;
using Sentry;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.WebRequests.Analytics
{
    public class SentryWebRequestSampler : SentrySampler.ISampler
    {
        [Serializable]
        public struct SentryTransactionConfiguration
        {
            public DecentralandUrl url;

            [Range(0, 0.1f)]
            public float samplingRate;
        }

        /// <summary>
        ///     Pooled and reused
        /// </summary>
        public class SamplingContext
        {
            public string Url;
        }

        private const string SAMPLE_KEY = nameof(SentryWebRequestSampler);

        private readonly ThreadSafeDictionaryPool<string, object> samplingContextPool;

        private readonly IReadOnlyList<SentryTransactionConfiguration> urlsToSample;
        private readonly IDecentralandUrlsSource urlsSource;

        public SentryWebRequestSampler(IDecentralandUrlsSource urlsSource, IReadOnlyList<SentryTransactionConfiguration> urlsToSample, int maxConcurrency)
        {
            samplingContextPool = new ThreadSafeDictionaryPool<string, object>(1, maxConcurrency);
            this.urlsToSample = urlsToSample;
            this.urlsSource = urlsSource;
        }

        public double? Execute(TransactionSamplingContext transactionSamplingContext)
        {
            IReadOnlyDictionary<string, object?> context = transactionSamplingContext.CustomSamplingContext;
            double? result = null;

            if (context.TryGetValue(SAMPLE_KEY, out object? ctx) && ctx is SamplingContext samplingContext)
            {
                foreach (SentryTransactionConfiguration configuration in urlsToSample)
                {
                    // TODO sanitize from string arguments like {{0}}
                    if (samplingContext.Url.StartsWith(urlsSource.Url(configuration.url)))
                        return configuration.samplingRate;
                }

                samplingContextPool.Release((Dictionary<string, object>)context);
            }

            return result;
        }
    }

    public class SentryWebRequestHandler { }
}
