namespace Global.AppArgs
{
    public static class AppArgsFlags
    {
        public const string DEBUG = "debug";
        public const string DCL_EDITOR = "dcl-editor";

        public const string ENABLE_VERSION_CONTROL = "versionControl";
        public const string SIMULATE_VERSION = "simulateVersion";

        public const string SCENE_CONSOLE = "scene-console";

        public const string ENVIRONMENT = "dclenv";
        public const string REALM = "realm";
        public const string LOCAL_SCENE = "local-scene";
        public const string POSITION = "position";

        public const string FORCED_EMOTES = "self-force-emotes";
        public const string SELF_PREVIEW_EMOTES = "self-preview-emotes";
        public const string SELF_PREVIEW_WEARABLES = "self-preview-wearables";

        public const string CAMERA_REEL = "camera-reel";

        public const string FORCE_NO_TEXTURE_COMPRESSION = "force-no-texture-compression";

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
