// Effectivly NO-OPs, exist only for the compatabilitty
namespace UUAV.Compat
{
    // Mirrors AVPro's MediaPathType. The consumer only ever passes
    // AbsolutePathOrURL; the remaining members exist for signature parity.
    public enum MediaPathType
    {
        AbsolutePathOrURL,
        RelativeToProjectFolder,
        RelativeToStreamingAssetsFolder,
        RelativeToDataFolder,
        RelativeToPersistentDataFolder,
    }

    // Mirrors AVPro's ErrorCode. UUAV surfaces errors only as a state, so the
    // backend collapses every failure onto LoadFailed; the consumer just checks
    // for != None.
    public enum ErrorCode
    {
        None = 0,
        LoadFailed = 100,
        DecodeFailed = 200,
    }

    // Windows platform-option enums. Inert under UUAV (audio always routes
    // through Unity, video always uses the D3D11 path) and present only so the
    // consumer's platform-options assignments compile.
    public static class Windows
    {
        public enum VideoApi
        {
            MediaFoundation,
            DirectShow,
            WinRT,
        }

        public enum AudioOutput
        {
            System,
            Unity,
            FacebookAudio360,
            None,
        }
    }
}
