using System.Collections.Generic;

namespace Global.AppArgs
{
    /// <summary>
    ///     Plain-language, one-line descriptions of what a deep-link param does to the session, shown next to each
    ///     denied key in the startup consent dialog so the user can judge the risk without knowing the codebase.
    ///     Only params a <see cref="DeepLinkAllowlist" /> denial can realistically surface need an entry; anything
    ///     without one falls back to <see cref="UNKNOWN" />.
    /// </summary>
    public static class DeepLinkParamDescriptions
    {
        public const string UNKNOWN = "Not recognized by this version - its effect is unknown.";

        private static readonly Dictionary<string, string> DESCRIPTIONS = new ()
        {
            // Runs code / external processes (SEC-005).
            // No AppArgsFlags constant: the Creator Hub path is resolved at runtime, it is not an app arg.
            ["creator-hub-bin-path"] = "Runs a program from a location chosen by the link.",
            [AppArgsFlags.LAUNCH_CDP_MONITOR_ON_START] = "Starts an external debugging process at launch.",

            // Points the client at infrastructure chosen by the link (SEC-052).
            [AppArgsFlags.COMMS_ADAPTER] = "Sends your voice and chat through a server chosen by the link.",
            [AppArgsFlags.GATEKEEPER_URL] = "Changes the server that grants access to voice and chat rooms.",
            [AppArgsFlags.FRIENDS_API_URL] = "Changes the server that holds your friends and social data.",
            [AppArgsFlags.FeatureFlags.URL] = "Changes the server that decides which features are enabled.",
            [AppArgsFlags.FeatureFlags.HOSTNAME] = "Changes the server that decides which features are enabled.",
            [AppArgsFlags.OPTIMIZED_ASSETS_URL] = "Changes the server scene models and textures are downloaded from.",
            [AppArgsFlags.LSD_REMOTE_AB_SERVER] = "Changes the server scene asset bundles are downloaded from.",
            [AppArgsFlags.LSD_REMOTE_AB_WORLD] = "Loads scene content from a world chosen by the link.",
            [AppArgsFlags.PULSE_MULTIPLAYER] = "Changes how this session connects to other players.",

            // Skips a protective screen.
            [AppArgsFlags.SKIP_VERSION_CHECK] = "Skips the check for an outdated Explorer version.",
            [AppArgsFlags.SKIP_MINIMUM_SPECS_SCREEN] = "Skips the hardware requirements warning.",
            [AppArgsFlags.SKIP_AUTH_SCREEN] = "Skips the sign-in screen.",

            // Development / automation modes.
            [AppArgsFlags.DEBUG] = "Enables debug mode and developer tooling.",
            [AppArgsFlags.AUTOPILOT] = "Takes over your avatar to run an automated route.",
            [AppArgsFlags.MEASURE_LOADING_TIME] = "Runs a loading-time benchmark and skips the sign-in screen.",
            [AppArgsFlags.ALTTESTER] = "Lets an external tool remotely control this client.",
            [AppArgsFlags.MCP] = "Opens a local port that lets other programs control this client.",
            [AppArgsFlags.MCP_PORT] = "Opens a local port that lets other programs control this client.",
            [AppArgsFlags.SCENE_CONSOLE] = "Opens the developer console for scenes.",
            [AppArgsFlags.LOCAL_SCENE] = "Loads a scene from a development server instead of Decentraland.",
            [AppArgsFlags.ENVIRONMENT] = "Switches the environment this client connects to.",
            [AppArgsFlags.DCL_EDITOR] = "Marks this session as launched from the Creator Hub.",
            [AppArgsFlags.MULTIPLE_RUNNING_INSTANCES] = "Allows several copies of the Explorer to run at once.",
            [AppArgsFlags.SIMULATE_MEMORY] = "Makes the Explorer act as if your machine had different memory.",
            [AppArgsFlags.SIMULATE_VERSION] = "Makes the Explorer report a different version than it is.",
            [AppArgsFlags.LANDSCAPE_TERRAIN_ENABLED] = "Turns the landscape terrain on or off.",

            // Loads content that is not published.
            [AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS] = "Loads unpublished wearables from Builder collections.",
            [AppArgsFlags.SELF_PREVIEW_WEARABLES] = "Loads wearables from a local preview folder.",
            [AppArgsFlags.SELF_PREVIEW_EMOTES] = "Loads emotes from a local preview folder.",
            [AppArgsFlags.FORCED_EMOTES] = "Adds emotes to your profile for this session.",

            // Session, cache and window behaviour.
            [AppArgsFlags.IDENTITY_EXPIRATION_DURATION] = "Changes how long you stay signed in.",
            [AppArgsFlags.DISABLE_DISK_CACHE] = "Disables the on-disk asset cache.",
            [AppArgsFlags.DISABLE_DISK_CACHE_CLEANUP] = "Stops the asset cache from being cleaned up.",
            [AppArgsFlags.DISABLE_HUD] = "Hides the in-world interface.",
            [AppArgsFlags.DISABLE_WINDOW_RESTRICTIONS] = "Removes restrictions on the application window.",
        };

        public static string For(string key) =>
            DESCRIPTIONS.TryGetValue(key, out string? description) ? description : UNKNOWN;
    }
}
