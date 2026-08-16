using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using UnityEditor;
using UnityEngine.Networking;

namespace DCL.WebRequests.Tests
{
    // Transport-security policy invariants: cleartext http is loopback-only; http to any
    // other host is upgraded to https on the wire URL of the built request (after per-request
    // URL composition, so URLs embedded in it as data survive); the player-level
    // insecureHttpOption stays AlwaysAllowed so the client-side policy is the single
    // enforcement point.
    public class InsecureSchemePolicyShould
    {
        private const string MEDIA_CONVERTER_TEMPLATE = "https://metamorph-api.decentraland.org/convert?url={0}";

        [Test]
        public void UpgradeNonLoopbackHttpToHttps()
        {
            Assert.That(
                UrlAfterEnvelopeInitialization("http://peer.decentraland.org/x"),
                Is.EqualTo(UnityCanonicalUrl("https://peer.decentraland.org/x")));
        }

        [TestCase("http://127.0.0.1:8000/content/contents/bafkreib")]
        [TestCase("http://127.0.0.5:8000/x")]
        [TestCase("http://localhost:8001/x")]
        [TestCase("http://[::1]:8000/x")]
        public void PassLoopbackHttpThroughUnchanged(string url)
        {
            Assert.That(UrlAfterEnvelopeInitialization(url), Is.EqualTo(UnityCanonicalUrl(url)));
        }

        [TestCase("https://peer.decentraland.org/lambdas/profiles")]
        [TestCase("file:///tmp/streaming-asset.bin")]
        public void PassNonHttpSchemesThroughUnchanged(string url)
        {
            Assert.That(UrlAfterEnvelopeInitialization(url), Is.EqualTo(UnityCanonicalUrl(url)));
        }

        [Test]
        public void UpgradeNonConvertedTextureUrlAtTheWire()
        {
            Assert.That(
                TextureUrlAfterEnvelopeInitialization("http://textures.example.com/a.png", ktxEnabled: false),
                Is.EqualTo(UnityCanonicalUrl("https://textures.example.com/a.png")));
        }

        [Test]
        public void PreserveHttpOriginEmbeddedInConverterUrl()
        {
            const string HTTP_ORIGIN = "http://textures.example.com/a.png";

            Assert.That(
                TextureUrlAfterEnvelopeInitialization(HTTP_ORIGIN, ktxEnabled: true),
                Is.EqualTo(UnityCanonicalUrl(string.Format(MEDIA_CONVERTER_TEMPLATE, Uri.EscapeDataString(HTTP_ORIGIN)))));
        }

        [TestCase("http://peer.decentraland.org/x", true)]
        [TestCase("http://127.0.0.1:8000/x", false)]
        [TestCase("http://localhost:8001/x", false)]
        [TestCase("http://[::1]:8000/x", false)]
        [TestCase("https://peer.decentraland.org/x", false)]
        [TestCase("file:///tmp/streaming-asset.bin", false)]
        public void ClassifyForbiddenCleartextForTheRedirectGuard(string url, bool forbidden)
        {
            Assert.That(WebRequestUtils.IsForbiddenCleartext(url), Is.EqualTo(forbidden));
        }

        [Test]
        public void KeepPlayerSettingAlwaysAllowed()
        {
            // Unity's own block is inexpressible for this product (all-http or no-http, loopback
            // included); the setting must stay AlwaysAllowed with the client-side policy as the guard.
            Assert.That(PlayerSettings.insecureHttpOption, Is.EqualTo(InsecureHttpOption.AlwaysAllowed));
        }

        /// <summary>
        ///     Builds the request through the same envelope path production uses
        ///     (WebRequestController.SendAsync -> InitializedWebRequest) and returns the URL the
        ///     UnityWebRequest would actually be sent with. The request is never sent.
        /// </summary>
        private static string UrlAfterEnvelopeInitialization(string url)
        {
            using var envelope = new RequestEnvelope<GenericGetRequest, GenericGetArguments>(
                GenericGetRequest.Initialize,
                new CommonArguments(URLAddress.FromString(url)),
                new GenericGetArguments(),
                CancellationToken.None,
                ReportData.UNSPECIFIED,
                WebRequestHeadersInfo.NewEmpty(),
                signInfo: null);

            GenericGetRequest request = envelope.InitializedWebRequest(Substitute.For<IWeb3IdentityCache>());
            using UnityWebRequest unityWebRequest = request.UnityWebRequest;
            return unityWebRequest.url;
        }

        /// <summary>
        ///     Same envelope path, through the texture request's own URL composition (the media
        ///     converter wraps the origin as an escaped query parameter when ktx is enabled).
        /// </summary>
        private static string TextureUrlAfterEnvelopeInitialization(string url, bool ktxEnabled)
        {
            IDecentralandUrlsSource urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(DecentralandUrl.MediaConverter).Returns(MEDIA_CONVERTER_TEMPLATE);

            using var envelope = new RequestEnvelope<GetTextureWebRequest, GetTextureArguments>(
                (string effectiveUrl, ref GetTextureArguments textureArguments) => GetTextureWebRequest.Initialize(effectiveUrl, textureArguments, urlsSource, ktxEnabled),
                new CommonArguments(URLAddress.FromString(url)),
                new GetTextureArguments(TextureType.Albedo),
                CancellationToken.None,
                ReportData.UNSPECIFIED,
                WebRequestHeadersInfo.NewEmpty(),
                signInfo: null);

            GetTextureWebRequest request = envelope.InitializedWebRequest(Substitute.For<IWeb3IdentityCache>());
            using UnityWebRequest unityWebRequest = request.UnityWebRequest;
            return unityWebRequest.url;
        }

        /// <summary>
        ///     UnityWebRequest applies its own URL canonicalization; comparing against the same
        ///     canonicalization keeps the assertions about the scheme policy only.
        /// </summary>
        private static string UnityCanonicalUrl(string url)
        {
            using UnityWebRequest unityWebRequest = UnityWebRequest.Get(url);
            return unityWebRequest.url;
        }
    }
}
