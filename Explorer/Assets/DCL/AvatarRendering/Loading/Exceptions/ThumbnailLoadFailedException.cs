using System;

namespace DCL.AvatarRendering.Loading.Exceptions
{
    /// <summary>
    ///     Thrown when a thumbnail request reaches a terminal failure state (e.g. the underlying
    ///     promise was cancelled mid-flight). Distinct from <see cref="OperationCanceledException"/>,
    ///     which is reserved for caller-initiated cancellation.
    /// </summary>
    public class ThumbnailLoadFailedException : Exception
    {
        public ThumbnailLoadFailedException() : base("Thumbnail load failed or was cancelled before completion") { }

        public ThumbnailLoadFailedException(string message) : base(message) { }
    }
}