using Best.HTTP;
using Best.HTTP.Shared.PlatformSupport.Memory;
using Best.HTTP.Shared.Streams;
using System;
using System.IO;

namespace DCL.WebRequests.HTTP2
{
    /// <summary>
    ///     A thread-safe, non-destructive, seekable stream built on top of BufferSegmentStream
    ///     that complies with Unity AssetBundle loading requirements.
    ///     <remarks>
    ///         <list type="bullet">
    ///             <item>Consists of the original <see cref="BufferSegment" /> that were allocated by <see cref="HTTPResponse.DownStream" /></item>
    ///             <item>The ownership of the segments is transferred to this stream from the original <see cref="HTTPResponse.DownStream" /> when they are being read</item>
    ///             <item>The segments are disposed upon Dispose of this stream: they will return to the original pool of the BestHTTP</item>
    ///         </list>
    ///     </remarks>
    /// </summary>
    public class SeekableBufferSegmentStream : BufferSegmentStream
    {
        // Lock object to ensure thread-safe access to Read/Seek.
        private readonly object @lock = new ();

        // Current stream position and segment tracking
        private long position; // The overall position in the stream
        private int currentSegmentIndex; // Which segment we're in
        private int currentSegmentOffset; // Offset within that segment

        /// <summary>
        ///     Override CanSeek so that Unity knows we support seeking.
        /// </summary>
        public override bool CanSeek => true;

        /// <summary>
        ///     Override CanRead to make sure it returns true (Unity requires this).
        /// </summary>
        public override bool CanRead => true;

        /// <summary>
        ///     Unity sets Position = 0 before loading, so we must support it.
        /// </summary>
        public override long Position
        {
            get
            {
                lock (@lock) { return position; }
            }

            set => Seek(value, SeekOrigin.Begin);
        }

        /// <summary>
        ///     The length is the sum of the remaining counts in all BufferSegments.
        ///     We override because the base class _length is decreased destructively.
        /// </summary>
        public override long Length
        {
            get
            {
                lock (@lock)
                {
                    long total = 0;

                    for (var i = 0; i < bufferList.Count; i++) { total += bufferList[i].Count; }

                    return total;
                }
            }
        }

        public SeekableBufferSegmentStream()
        {
            position = 0;
            currentSegmentIndex = 0;
            currentSegmentOffset = 0;
        }

        /// <summary>
        ///     We provide our own non-destructive Read. This ensures data stays
        ///     in the underlying buffer segments for seeking and multiple reads.
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            // Basic argument checks
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException("Invalid offset or count for Read.");

            lock (@lock)
            {
                if (position >= Length)
                {
                    // Already at or past the end — no bytes available
                    return 0;
                }

                var totalBytesRead = 0;

                // Read up to 'count' bytes, or until we run out of data
                while (count > 0 && currentSegmentIndex < bufferList.Count)
                {
                    BufferSegment seg = bufferList[currentSegmentIndex];

                    // How many bytes are left in the current segment from our offset?
                    int segmentReadable = seg.Count - currentSegmentOffset;

                    if (segmentReadable <= 0)
                    {
                        // Move to the next segment
                        currentSegmentIndex++;
                        currentSegmentOffset = 0;
                        continue;
                    }

                    int toRead = Math.Min(count, segmentReadable);

                    // Copy from segment.Data -> user buffer
                    Array.Copy(
                        seg.Data,
                        seg.Offset + currentSegmentOffset,
                        buffer,
                        offset,
                        toRead
                    );

                    // Update counters and positions
                    offset += toRead;
                    count -= toRead;
                    totalBytesRead += toRead;
                    position += toRead;
                    currentSegmentOffset += toRead;
                }

                return totalBytesRead;
            }
        }

        /// <summary>
        ///     ReadByte is basically a convenience method that calls Read(...) for 1 byte.
        ///     We do it in a thread-safe way, matching the non-destructive approach.
        /// </summary>
        public override int ReadByte()
        {
            var singleByte = new byte[1];
            int read = Read(singleByte, 0, 1);
            return read == 0 ? -1 : singleByte[0];
        }

        /// <summary>
        ///     Implement seeking so Unity can jump around in the stream.
        ///     We recalculate which segment and offset correspond to the new position.
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin)
        {
            lock (@lock)
            {
                long newPos;

                switch (origin)
                {
                    case SeekOrigin.Begin:
                        newPos = offset;
                        break;
                    case SeekOrigin.Current:
                        newPos = position + offset;
                        break;
                    case SeekOrigin.End:
                        // Because Length is the total size of the AssetBundle data,
                        // offset from the end is (Length + offset).
                        newPos = Length + offset;
                        break;
                    default:
                        throw new ArgumentException("Invalid SeekOrigin", nameof(origin));
                }

                // Don’t allow seeking before the start
                if (newPos < 0)
                    newPos = 0;

                // It's typically safe to allow seeking beyond the end; we clamp reads to 0 bytes
                // if the position is beyond the actual data length.
                // But we won't throw an exception for that scenario.
                if (newPos > Length)
                    newPos = Length;

                position = newPos;

                // Recompute which segment we're in
                long remaining = newPos;
                currentSegmentIndex = 0;
                currentSegmentOffset = 0;

                for (var i = 0; i < bufferList.Count; i++)
                {
                    long segCount = bufferList[i].Count;

                    if (remaining < segCount)
                    {
                        currentSegmentIndex = i;
                        currentSegmentOffset = (int)remaining;
                        break;
                    }

                    remaining -= segCount;
                }

                return position;
            }
        }

        /// <summary>
        ///     Writes still append data to the end. We keep it thread-safe as well.
        ///     If Unity doesn't need writes, you can remove or restrict this.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (@lock)
            {
                // We can still rely on the base class "destructive length" logic for writing,
                // but be aware that the base modifies _length which we don't use anymore.
                // However, we do want to keep all segments in 'bufferList', so no conflict.
                base.Write(buffer, offset, count);
            }
        }

        /// <summary>
        ///     We do not support explicitly changing the Length in the middle of streaming.
        /// </summary>
        public override void SetLength(long value)
        {
            throw new NotSupportedException("SetLength is not supported on SeekableBufferSegmentStream.");
        }
    }
}
