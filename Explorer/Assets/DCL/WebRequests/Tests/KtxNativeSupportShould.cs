using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests.RequestsHub;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine.Networking;

namespace DCL.WebRequests.Tests
{
    // Regression coverage for UNITY-EXPLORER-NQS (#7832): KTX2 conversion is enabled by a remote feature
    // flag, so a machine whose OS cannot open the ktx_unity native plugin must route texture requests to
    // the original URL instead of the media converter.
    public class KtxNativeSupportShould
    {
        private const string ORIGINAL_URL = "https://peer.decentraland.org/content/contents/bafytexture";
        private const string CONVERTER_URL_PREFIX = "https://converter.invalid/convert?url=";
        private const string CONVERTER_URL_TEMPLATE = CONVERTER_URL_PREFIX + "{0}";

        private IDecentralandUrlsSource urlsSource = null!;

        [SetUp]
        public void SetUp()
        {
            KtxNativeSupport.Reset();

            urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.TransformUrl(Arg.Any<string>()).Returns(callInfo => callInfo.Arg<string>());
            urlsSource.Url(DecentralandUrl.MediaConverter).Returns(CONVERTER_URL_TEMPLATE);
        }

        [TearDown]
        public void TearDown()
        {
            KtxNativeSupport.Reset();
        }

        [Test]
        public void RouteToOriginalUrl_WhenKtxNativeUnavailable()
        {
            KtxNativeSupport.probeOverride = static () => false;

            var hub = new RequestHub(urlsSource);
            hub.SetKTXEnabled(true);

            using UnityWebRequest request = CreateTextureRequest(hub);

            Assert.That(request.url, Is.EqualTo(ORIGINAL_URL));
        }

        [Test]
        public void RouteToConverter_WhenKtxNativeAvailable()
        {
            KtxNativeSupport.probeOverride = static () => true;

            var hub = new RequestHub(urlsSource);
            hub.SetKTXEnabled(true);

            using UnityWebRequest request = CreateTextureRequest(hub);

            Assert.That(request.url, Does.StartWith(CONVERTER_URL_PREFIX));
        }

        [Test]
        public void CacheProbeResult_AcrossReads()
        {
            var probeCalls = 0;

            KtxNativeSupport.probeOverride = () =>
            {
                probeCalls++;
                return true;
            };

            Assert.That(KtxNativeSupport.IsSupported, Is.True);
            Assert.That(KtxNativeSupport.IsSupported, Is.True);
            Assert.That(probeCalls, Is.EqualTo(1));
        }

        [Test]
        public void ReportUnsupported_WhenProbeThrowsDllNotFound()
        {
            KtxNativeSupport.probeOverride = static () => throw new DllNotFoundException("ktx_unity");

            Assert.That(KtxNativeSupport.IsSupported, Is.False);
        }

        [Test]
        public void StayUnsupported_AfterRuntimeTrip()
        {
            KtxNativeSupport.probeOverride = static () => true;

            Assert.That(KtxNativeSupport.IsSupported, Is.True);

            KtxNativeSupport.MarkUnsupported();

            Assert.That(KtxNativeSupport.IsSupported, Is.False);
        }

        private static UnityWebRequest CreateTextureRequest(RequestHub hub)
        {
            InitializeRequest<GetTextureArguments, GetTextureWebRequest> initialize = hub.RequestDelegateFor<GetTextureArguments, GetTextureWebRequest>();
            var arguments = new GetTextureArguments(TextureType.Albedo, useKtx: true);
            return initialize(ORIGINAL_URL, ref arguments).UnityWebRequest;
        }
    }
}
