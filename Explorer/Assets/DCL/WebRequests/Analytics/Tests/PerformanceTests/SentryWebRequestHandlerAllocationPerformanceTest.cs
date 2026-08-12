using DCL.Multiplayer.Connections.DecentralandUrls;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics.PerformanceTests
{
    /// <summary>
    ///     Guards the download-hot-path pre-check in <see cref="SentryWebRequestHandler.OnRequestStarted{T,TArgs}" />:
    ///     it now consults <see cref="SentryWebRequestSampler.IsWhitelisted" /> and returns before allocating a
    ///     <c>TransactionContext</c>, renting a pooled sampling-context dictionary and inserting an unsampled
    ///     transaction into the SDK map. Only URLs a configured template matches (which could actually be sampled)
    ///     pay that cost; the non-whitelisted CDN-download majority is skipped. The pre-check reuses the sampler's
    ///     own template matcher, so this pins that a whitelisted URL is admitted and a non-whitelisted one is not.
    /// </summary>
    [Category("Performance")]
    public class SentryWebRequestHandlerAllocationPerformanceTest
    {
        private const string WHITELISTED_TEMPLATE = "https://sampled.decentraland.org/";
        private const string WHITELISTED_URL = "https://sampled.decentraland.org/content/entities/active";
        private const string NON_WHITELISTED_URL = "https://peer.decentraland.org/content/contents/QmTextureHashAbc123";

        private const DecentralandUrl TEMPLATE_URL = DecentralandUrl.Host;

        private static SentryWebRequestSampler BuildSampler()
        {
            var urlsSource = new FakeUrlsSource(WHITELISTED_TEMPLATE);

            var configs = new List<SentryWebRequestSampler.SentryTransactionConfiguration>
            {
                new () { url = TEMPLATE_URL, samplingRate = 1f },
            };

            return new SentryWebRequestSampler(urlsSource, configs, maxConcurrency: 32);
        }

        [Test]
        public void NonWhitelistedUrlsAreSkippedBeforeAllocatingATransaction()
        {
            SentryWebRequestSampler sampler = BuildSampler();

            Assert.That(sampler.IsWhitelisted(WHITELISTED_URL), Is.True,
                "a URL matching a configured template must be whitelisted so its transaction is still sampled");

            Assert.That(sampler.IsWhitelisted(NON_WHITELISTED_URL), Is.False,
                "a non-whitelisted CDN download must be rejected by the pre-check, so OnRequestStarted returns before allocating a transaction");
        }

        private sealed class FakeUrlsSource : IDecentralandUrlsSource
        {
            private readonly string template;

            public FakeUrlsSource(string template)
            {
                this.template = template;
            }

            public string Url(DecentralandUrl decentralandUrl) => template;

            public string Probe(DecentralandUrl decentralandUrl) => template;

            public string TransformUrl(string originalUrl) => originalUrl;

            public string GetOriginalUrl(string url) => url;

            public string GetHostnameForFeatureFlag() => template;
        }
    }
}
