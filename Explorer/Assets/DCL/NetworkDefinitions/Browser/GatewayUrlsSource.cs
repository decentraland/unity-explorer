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

            // LiveKit rooms providers
            DecentralandUrl.GateKeeperSceneAdapter,
            DecentralandUrl.LocalGateKeeperSceneAdapter,
            DecentralandUrl.ChatAdapter,
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
            DecentralandUrl.CameraReelImages,
            DecentralandUrl.CameraReelLink,
            DecentralandUrl.CameraReelPlaces,
            DecentralandUrl.CameraReelUsers,

            DecentralandUrl.AssetBundleRegistry,
            DecentralandUrl.AssetBundleRegistryVersion,

            DecentralandUrl.MediaConverter,
            DecentralandUrl.MarketplaceCredits,
            DecentralandUrl.Notifications,

            // Social
            DecentralandUrl.Communities,
            DecentralandUrl.CommunityThumbnail,
            DecentralandUrl.Members,
            DecentralandUrl.ActiveCommunityVoiceChats,
        };

        private readonly bool envSupported;

        private bool enabled => envSupported && FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.USE_GATEWAY);

        public GatewayUrlsSource(DecentralandEnvironment environment, IRealmData realmData, ILaunchMode launchMode) : base(environment, realmData, launchMode)
        {
            envSupported = SUPPORTED_ENVS.Contains(environment);
        }

        protected override string? RawUrl(DecentralandUrl decentralandUrl)
        {
            string? serviceUrl = base.RawUrl(decentralandUrl);

            if (!enabled || serviceUrl == null || !SUPPORTED_URLS.Contains(decentralandUrl))
                return serviceUrl;

            // Transform: https://{subdomain}.{domain}/{path}
            //        to: https://gateway.{domain}/{subdomain}/{path}
            ReadOnlySpan<char> url = serviceUrl.AsSpan();
            ReadOnlySpan<char> afterPrefix = url.Slice(HTTPS_PREFIX_LENGTH);

            int firstDotRel = afterPrefix.IndexOf('.');

            if (firstDotRel < 0)
                return serviceUrl;

            int subdomainLength = firstDotRel;
            ReadOnlySpan<char> afterSubdomain = afterPrefix.Slice(firstDotRel);

            int pathIndexRel = afterSubdomain.IndexOf('/');
            int domainLength = pathIndexRel >= 0 ? pathIndexRel : afterSubdomain.Length;
            int pathLength = pathIndexRel >= 0 ? afterSubdomain.Length - pathIndexRel : 0;

            // Result: "https://" + "gateway" + domain + "/" + subdomain + path
            int resultLength = HTTPS_PREFIX_LENGTH + GATEWAY_SUBDOMAIN.Length + domainLength + 1 + subdomainLength + pathLength;

            return string.Create(resultLength, (serviceUrl, subdomainLength, domainLength, pathIndexRel), static (span, state) =>
            {
                const int HTTPS_LEN = 8;
                const string GATEWAY = "gateway";

                ReadOnlySpan<char> url = state.serviceUrl.AsSpan();
                ReadOnlySpan<char> afterPrefix = url.Slice(HTTPS_LEN);
                ReadOnlySpan<char> subdomain = afterPrefix.Slice(0, state.subdomainLength);
                ReadOnlySpan<char> afterSubdomain = afterPrefix.Slice(state.subdomainLength);
                ReadOnlySpan<char> domain = afterSubdomain.Slice(0, state.domainLength);

                var pos = 0;

                "https://".AsSpan().CopyTo(span);
                pos += HTTPS_LEN;

                GATEWAY.AsSpan().CopyTo(span.Slice(pos));
                pos += GATEWAY.Length;

                domain.CopyTo(span.Slice(pos));
                pos += domain.Length;

                span[pos++] = '/';

                subdomain.CopyTo(span.Slice(pos));
                pos += subdomain.Length;

                if (state.pathIndexRel >= 0)
                    afterSubdomain.Slice(state.pathIndexRel).CopyTo(span.Slice(pos));
            });
        }
    }
}
