namespace DCL.Multiplayer.Connections.DecentralandUrls
{
    public enum DecentralandUrl
    {
        Host,
        Genesis,

        ArchipelagoStatus,
        GatekeeperStatus,
        ArchipelagoHotScenes,

        DiscordLink,
        DiscordDirectLink,
        TwitterLink,
        TwitterNewPostLink,
        NewsletterSubscriptionLink,
        MarketplaceLink,

        PrivacyPolicy,
        TermsOfUse,
        ContentPolicy,
        CodeOfEthics,

        ApiChunks,

        ApiPlaces,
        ApiWorlds,
        ApiDestinations,
        POI,
        Map,
        ContentModerationReport,

        ApiEvents,
        EventsWebpage,

        ApiAuth,
        AuthSignatureWebApp,
        ApiRpc,

        GateKeeperSceneAdapter,
        LocalGateKeeperSceneAdapter,
        ChatAdapter,

        OpenSea,

        PeerAbout,
        RemotePeers,
        RemotePeersWorld,

        DAO,

        Help,
        Account,
        MinimumSpecs,

        FeatureFlags,

        Market,
        MarketplaceClaimName,

        AssetBundlesCDN,

        Badges,

        CameraReelUsers,
        CameraReelImages,
        CameraReelPlaces,
        CameraReelLink,

        ApiFriends,
        AssetBundleRegistry,
        AssetBundleRegistryVersion,
        Profiles,
        ProfilesMetadata,

        BuilderApiDtos,
        BuilderApiContent,

        Blocklist,

        WorldContentServer,

        Servers,

        MediaConverter,

        MarketplaceCredits,
        GoShoppingWithMarketplaceCredits,
        Notifications,

        Communities,
        CommunityThumbnail,
        Members,
        CommunityProfileLink,

        DecentralandWorlds,

        ChatTranslate,

        ActiveCommunityVoiceChats,

        Support,

        ManaUsdRateApiUrl,

        CreatorHub,

        JumpInGenesisCityLink,
        JumpInWorldLink,

        /// <summary>
        ///     Normally Entities Active are served through Asset Bundle Registry, and not via a catalyst, it's transparent for consumers
        /// </summary>
        EntitiesActive,

        // Catalyst Related
        Lambdas,
        Content,
        EntitiesDeployment,
    }
}
