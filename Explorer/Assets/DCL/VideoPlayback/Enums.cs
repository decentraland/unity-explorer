namespace DCL.VideoPlayback
{
    // How OpenMedia interprets its path argument. The consumer only ever
    // passes AbsolutePathOrURL; the remaining members exist for signature
    // parity with the facade.
    public enum MediaPathType
    {
        AbsolutePathOrURL,
        RelativeToProjectFolder,
        RelativeToStreamingAssetsFolder,
        RelativeToDataFolder,
        RelativeToPersistentDataFolder,
    }

    // Player error codes. Values match the facade's enum, which this layer
    // casts numerically.
    public enum ErrorCode
    {
        None = 0,
        LoadFailed = 100,
        DecodeFailed = 200,
    }
}
