namespace Global.AppArgs
{
    public static class AppArgsFlags
    {
        public const string DEBUG = "debug";
        public const string DCL_EDITOR = "hub";

        public const string SKIP_VERSION_CHECK = "skip-version-check";
        public const string SIMULATE_VERSION = "simulateVersion";
        public const string FORCE_MINIMUM_SPECS_SCREEN = "forceMinimumSpecsScreen";

        public const string SCENE_CONSOLE = "scene-console";

        public const string ENVIRONMENT = "dclenv";
        public const string REALM = "realm";
        public const string COMMS_ADAPTER = "comms-adapter";
        public const string LOCAL_SCENE = "local-scene";
        public const string POSITION = "position";
        public const string SKIP_AUTH_SCREEN = "skip-auth-screen";
        public const string LANDSCAPE_TERRAIN_ENABLED = "landscape-terrain-enabled";

        public const string FORCED_EMOTES = "self-force-emotes";
        public const string SELF_PREVIEW_EMOTES = "self-preview-emotes";
        public const string SELF_PREVIEW_WEARABLES = "self-preview-wearables";
        public const string SELF_PREVIEW_BUILDER_COLLECTIONS = "self-preview-builder-collections";
        public const string SELF_PREVIEW_BUILDER_EMOTE_COLLECTIONS = "self-preview-builder-emote-collections";

        public const string PROFILE_NAME_EDITOR = "profile-name-editor";

        public const string CAMERA_REEL = "camera-reel";
        public const string FRIENDS = "friends";
        public const string FRIENDS_API_URL = "friends-api-url";
        public const string FRIENDS_ONLINE_STATUS = "friends-online-status";
        public const string FRIENDS_USER_BLOCKING = "friends-user-blocking";

        public const string DISABLE_DISK_CACHE = "disable-disk-cache";
        public const string DISABLE_DISK_CACHE_CLEANUP = "disable-disk-cache-cleanup";

        public const string IDENTITY_EXPIRATION_DURATION = "identity-expiration-duration";

        public const string SIMULATE_MEMORY = "simulateMemory";

        public static class Multiplayer
        {
            public const string COMPRESSION = "compression";
        }

        public static class FeatureFlags
        {
            public const string URL = "feature-flags-url";
            public const string HOSTNAME = "feature-flags-hostname";
        }

        public static class Analytics
        {
            public const string SESSION_ID = "session_id";
            public const string LAUNCHER_ID = "launcher_anonymous_id";
        }
    }
}
