using DCL.Browser.DecentralandUrls;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility;
using ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using Utility;

// ReSharper disable once CheckNamespace
namespace DCL.Browser
{
    public class GatewayUrlsSource : DecentralandUrlsSource
    {
        private const string GATEWAY_SUBDOMAIN = "gateway";
        private const int HTTPS_PREFIX_LENGTH = 8; // "https://".Length

        // Today is excluded on purpose: its org/today host mixture is pinned at construction and would not survive
        // the rewrite. Custom is included, but a custom deployment only routes through a gateway when the
        // "use-gateway" flag its own feature-flags backend serves says so, so opting in stays that deployment's call.
        private static readonly DecentralandEnvironment[] SUPPORTED_ENVS = { DecentralandEnvironment.Org, DecentralandEnvironment.Zone, DecentralandEnvironment.Custom };

        private static readonly HashSet<DecentralandUrl> SUPPORTED_URLS = new (EnumUtils.GetEqualityComparer<DecentralandUrl>())
        {
            // Places API
            DecentralandUrl.ApiPlaces,
            DecentralandUrl.ApiWorlds,
            DecentralandUrl.ApiDestinations,
            DecentralandUrl.POI,
            DecentralandUrl.Map,
            DecentralandUrl.ContentModerationReport,
            DecentralandUrl.ApiEvents,

            DecentralandUrl.ApiAuth,
            DecentralandUrl.ApiChunks,

            // LiveKit rooms providers require signed fetch
            DecentralandUrl.GateKeeperSceneAdapter,
            DecentralandUrl.LocalGateKeeperSceneAdapter,
            DecentralandUrl.ChatAdapter,
            DecentralandUrl.GatekeeperStatus,
            DecentralandUrl.BannedUsers,
            DecentralandUrl.RemotePeers,
            DecentralandUrl.RemotePeersWorld,
            DecentralandUrl.ArchipelagoStatus,
            DecentralandUrl.ArchipelagoHotScenes,

            // Content Servers
            DecentralandUrl.AssetBundlesCDN,
            DecentralandUrl.LodAssetBundlesCDN,
            DecentralandUrl.WorldContentServer,

            DecentralandUrl.Genesis,
            DecentralandUrl.Badges,

            // Requires signed fetch
            DecentralandUrl.CameraReelImages,
            DecentralandUrl.CameraReelPlaces,
            DecentralandUrl.CameraReelUsers,

            DecentralandUrl.AssetBundleRegistry,
            DecentralandUrl.AssetBundleRegistryVersion,
            DecentralandUrl.Profiles,
            DecentralandUrl.ProfilesMetadata,

            DecentralandUrl.MediaConverter,

            DecentralandUrl.MarketplaceCredits,
            DecentralandUrl.Notifications, // Notification partially required signed fetch

            // Social
            DecentralandUrl.CommunityThumbnail,

            // The following requires signed fetch
            DecentralandUrl.Communities,
            DecentralandUrl.CommunitiesV2,
            DecentralandUrl.Members,
            DecentralandUrl.MembersV2,
            DecentralandUrl.ActiveCommunityVoiceChats,
        };

        /// <summary>
        ///     Routing via the Gateway enables multiplexing over HTTP/2 even for resources originated from the backend
        ///     services. Subdomains, not whole hosts: they are composed against this client's base domain.
        /// </summary>
        private static readonly string[] SUPPORTED_SUBDOMAINS_OF_NON_CLIENT_ORIGIN =
        {
            "profile-images",
        };

        private readonly bool envSupported;

        // The origin --gateway named, already normalized by TryNormalizeGatewayPrefix, or null to use
        // gateway.{BaseDomain}. Naming one is itself the opt-in, so it also stands in for the flag.
        private readonly string? cliGatewayPrefix;
        private readonly List<string>? resolvedNonClientHosts;
        private readonly string? gatewayPrefix;
        private readonly string? domainSuffix;

        private bool enabled => envSupported && (cliGatewayPrefix != null || FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.USE_GATEWAY));

