using Crosstales.FB;
using Cysharp.Threading.Tasks;
using DCL.Utility.Types;
using System.Threading;
using UnityEngine;

namespace DCL.BugReporting.UI
{
    /// <summary>Picks the screenshot through the OS file browser (File Browser PRO).</summary>
    public class OsFileBrowserBugReportImageProvider : IBugReportImageProvider
    {
        private const string FILE_BROWSER_TITLE = "Select a screenshot";
        private const int MAX_IMAGE_SIZE_BYTES = 10 * 1024 * 1024;

        private static readonly string[] ALLOWED_EXTENSIONS = { "png", "jpg", "jpeg" };

        public async UniTask<Result<BugReportImage>> PickAsync(CancellationToken ct)
        {
            FileBrowser.Instance.AllowSyncCalls = true;
            string path = FileBrowser.Instance.OpenSingleFile(FILE_BROWSER_TITLE, string.Empty, string.Empty, ALLOWED_EXTENSIONS);
            byte[]? data = FileBrowser.Instance.CurrentOpenSingleFileData;

            // The file browser needs two frames after closing (Mac) so the closing click doesn't fall through to the UI underneath.
            await UniTask.DelayFrame(2, cancellationToken: ct).SuppressCancellationThrow();

            if (ct.IsCancellationRequested)
                return Result<BugReportImage>.CancelledResult();

            if (string.IsNullOrEmpty(path) || data == null)
                return Result<BugReportImage>.CancelledResult();

            if (data.Length > MAX_IMAGE_SIZE_BYTES)
                return Result<BugReportImage>.ErrorResult($"The image exceeds {MAX_IMAGE_SIZE_BYTES / (1024 * 1024)} MB");

            // Decoding doubles as validation: the extension filter cannot vouch for the content.
            var preview = new Texture2D(2, 2);

            if (!preview.LoadImage(data))
            {
                Object.Destroy(preview);
                return Result<BugReportImage>.ErrorResult("The file is not a readable image");
            }

            return Result<BugReportImage>.SuccessResult(new BugReportImage(data, ContentTypeFor(data), preview));
        }

        // From the content, not the file name: a mislabeled extension must not mislead the uploads.
        private static string ContentTypeFor(byte[] data) =>
            data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
                ? "image/png"
                : "image/jpeg";
    }
}
