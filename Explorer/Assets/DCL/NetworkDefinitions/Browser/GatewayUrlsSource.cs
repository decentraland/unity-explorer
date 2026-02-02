using DCL.Browser.DecentralandUrls;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility;
using ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using Utility;

namespace DCL.Browser
{
    public class GatewayUrlsSource : DecentralandUrlsSource
    {
        private const string GATEWAY_SUBDOMAIN = "gateway";
        private const int HTTPS_PREFIX_LENGTH = 8; // "https://".Length

        private static readonly DecentralandEnvironment[] SUPPORTED_ENVS = { DecentralandEnvironment.Org, DecentralandEnvironment.Zone };

        private static readonly HashSet<DecentralandUrl> SUPPORTED_URLS = new (EnumUtils.GetEqualityComparer<DecentralandUrl>())
        {
            // Places API
            DecentralandUrl.ApiPlaces,
            DecentralandUrl.ApiWorlds,
            DecentralandUrl.ApiDestinations,
            DecentralandUrl.Map,
            DecentralandUrl.ContentModerationReport,

            DecentralandUrl.ApiAuth,
            DecentralandUrl.ApiChunks,

            // LiveKit rooms providers require signed fetch
            // DecentralandUrl.GateKeeperSceneAdapter,
            // DecentralandUrl.LocalGateKeeperSceneAdapter,
            // DecentralandUrl.ChatAdapter,
            DecentralandUrl.GatekeeperStatus,
            DecentralandUrl.RemotePeers,
            DecentralandUrl.RemotePeersWorld,
            DecentralandUrl.ArchipelagoStatus,
            DecentralandUrl.ArchipelagoHotScenes,

            // Content Servers
            DecentralandUrl.AssetBundlesCDN,
            DecentralandUrl.WorldContentServer,

            DecentralandUrl.Genesis,
            DecentralandUrl.Badges,

            // Requires signed fetch
            //DecentralandUrl.CameraReelImages,
            //DecentralandUrl.CameraReelLink,
            //DecentralandUrl.CameraReelPlaces,
            //DecentralandUrl.CameraReelUsers,

            DecentralandUrl.AssetBundleRegistry,
            DecentralandUrl.AssetBundleRegistryVersion,

            DecentralandUrl.MediaConverter,

            // DecentralandUrl.MarketplaceCredits,
            // DecentralandUrl.Notifications, Notification partially required signed fetch

            // Social
            DecentralandUrl.CommunityThumbnail,

            // The following requires signed fetch
            //DecentralandUrl.Communities,
            //DecentralandUrl.Members,
            //DecentralandUrl.ActiveCommunityVoiceChats,
        };

        /// <summary>
        ///     Routing via the Gateway enables multiplexing over HTTP/2 even for resources originated from the backend services
        /// </summary>
        private static readonly HashSet<string> SUPPORTED_URLS_OF_NON_CLIENT_ORIGIN = new (StringComparer.OrdinalIgnoreCase)
        {
            $"profile-images.decentraland.{ENV}",
        };

        private readonly bool envSupported;
        private readonly List<string>? resolvedNonClientHosts;

        private bool enabled => envSupported && FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.USE_GATEWAY);

        public GatewayUrlsSource(DecentralandEnvironment environment, IRealmData realmData, ILaunchMode launchMode) : base(environment, realmData, launchMode)
        {
            envSupported = SUPPORTED_ENVS.Contains(environment);

            if (envSupported)
            {
                string envDomain = environment.ToString()!.ToLower();
                resolvedNonClientHosts = new List<string>(SUPPORTED_URLS_OF_NON_CLIENT_ORIGIN.Count);

                foreach (string pattern in SUPPORTED_URLS_OF_NON_CLIENT_ORIGIN)
                    resolvedNonClientHosts.Add(pattern.Replace(ENV, envDomain));
            }
        }

        public new static GatewayUrlsSource CreateForTest(DecentralandEnvironment environment, ILaunchMode launchMode) =>
            new (environment, new IRealmData.Fake(), launchMode);

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

        protected override string? RawUrl(DecentralandUrl decentralandUrl)
        {
            string? serviceUrl = base.RawUrl(decentralandUrl);

            if (!enabled || serviceUrl == null || !SUPPORTED_URLS.Contains(decentralandUrl))
                return serviceUrl;

            return TransformToGateway(serviceUrl);
        }

        /// <summary>
        ///     Transform: https://{subdomain}.{domain}/{path}
        ///     to: https://gateway.{domain}/{subdomain}/{path}
        /// </summary>
        private static string TransformToGateway(string url)
        {
            int firstDot = url.IndexOf('.', HTTPS_PREFIX_LENGTH);

            if (firstDot < 0)
                return url;

            int subdomainLength = firstDot - HTTPS_PREFIX_LENGTH;
            int pathStart = url.IndexOf('/', firstDot);
            int domainEnd = pathStart >= 0 ? pathStart : url.Length;
            int domainLength = domainEnd - firstDot;
            int pathLength = url.Length - domainEnd;

            int resultLength = HTTPS_PREFIX_LENGTH + GATEWAY_SUBDOMAIN.Length + domainLength + 1 + subdomainLength + pathLength;

            return string.Create(resultLength, (url, firstDot, domainLength, subdomainLength, pathStart, pathLength), static (span, state) =>
            {
                const string GATEWAY = "gateway";
                ReadOnlySpan<char> src = state.url.AsSpan();

                var pos = 0;

                "https://".AsSpan().CopyTo(span);
                pos += 8;

                GATEWAY.AsSpan().CopyTo(span.Slice(pos));
                pos += GATEWAY.Length;

                src.Slice(state.firstDot, state.domainLength).CopyTo(span.Slice(pos));
                pos += state.domainLength;

                span[pos++] = '/';

                src.Slice(8, state.subdomainLength).CopyTo(span.Slice(pos));
                pos += state.subdomainLength;

                if (state.pathLength > 0)
                    src.Slice(state.pathStart, state.pathLength).CopyTo(span.Slice(pos));
            });
        }
    }
}
