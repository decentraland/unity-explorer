using LiveKit.Proto;
using System;
using UnityEngine;

namespace Global.Dynamic.DebugSettings
{
    [Serializable]
    public class DebugSettings : IDebugSettings
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
        private ConnectionQuality connectionQuality;
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
                connectionQuality = ConnectionQuality.QualityExcellent,
                enableRemotePortableExperiences = true,
                emotesToAddToUserProfile = null,
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
        public ConnectionQuality ConnectionQuality => Application.isEditor ? this.connectionQuality : RELEASE_SETTINGS.connectionQuality;
        public string[] AppParameters => appParameters;
    }
}
