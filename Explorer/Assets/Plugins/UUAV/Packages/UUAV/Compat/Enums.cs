namespace UUAV.Compat
{
    // How OpenMedia interprets its path argument. The consumer only ever
    // passes AbsolutePathOrURL; the remaining members exist for signature
    // parity.
    public enum MediaPathType
    {
        AbsolutePathOrURL,
        RelativeToProjectFolder,
        RelativeToStreamingAssetsFolder,
        RelativeToDataFolder,
        RelativeToPersistentDataFolder,
    }

    // UUAV surfaces errors only as a state, so the backend collapses every
    // failure onto LoadFailed; the consumer just checks for != None.
    public enum ErrorCode
    {
        None = 0,
        LoadFailed = 100,
        DecodeFailed = 200,
    }
}
