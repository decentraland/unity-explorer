using LiveKit.Proto;

namespace Global.Dynamic.DebugSettings
{
    public interface IDebugSettings
    {
        string[]? PortableExperiencesEnsToLoad { get; }
        string[]? EmotesToAddToUserProfile { get; }
        bool ShowSplash { get; }
        bool ShowAuthentication { get; }
        bool ShowLoading { get; }
        bool EnableLandscape { get; }
        bool EnableLOD { get; }
        bool EnableVersionUpdateGuard { get; }
        bool EnableEmulateNoLivekitConnection { get; }
        bool OverrideConnectionQuality { get; }
        ConnectionQuality ConnectionQuality { get; }
        bool EnableRemotePortableExperiences { get; }
    }
}
