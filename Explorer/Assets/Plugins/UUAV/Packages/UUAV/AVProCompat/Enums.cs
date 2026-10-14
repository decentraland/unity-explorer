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
}
