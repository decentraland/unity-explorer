using Cysharp.Threading.Tasks;
using DCL.Utility.Types;
using System.Threading;
using UnityEngine;

namespace DCL.BugReporting.UI
{
    /// <summary>Supplies the image the user attaches to a bug report; without a provider the attach flow is hidden.</summary>
    public interface IBugReportImageProvider
    {
        /// <returns>A cancelled result when the user aborts the selection.</returns>
        UniTask<Result<BugReportImage>> PickAsync(CancellationToken ct);
    }

    public readonly struct BugReportImage
    {
        public readonly byte[] Bytes;
        public readonly string ContentType;
        public readonly Texture2D Preview;

        public BugReportImage(byte[] bytes, string contentType, Texture2D preview)
        {
            Bytes = bytes;
            ContentType = contentType;
            Preview = preview;
        }
    }
}
