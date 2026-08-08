using Cysharp.Threading.Tasks;
using DCL.Utility.Types;
using System.Threading;
using UnityEngine;

namespace DCL.BugReporting.UI
{
    /// <summary>
    ///     Supplies the image the user attaches to a bug report. The form works without one:
    ///     when no provider is available the attach flow is hidden.
    /// </summary>
    public interface IBugReportImageProvider
    {
        /// <returns>A cancelled result when the user aborts the selection.</returns>
        UniTask<Result<BugReportImage>> PickAsync(CancellationToken ct);
    }

    public readonly struct BugReportImage
    {
        /// <summary>Encoded image bytes, ready to upload.</summary>
        public readonly byte[] Bytes;

        /// <summary>Mime type of <see cref="Bytes" />, e.g. "image/jpeg".</summary>
        public readonly string ContentType;

        /// <summary>Texture shown as the in-form preview.</summary>
        public readonly Texture2D Preview;

        public BugReportImage(byte[] bytes, string contentType, Texture2D preview)
        {
            Bytes = bytes;
            ContentType = contentType;
            Preview = preview;
        }
    }
}
