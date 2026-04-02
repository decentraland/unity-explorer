using DCL.LiveKit.Public;
using LiveKit.Proto;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Global.Dynamic.DebugSettings
{
    public enum GatekeeperMode
    {
        Org = 0,
        Zone = 3,
        Today = 4,
        Localhost = 1,
        Custom = 2,
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
        [SerializeField] [Tooltip("Full adapter URL used only when Gatekeeper Mode is set to Custom (e.g. http://localhost:3000/get-scene-adapter)")]
        private string customGatekeeperUrl = "http://localhost:3000/get-scene-adapter";
        [Space]
        [SerializeField]
        private string[] appParameters;

        private const string GATEKEEPER_URL_ORG = "https://comms-gatekeeper.decentraland.org/get-scene-adapter";
        private const string GATEKEEPER_URL_ZONE = "https://comms-gatekeeper.decentraland.zone/get-scene-adapter";
        private const string GATEKEEPER_URL_TODAY = "https://comms-gatekeeper.decentraland.today/get-scene-adapter";
        private const string GATEKEEPER_URL_LOCALHOST = "http://localhost:3000/get-scene-adapter";
        private const string GATEKEEPER_URL_FLAG = "gatekeeper-url";

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
                customGatekeeperUrl = GATEKEEPER_URL_LOCALHOST,
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
        public string[] AppParameters
        {
            get
            {
                if (!Application.isEditor)
                    return RELEASE_SETTINGS.appParameters;

                string? gatekeeperUrl = gatekeeperMode switch
                {
                    GatekeeperMode.Org => GATEKEEPER_URL_ORG,
                    GatekeeperMode.Zone => GATEKEEPER_URL_ZONE,
                    GatekeeperMode.Today => GATEKEEPER_URL_TODAY,
                    GatekeeperMode.Localhost => GATEKEEPER_URL_LOCALHOST,
                    GatekeeperMode.Custom => customGatekeeperUrl,
                    _ => null,
                };

                if (string.IsNullOrEmpty(gatekeeperUrl))
                    return appParameters;

                var merged = new List<string>(appParameters.Length + 2);
                merged.AddRange(appParameters);
                merged.Add($"--{GATEKEEPER_URL_FLAG}");
                merged.Add(gatekeeperUrl);
                return merged.ToArray();
            }
        }
    }
}