        public GatewayUrlsSource(
            DecentralandEnvironment environment,
            IRealmData realmData,
            ILaunchMode launchMode,
            GatekeeperMode gatekeeperMode = GatekeeperMode.Org,
            string customGatekeeperUrl = "",
            string? cliGatekeeperUrl = null,
            string? cliOptimizedAssetsUrl = null,
            string? customBaseDomain = null,
            bool abgenPipelineForced = false,
            string? cliGatewayPrefix = null)
            : base(environment, realmData, launchMode, gatekeeperMode, customGatekeeperUrl, cliGatekeeperUrl, cliOptimizedAssetsUrl, customBaseDomain, abgenPipelineForced)
        {
            this.cliGatewayPrefix = cliGatewayPrefix;
            envSupported = SUPPORTED_ENVS.Contains(environment);

            if (envSupported)
            {
                resolvedNonClientHosts = new List<string>(SUPPORTED_SUBDOMAINS_OF_NON_CLIENT_ORIGIN.Length);

                foreach (string subdomain in SUPPORTED_SUBDOMAINS_OF_NON_CLIENT_ORIGIN)
                    resolvedNonClientHosts.Add($"{subdomain}.{BaseDomain}");

                gatewayPrefix = cliGatewayPrefix ?? $"https://{GATEWAY_SUBDOMAIN}.{BaseDomain}/";
                domainSuffix = $".{BaseDomain}";
            }
        }

        public new static GatewayUrlsSource CreateForTest(DecentralandEnvironment environment, ILaunchMode launchMode) =>
            new (environment, new IRealmData.Fake(), launchMode);

        public new static GatewayUrlsSource CreateForTest(string customBaseDomain, ILaunchMode launchMode) =>
            new (DecentralandEnvironment.Custom, new IRealmData.Fake(), launchMode, customBaseDomain: customBaseDomain);

