namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Resolved-URL metadata captured during video pre-loading — the only reusable part of a preload,
    ///     since an opened AVPro player cannot be shared or cloned.
    /// </summary>
    public readonly struct VideoTemplateData
    {
        public readonly MediaAddress ResolvedAddress;
        public readonly MediaAddress OriginalAddress;
        public readonly ResolvedMediaUrl Resolved;

        public VideoTemplateData(MediaAddress resolvedAddress, MediaAddress originalAddress, ResolvedMediaUrl resolved)
        {
            ResolvedAddress = resolvedAddress;
            OriginalAddress = originalAddress;
            Resolved = resolved;
        }
    }
}
