namespace DCL.AvProSwitch
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

    // Mirrors AVPro's ErrorCode (same members, same values in both backends).
    public enum ErrorCode
    {
        None = 0,
        LoadFailed = 100,
        DecodeFailed = 200,
    }

    // Windows platform-option enums. Forwarded to AVPro on the AVPro backend;
    // inert under UUAV (audio always routes through Unity, video always uses
    // the D3D11 path).
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
