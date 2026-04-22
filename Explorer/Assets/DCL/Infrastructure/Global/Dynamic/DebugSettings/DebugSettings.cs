using DCL.LiveKit.Public;
using LiveKit.Proto;
using System;
using UnityEngine;

namespace Global.Dynamic.DebugSettings
{
    public enum GatekeeperMode
    {
        Org = 0,
        Zone = 1,
        Today = 2,
        Localhost = 3,
        Custom = 4,
    }

    [Serializable]
    public class DebugSettings
    {
        private static readonly DebugSettings RELEASE_SETTINGS = Release();

        [SerializeField]
        private bool showSplash;
        [SerializeField]
        private bool showAuthentication;
        [SerializeField]
        private bool showLoading;
        [SerializeField]
        private bool enableLandscape;
        [SerializeField]
        private bool enableLOD;
        [SerializeField] private bool enableVersionUpdateGuard;
        [SerializeField]
        private bool enableEmulateNoLivekitConnection;
        [SerializeField] [Tooltip("Enable Portable Experiences obtained from Feature Flags from loading at the start of the game")]
        private bool enableRemotePortableExperiences;
        [SerializeField] [Tooltip("Make sure the ENS put here will be loaded as a GlobalPX (format must be something.dcl.eth)")]
        internal string[]? portableExperiencesEnsToLoad;
        [SerializeField]
        internal string[]? emotesToAddToUserProfile;
        [Space]
        [SerializeField]
        private bool overrideConnectionQuality;
        [SerializeField]
        private LKConnectionQuality connectionQuality;
        [Space]
        [Header("Comms Gatekeeper")]
        [SerializeField]
        private GatekeeperMode gatekeeperMode;
        [SerializeField] [Tooltip("Base gatekeeper URL used only when Gatekeeper Mode is set to Custom (e.g. http://localhost:3000)")]
        private string customGatekeeperUrl = string.Empty;
        [Space]
        [SerializeField]
        private string[] appParameters;

        public static DebugSettings Release() =>
            new ()
            {
                showSplash = true,
                showAuthentication = true,
                showLoading = true,
                enableLandscape = true,
                enableLOD = true,
                enableVersionUpdateGuard = true,
                portableExperiencesEnsToLoad = null,
                enableEmulateNoLivekitConnection = false,
                overrideConnectionQuality = false,
                connectionQuality = LKConnectionQuality.QualityExcellent,
                enableRemotePortableExperiences = true,
                emotesToAddToUserProfile = null,
                gatekeeperMode = GatekeeperMode.Org,
                customGatekeeperUrl = string.Empty,
                appParameters = Array.Empty<string>(),
            };

        // To avoid configuration issues, force full flow on build (Application.isEditor is always true in Editor, but in profile builds (i.e. when set to Development) we will have the expected release flow too.
        public string[]? EmotesToAddToUserProfile => Application.isEditor ? this.emotesToAddToUserProfile : RELEASE_SETTINGS.emotesToAddToUserProfile;
        public string[]? PortableExperiencesEnsToLoad => Application.isEditor ? this.portableExperiencesEnsToLoad : RELEASE_SETTINGS.portableExperiencesEnsToLoad;
        public bool EnableRemotePortableExperiences => Application.isEditor ? this.enableRemotePortableExperiences : RELEASE_SETTINGS.enableRemotePortableExperiences;
        public bool ShowSplash => Application.isEditor ? this.showSplash : RELEASE_SETTINGS.showSplash;
        public bool ShowAuthentication => Application.isEditor ? this.showAuthentication : RELEASE_SETTINGS.showAuthentication;
        public bool ShowLoading => Application.isEditor ? this.showLoading : RELEASE_SETTINGS.showLoading;
        public bool EnableLandscape => Application.isEditor ? this.enableLandscape : RELEASE_SETTINGS.enableLandscape;
        public bool EnableLOD => Application.isEditor ? this.enableLOD : RELEASE_SETTINGS.enableLOD;
        public bool EnableVersionUpdateGuard => Application.isEditor ? this.enableVersionUpdateGuard : RELEASE_SETTINGS.enableVersionUpdateGuard;
        public bool EnableEmulateNoLivekitConnection => Application.isEditor? this.enableEmulateNoLivekitConnection : RELEASE_SETTINGS.enableEmulateNoLivekitConnection;
        public bool OverrideConnectionQuality => Application.isEditor ? this.overrideConnectionQuality : RELEASE_SETTINGS.overrideConnectionQuality;
        public LKConnectionQuality ConnectionQuality => Application.isEditor ? this.connectionQuality : RELEASE_SETTINGS.connectionQuality;
        public GatekeeperMode GatekeeperMode => Application.isEditor ? gatekeeperMode : RELEASE_SETTINGS.gatekeeperMode;
        public string CustomGatekeeperUrl => Application.isEditor ? customGatekeeperUrl : RELEASE_SETTINGS.customGatekeeperUrl;
        public string[] AppParameters => Application.isEditor ? appParameters : RELEASE_SETTINGS.appParameters;

        /// <summary>
        ///     Base gatekeeper URL override derived from <see cref="GatekeeperMode"/> and
        ///     (for <see cref="Global.Dynamic.DebugSettings.GatekeeperMode.Custom"/>) <see cref="CustomGatekeeperUrl"/>.
        ///     Returns <c>null</c> when no override should be applied (Org mode, or Custom with an empty URL).
        /// </summary>
        public string? GatekeeperBaseOverride =>
            GatekeeperMode switch
            {
                GatekeeperMode.Org => null,
                GatekeeperMode.Zone => "https://comms-gatekeeper.decentraland.zone",
                GatekeeperMode.Today => "https://comms-gatekeeper.decentraland.today",
                GatekeeperMode.Localhost => "http://localhost:3000",
                GatekeeperMode.Custom => string.IsNullOrEmpty(CustomGatekeeperUrl) ? null : CustomGatekeeperUrl,
                _ => throw new ArgumentOutOfRangeException(nameof(GatekeeperMode), GatekeeperMode, null),
            };
    }
}
