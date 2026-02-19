using System.Diagnostics.CodeAnalysis;

namespace DCL.Multiplayer.Connections.DecentralandUrls
{
    /// <summary>
    ///     Maintain the order as it's serialized
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum DecentralandUrl
    {
        Host = 0,
        Genesis = 1,

        ArchipelagoStatus = 2,
        GatekeeperStatus = 3,
        ArchipelagoHotScenes = 4,

        SupportLink = 5,
        DiscordDirectLink = 6,
        TwitterLink = 7,
        TwitterNewPostLink = 8,
        NewsletterSubscriptionLink = 9,
        MarketplaceLink = 10,

        PrivacyPolicy = 11,
        TermsOfUse = 12,
        ContentPolicy = 13,
        CodeOfEthics = 14,

        ApiChunks = 15,

        ApiPlaces = 16,
        ApiWorlds = 17,
        ApiDestinations = 18,
        POI = 19,
        Map = 20,
        ContentModerationReport = 21,

        ApiEvents = 22,
        EventsWebpage = 23,

        ApiAuth = 24,
        AuthSignatureWebApp = 25,
        ApiRpc = 26,

        GateKeeperSceneAdapter = 27,
        LocalGateKeeperSceneAdapter = 28,
        ChatAdapter = 29,

        OpenSea = 30,

        PeerAbout = 31,
        RemotePeers = 32,
        RemotePeersWorld = 33,

        DAO = 34,

        Help = 35,
        Account = 36,
        MinimumSpecs = 37,

        FeatureFlags = 38,

        Market = 39,
        MarketplaceClaimName = 40,

        AssetBundlesCDN = 41,

        Badges = 42,

        CameraReelUsers = 43,
        CameraReelImages = 44,
        CameraReelPlaces = 45,
        CameraReelLink = 46,

        ApiFriends = 47,
        AssetBundleRegistry = 48,
        AssetBundleRegistryVersion = 49,
        Profiles = 50,
        ProfilesMetadata = 51,

        BuilderApiDtos = 52,
        BuilderApiContent = 53,

        Blocklist = 54,

        WorldServer = 55,

        Servers = 56,

        MediaConverter = 57,

        MarketplaceCredits = 58,
        GoShoppingWithMarketplaceCredits = 59,
        Notifications = 60,

        Communities = 61,
        CommunityThumbnail = 62,
        Members = 63,
        CommunityProfileLink = 64,

        DecentralandWorlds = 65,

        ChatTranslate = 66,

        ActiveCommunityVoiceChats = 67,

        Support = 68,

        ManaUsdRateApiUrl = 69,

        CreatorHub = 70,

        JumpInGenesisCityLink = 71,
        JumpInWorldLink = 72,

        /// <summary>
        ///     Normally Entities Active are served through Asset Bundle Registry, and not via a catalyst, it's transparent for consumers
        /// </summary>
        EntitiesActive = 73,

        // Catalyst Related
        Lambdas = 74,
        Content = 75,
        EntitiesDeployment = 76,


        BuilderApiNewsletter = 77,
        MetaTransactionServer = 78,
<<<<<<< HEAD
        
        WorldPermissions = 79,
        WorldComms = 80,
        WorldCommsSceneAdapter = 81
=======

        WorldContentServer = 79,

        WorldEntitiesActive = 80,
        WorldCommsAdapter = 81,




>>>>>>> dev
    }
}