        /// <summary>
        ///     <paramref name="gatewayUrl" /> reduced to the prefix every gatewayed url is built from, or false when
        ///     it is not an absolute http(s) url with a host and without query or fragment. The caller reports and
        ///     abandons the launch rather than coercing a value into something plausible: this arg forces routing on,
        ///     so a misread one sends every supported service to a host nobody named.
        /// </summary>
        public static bool TryNormalizeGatewayPrefix(string gatewayUrl, out string prefix)
        {
            prefix = string.Empty;

            if (!Uri.TryCreate(gatewayUrl.Trim(), UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || string.IsNullOrEmpty(uri.Host)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
                return false;

            prefix = uri.ToString().TrimEnd('/') + "/";
            return true;
        }

        /// <summary>
        ///     Transforms a 3rd party URL, DecentralandURLs are already transformed by <see cref="RawUrl" />
        /// </summary>
        public override string TransformUrl(string originalUrl)
        {
            if (!enabled || resolvedNonClientHosts == null || originalUrl.Length <= HTTPS_PREFIX_LENGTH)
                return originalUrl;

            ReadOnlySpan<char> urlAfterPrefix = originalUrl.AsSpan(HTTPS_PREFIX_LENGTH);

            foreach (string host in resolvedNonClientHosts)
            {
                if (!urlAfterPrefix.StartsWith(host.AsSpan(), StringComparison.OrdinalIgnoreCase))
                    continue;

                if (urlAfterPrefix.Length == host.Length || urlAfterPrefix[host.Length] == '/')
                    return TransformToGateway(originalUrl);
            }

            return originalUrl;
        }

        protected override UrlData RawUrl(DecentralandUrl decentralandUrl)
        {
            UrlData serviceUrl = base.RawUrl(decentralandUrl);

            if (!enabled || serviceUrl.Url == null || !SUPPORTED_URLS.Contains(decentralandUrl))
                return serviceUrl;

            // FeatureFlagsDependent from base.RawUrl() signals a consolidated / optimized-assets URL that
            // must NOT be gateway-rewritten (it resolves to its own origin). If a future URL legitimately
            // needs FeatureFlagsDependent caching AND gateway routing, give UrlData a dedicated skipGateway
            // field instead of widening this guard. Custom hosts also pass through untouched.
            if (serviceUrl.Caching == CacheBehaviour.FeatureFlagsDependent || !IsGatewayTransformable(serviceUrl.Url))
                return serviceUrl;

            // it is called only once and then cached in the base class
            return new UrlData(CacheBehaviour.FeatureFlagsDependent, TransformToGateway(serviceUrl.Url));
        }

        /// <summary>
        ///     True only for a bare <c>https://{subdomain}.{BaseDomain}</c> authority: a single-label subdomain under
        ///     this client's own base domain, with no port or userinfo. Any other host — a <c>--gatekeeper-url</c>
        ///     override, a flag-driven assets host, another environment's domain — passes through untouched, because
        ///     the gateway only fronts this deployment's own services.
        /// </summary>
        private bool IsGatewayTransformable(string url)
        {
            if (domainSuffix == null || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return false;

            int hostEnd = url.IndexOf('/', HTTPS_PREFIX_LENGTH);
            ReadOnlySpan<char> authority = url.AsSpan(HTTPS_PREFIX_LENGTH, (hostEnd < 0 ? url.Length : hostEnd) - HTTPS_PREFIX_LENGTH);

            if (authority.IndexOfAny(':', '@') >= 0)
                return false;

            ReadOnlySpan<char> suffix = domainSuffix.AsSpan();

            if (authority.Length <= suffix.Length || !authority.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return false;

            // Single-label subdomain: "peer.decentraland.org" routes, "a.b.decentraland.org" does not.
            ReadOnlySpan<char> subdomain = authority.Slice(0, authority.Length - suffix.Length);
            return subdomain.IndexOf('.') < 0;
        }

        public override string GetOriginalUrl(string url)
        {
            if (!enabled || gatewayPrefix == null || !url.StartsWith(gatewayPrefix, StringComparison.OrdinalIgnoreCase))
                return url;

            string original = ReverseGatewayTransform(url);
            return original;
        }

        /// <summary>
        ///     Reverse of <see cref="TransformToGateway" />:
        ///     https://gateway.{domain}/{subdomain}/{path} → https://{subdomain}.{domain}/{path}
        /// </summary>
        private string ReverseGatewayTransform(string url)
        {
            int prefixLength = gatewayPrefix!.Length;
            ReadOnlySpan<char> afterPrefix = url.AsSpan(prefixLength);
            int slashIdx = afterPrefix.IndexOf('/');

            int subdomainLength = slashIdx >= 0 ? slashIdx : afterPrefix.Length;
            int pathLength = slashIdx >= 0 ? afterPrefix.Length - slashIdx : 0;
            string suffix = domainSuffix!;
            int resultLength = HTTPS_PREFIX_LENGTH + subdomainLength + suffix.Length + pathLength;

            return string.Create(resultLength, (url, prefixLength, subdomainLength, pathLength, suffix), static (span, state) =>
            {
                ReadOnlySpan<char> src = state.url.AsSpan();
                var pos = 0;

                "https://".AsSpan().CopyTo(span);
                pos += 8;

                src.Slice(state.prefixLength, state.subdomainLength).CopyTo(span.Slice(pos));
                pos += state.subdomainLength;

                state.suffix.AsSpan().CopyTo(span.Slice(pos));
                pos += state.suffix.Length;

                if (state.pathLength > 0)
                    src.Slice(state.prefixLength + state.subdomainLength, state.pathLength).CopyTo(span.Slice(pos));
            });
        }

        /// <summary>
        ///     Transform: https://{subdomain}.{domain}/{path}
        ///     to: {gateway origin}/{subdomain}/{path} — gateway.{BaseDomain} unless <c>--gateway</c> named another.
        /// </summary>
        private string TransformToGateway(string url)
        {
            if (gatewayPrefix == null)
                return url;

            string prefix = gatewayPrefix;

            int firstDot = url.IndexOf('.', HTTPS_PREFIX_LENGTH);

            if (firstDot < 0)
                return url;

            // Already a gateway URL — don't double-transform
            if (url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return url;

            int subdomainLength = firstDot - HTTPS_PREFIX_LENGTH;
            int pathStart = url.IndexOf('/', firstDot);
            int domainEnd = pathStart >= 0 ? pathStart : url.Length;
            int pathLength = url.Length - domainEnd;

            int resultLength = prefix.Length + subdomainLength + pathLength;

            return string.Create(resultLength, (url, prefix, subdomainLength, pathStart, pathLength), static (span, state) =>
            {
                ReadOnlySpan<char> src = state.url.AsSpan();

                var pos = 0;

                state.prefix.AsSpan().CopyTo(span);
                pos += state.prefix.Length;

                src.Slice(HTTPS_PREFIX_LENGTH, state.subdomainLength).CopyTo(span.Slice(pos));
                pos += state.subdomainLength;

                if (state.pathLength > 0)
                    src.Slice(state.pathStart, state.pathLength).CopyTo(span.Slice(pos));
            });
        }
    }
}
