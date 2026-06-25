namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Resolved-URL metadata captured during video pre-loading — the only reusable part of a preload,
    ///     since an opened AVPro player cannot be shared or cloned.
    /// </summary>
    public readonly struct VideoTemplateData
    {
        /// <summary>Post-resolution direct URL that OpenMedia consumes.</summary>
        public readonly MediaAddress ResolvedAddress;

        /// <summary>Pre-resolution address, unchanged by URL redirection.</summary>
        public readonly MediaAddress OriginalAddress;

        public readonly bool IsReachable;
        public readonly bool IsLiveStream;
        public readonly float ResolvedUrlExpiresAt;

        public VideoTemplateData(MediaAddress resolvedAddress, MediaAddress originalAddress, bool isReachable,
            bool isLiveStream, float resolvedUrlExpiresAt)
        {
            ResolvedAddress = resolvedAddress;
            OriginalAddress = originalAddress;
            IsReachable = isReachable;
            IsLiveStream = isLiveStream;
            ResolvedUrlExpiresAt = resolvedUrlExpiresAt;
        }
    }
}
