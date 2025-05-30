using Arch.Core;
using System.IO;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Represents either a file or memory stream of a single process of partial downloading
    ///     <remarks>
    ///         <list type="bullet">
    ///             <item>
    ///                 The stream is not thread-safe, it's impossible to read and write at the same time, it's assumed that the caller side will wait for the Web Request Controller
    ///                 to finish its part before reading
    ///             </item>
    ///             <item>
    ///                 While Partial Downloading is not finished it protects the underlying stream from releasing.
    ///                 It's mandatory to release the stream when the related operations are finished
    ///             </item>
    ///             <item>
    ///                 It's possible to read from the stream only when the download is finished, after that it's impossible to write to it
    ///             </item>
    ///             <item>
    ///                 Once the stream is created it won't be stored anywhere, it's responsibility of the consumer to keep the reference and re-utilize it
    ///             </item>
    ///             <item>
    ///                 While the stream is open it keep a file descriptor open if there is enough space on disk.
    ///             </item>
    ///             <item>
    ///                 It's the responsibility of the consumer to dispose of the stream
    ///             </item>
    ///         </list>
    ///     </remarks>
    /// </summary>
    public abstract class PartialDownloadStream : Stream
    {
        /// <summary>
        ///     There could be only one owner of the stream at a time: <br />
        ///     It's by design as we should not even try to read/write to the partial stream in parallel <br />
        ///     It's an easy way to prevent concurrency issues without complications
        /// </summary>
        public Entity Entity { get; private set; }

        public abstract bool IsFullyDownloaded { get; }

        public void SetOwner(Entity entity)
        {
            Entity = entity;
        }
    }
}
