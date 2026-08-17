using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using UnityEditor;
using UnityEngine.Networking;

namespace DCL.WebRequests.Tests
{
    // Transport-security policy invariants: cleartext http is loopback-only where the policy
    // binds — at infra URL-resolution sites (media resolution, sidecar realm root) and on the
    // wire URL of signed requests (the identity auth chain never travels over forbidden
    // cleartext). Unsigned wire URLs pass through the envelope unchanged: their scheme is a
    // module-level decision (local-scene-development fetch permits cleartext to any host).
    // The redirect guard blocks only mid-flight downgrades, and the player-level
    // insecureHttpOption stays AlwaysAllowed so the client-side policy is the single
    // enforcement point.
    public class InsecureSchemePolicyShould
    {
        private const string MEDIA_CONVERTER_TEMPLATE = "https://metamorph-api.decentraland.org/convert?url={0}";

        [TestCase("http://192.168.1.50:8000/api")]
        [TestCase("http://peer.decentraland.org/x")]
        public void PassNonLoopbackHttpThroughUnchangedWhenUnsigned(string url)
        {
            // A local-scene-development scene fetch is an unsigned request whose cleartext
            // scheme is the fetch module's own vetted decision; the envelope must not rewrite it
            Assert.That(UrlAfterEnvelopeInitialization(url), Is.EqualTo(UnityCanonicalUrl(url)));
        }

        [Test]
        public void UpgradeNonLoopbackHttpToHttpsWhenSigned()
        {
            Assert.That(
                UrlAfterEnvelopeInitialization("http://peer.decentraland.org/x", new WebRequestSignInfo(string.Empty)),
                Is.EqualTo(UnityCanonicalUrl("https://peer.decentraland.org/x")));
        }

        [TestCase("http://127.0.0.1:8000/content/contents/bafkreib")]
        [TestCase("http://localhost:8001/x")]
        public void PassLoopbackHttpThroughUnchangedWhenSigned(string url)
        {
            Assert.That(
                UrlAfterEnvelopeInitialization(url, new WebRequestSignInfo(string.Empty)),
                Is.EqualTo(UnityCanonicalUrl(url)));
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
        public void PassNonConvertedTextureUrlThroughUnchangedAtTheWire()
        {
            const string HTTP_URL = "http://textures.example.com/a.png";

            Assert.That(
                TextureUrlAfterEnvelopeInitialization(HTTP_URL, ktxEnabled: false),
                Is.EqualTo(UnityCanonicalUrl(HTTP_URL)));
        }

        [Test]
        public void KeepLoopbackTextureDirectWhenKtxEnabled()
        {
            // The loopback classification the converter reroute keys on must match the wire
            // policy: a loopback origin the policy lets through as cleartext must never be
            // rerouted to the public media converter, which cannot reach it
            const string LOOPBACK_URL = "http://127.0.0.5:8000/a.png";

            Assert.That(
                TextureUrlAfterEnvelopeInitialization(LOOPBACK_URL, ktxEnabled: true),
                Is.EqualTo(UnityCanonicalUrl(LOOPBACK_URL)));
        }

        [TestCase("http://127.0.0.5:8000/x", true)]
        [TestCase("http://localhost:8001/x", true)]
        [TestCase("https://localhost:3000/x", true)]
        [TestCase("http://[::1]:8000/x", true)]
        [TestCase("http://localhost.evil.com/x", false)]
        [TestCase("http://peer.decentraland.org/x", false)]
        [TestCase("file:///tmp/streaming-asset.bin", false)]
        public void ClassifyLocalhostBySchemeAndLoopbackHost(string url, bool isLocalhost)
        {
            Assert.That(WebRequestUtils.IsLocalhost(url), Is.EqualTo(isLocalhost));
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
        public void ClassifyForbiddenCleartext(string url, bool forbidden)
        {
            Assert.That(WebRequestUtils.IsForbiddenCleartext(url), Is.EqualTo(forbidden));
        }

        [TestCase("https://peer.decentraland.org/x", "http://peer.decentraland.org/x", true)]
        [TestCase("http://127.0.0.1:8000/x", "http://192.168.1.50:8000/x", true)]
        [TestCase("http://192.168.1.50:8000/api", "http://192.168.1.50:8000/api", false)]
        [TestCase("http://192.168.1.50:8000/x", "http://10.0.0.7:9000/y", false)]
        [TestCase("https://peer.decentraland.org/x", "https://cdn.decentraland.org/x", false)]
        [TestCase("https://peer.decentraland.org/x", "http://127.0.0.1:8000/x", false)]
        public void ClassifyCleartextDowngradeForTheRedirectGuard(string sentUrl, string finalUrl, bool downgrade)
        {
            Assert.That(WebRequestUtils.IsCleartextDowngrade(sentUrl, finalUrl), Is.EqualTo(downgrade));
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
        private static string UrlAfterEnvelopeInitialization(string url, WebRequestSignInfo? signInfo = null)
        {
            using var envelope = new RequestEnvelope<GenericGetRequest, GenericGetArguments>(
                GenericGetRequest.Initialize,
                new CommonArguments(URLAddress.FromString(url)),
                new GenericGetArguments(),
                CancellationToken.None,
                ReportData.UNSPECIFIED,
                WebRequestHeadersInfo.NewEmpty(),
                signInfo);

            GenericGetRequest request = envelope.InitializedWebRequest(SigningIdentityCache());
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

            GetTextureWebRequest request = envelope.InitializedWebRequest(SigningIdentityCache());
            using UnityWebRequest unityWebRequest = request.UnityWebRequest;
            return unityWebRequest.url;
        }

        /// <summary>
        ///     An identity cache whose identity signs any payload with an empty auth chain, so
        ///     signed envelopes can be initialized without a real wallet.
        /// </summary>
        private static IWeb3IdentityCache SigningIdentityCache()
        {
            IWeb3Identity identity = Substitute.For<IWeb3Identity>();
            identity.Sign(Arg.Any<string>()).Returns(_ => AuthChain.Create());

            IWeb3IdentityCache cache = Substitute.For<IWeb3IdentityCache>();
            cache.Identity.Returns(identity);
            return cache;
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
