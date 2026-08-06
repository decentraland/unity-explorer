using CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.DecentralandUrls;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.PerformanceTesting;
using UnityEngine.Networking;

namespace DCL.WebRequests.Analytics.PerformanceTests
{
    /// <summary>
    ///     Guards the download-hot-path allocation fix in <see cref="SentryWebRequestHandler.OnRequestStarted{T,TArgs}" />:
    ///     before the fix, EVERY non-file web request (every CDN texture / wearable / AssetBundle / GLB download)
    ///     unconditionally allocated a <c>TransactionContext</c>, rented a pooled sampling-context dictionary and
    ///     inserted an unsampled transaction into the SDK map — only for the sampler to reject it (rate 0) and the
    ///     handler to discard it with no trace-header injection. The fix adds a cheap <c>IsWhitelisted</c> pre-check
    ///     so only URLs a configured template matches pay that cost.
    ///     <para>
    ///     The metric is bytes allocated on the current thread across N identical <c>OnRequestStarted</c> calls
    ///     (<see cref="GC.GetAllocatedBytesForCurrentThread" />), measured for a whitelisted URL (still allocates a
    ///     transaction — the baseline) and a non-whitelisted URL (must not). The comparison is self-calibrating so it
    ///     needs no absolute byte threshold: on the pre-fix build the non-whitelisted path allocates as much as the
    ///     whitelisted one and the >=95%-reduction assertion FAILS; on the fixed build it is ~0 and the assertion
    ///     passes. Requiring the whitelisted baseline to be > 0 simultaneously falsifies an over-aggressive early-out
    ///     that would wrongly skip whitelisted (sampled) URLs.
    ///     </para>
    /// </summary>
    [Category("Performance")]
    public class SentryWebRequestHandlerAllocationPerformanceTest
    {
        // Template registered as whitelisted; the sampler matches a URL when it StartsWith the template.
        private const string WHITELISTED_TEMPLATE = "https://sampled.decentraland.org/";
        private const string WHITELISTED_URL = "https://sampled.decentraland.org/content/entities/active";

        // A representative CDN download that no template matches — the majority case the fix targets.
        private const string NON_WHITELISTED_URL = "https://peer.decentraland.org/content/contents/QmTextureHashAbc123";

        private const DecentralandUrl TEMPLATE_URL = DecentralandUrl.Host;
        private const int N = 1000;

        private SentryWebRequestHandler handler = null!;
        private readonly List<UnityWebRequest> tracked = new ();

        [SetUp]
        public void SetUp()
        {
            // Hand-rolled fake (not NSubstitute): a mock records every Url() call and would allocate on the hot
            // path we are measuring, hiding the real signal. This returns the template with zero allocation.
            var urlsSource = new FakeUrlsSource(WHITELISTED_TEMPLATE);

            var configs = new List<SentryWebRequestSampler.SentryTransactionConfiguration>
            {
                new () { url = TEMPLATE_URL, samplingRate = 1f },
            };

            var sampler = new SentryWebRequestSampler(urlsSource, configs, maxConcurrency: 32);
            handler = new SentryWebRequestHandler(sampler);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityWebRequest uwr in tracked)
                if (uwr != null)
                    uwr.Dispose();

            tracked.Clear();
        }

        [Test, Performance]
        public void OnRequestStarted_NonWhitelisted_AllocatesFarLessThanWhitelisted()
        {
            // Verify the allocation counter is live on this runtime; otherwise the comparison is meaningless.
            long p0 = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[4096];
            long p1 = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(probe);

            if (p1 - p0 <= 0)
                Assert.Inconclusive("GC.GetAllocatedBytesForCurrentThread does not report allocations on this runtime.");

            // Baseline: whitelisted URLs still start a transaction, so they allocate (>0).
            long whitelistedBytes = MeasureAllocatedBytes(WHITELISTED_URL, N);

            // Hot path under test: non-whitelisted URLs must short-circuit before any allocation.
            long nonWhitelistedBytes = MeasureAllocatedBytes(NON_WHITELISTED_URL, N);

            Measure.Custom(new SampleGroup("Whitelisted.AllocatedBytes", SampleUnit.Byte), whitelistedBytes);
            Measure.Custom(new SampleGroup("NonWhitelisted.AllocatedBytes", SampleUnit.Byte), nonWhitelistedBytes);

            Assert.That(whitelistedBytes, Is.GreaterThan(0L),
                "Whitelisted (sampled) requests must still start a transaction and allocate. Zero here means the "
                + "IsWhitelisted pre-check is over-aggressive and is skipping URLs that should be sampled.");

            // >=95% reduction on the non-whitelisted path. Pre-fix this path allocated a TransactionContext per call,
            // making the two figures comparable (ratio ~1) and failing this bound.
            Assert.That(nonWhitelistedBytes, Is.LessThanOrEqualTo(whitelistedBytes / 20L),
                $"Non-whitelisted OnRequestStarted allocated {nonWhitelistedBytes} B over {N} calls vs a whitelisted "
                + $"baseline of {whitelistedBytes} B; expected <=5% (>=95% reduction). The per-request Sentry "
                + "TransactionContext allocation is not being skipped.");
        }

        /// <summary>
        ///     Runs <paramref name="iterations" /> identical <see cref="SentryWebRequestHandler.OnRequestStarted{T,TArgs}" />
        ///     calls against a single reused request/envelope and returns the current thread's allocated-byte delta.
        ///     A single warm-up call primes the template cache, the sampling-context pool and the JIT so the measured
        ///     window reflects steady state.
        /// </summary>
        private long MeasureAllocatedBytes(string url, int iterations)
        {
            var uwr = new UnityWebRequest(url);
            tracked.Add(uwr);

            var request = new FakeTypedWebRequest(uwr);
            RequestEnvelope<FakeTypedWebRequest, FakeArgs> envelope = BuildEnvelope(url);
            DateTime startedAt = DateTime.UtcNow;

            handler.OnRequestStarted(in envelope, request, startedAt);

            long before = GC.GetAllocatedBytesForCurrentThread();

            for (var i = 0; i < iterations; i++)
                handler.OnRequestStarted(in envelope, request, startedAt);

            long after = GC.GetAllocatedBytesForCurrentThread();
            return after - before;
        }

        private static RequestEnvelope<FakeTypedWebRequest, FakeArgs> BuildEnvelope(string url) =>
            new (
                null, // initializeRequest — never invoked by OnRequestStarted
                new CommonArguments(URLAddress.FromString(url)),
                default(FakeArgs),
                CancellationToken.None,
                default, // ReportData
                default(WebRequestHeadersInfo),
                null); // signInfo

        private struct FakeArgs { }

        private readonly struct FakeTypedWebRequest : ITypedWebRequest
        {
            public UnityWebRequest UnityWebRequest { get; }

            public bool Idempotent => true;

            public FakeTypedWebRequest(UnityWebRequest unityWebRequest)
            {
                UnityWebRequest = unityWebRequest;
            }
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