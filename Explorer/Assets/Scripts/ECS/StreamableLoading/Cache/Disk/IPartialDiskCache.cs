using Cysharp.Threading.Tasks;
using DCL.Optimization.Hashing;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Utility.Multithreading;
using Utility.Types;

namespace ECS.StreamableLoading.Cache.Disk
{
    public interface IPartialDiskCache
    {
        UniTask<EnumResult<MutexSlim<PartialFile>, TaskError>> PartialFileAsync(HashKey key, string extension, CancellationToken ct);
    }

    public class PartialFile : IDisposable
    {
        private readonly FileStream stream;
        private readonly ReadOnlyViewStream readOnlyViewStream;
        private readonly HashKey hashKey;
        private Meta meta;

        public Meta MetaData => meta;

        public Stream ReadOnlyStream => readOnlyViewStream;

        public int NextRangeStart => (int)stream.Length - Meta.MetaSize;

        internal PartialFile(HashKey hashKey, FileStream stream, Meta meta)
        {
            this.hashKey = hashKey;
            this.stream = stream;
            this.meta = meta;
            readOnlyViewStream = new ReadOnlyViewStream(stream, Meta.MetaSize);
        }

        public async UniTask AppendDataAsync(ReadOnlyMemory<byte> data, HashKey forFile)
        {
            if (meta.IsFullyDownloaded)
                throw new InvalidOperationException("File is already fully downloaded");

            if (hashKey.Equals(forFile) == false)
                throw new InvalidOperationException($"HashKey does not match: {hashKey.ToString()} and {forFile.ToString()}");

            stream.Seek(0, SeekOrigin.End);
            await stream.WriteAsync(data);
            await UpdateMetaAsync();
        }

        public UniTask UpdateFullSizeIfRequiredAsync(int targetSize)
        {
            if (meta.MaxFileSize != 0)
                return UniTask.CompletedTask;

            var newMeta = new Meta(targetSize, 0, false);
            meta = newMeta;
            return WriteMetaAsync(newMeta);
        }

        private UniTask UpdateMetaAsync()
        {
            var current = meta;
            int withoutMetaLength = (int)stream.Length - Meta.MetaSize;
            bool isFullyDownloaded = withoutMetaLength >= current.MaxFileSize;
            meta = new Meta(current.MaxFileSize, withoutMetaLength, isFullyDownloaded);
            return WriteMetaAsync(meta);
        }

        private async UniTask WriteMetaAsync(Meta metaToWrite)
        {
            using var buffer = MemoryPool<byte>.Shared!.Rent(Meta.MetaSize)!;
            var memory = buffer.Memory.Slice(0, Meta.MetaSize);
            metaToWrite.ToSpan(memory.Span);
            stream.Seek(0, SeekOrigin.Begin);
            await stream.WriteAsync(memory);
        }

        public void Dispose()
        {
            stream.Dispose();
            hashKey.Dispose();
        }

        public readonly struct Meta
        {
            public static int MetaSize
            {
                get
                {
                    unsafe { return sizeof(Meta); }
                }
            }

            public readonly int MaxFileSize;
            public readonly int WrittenBytesSize;
            public readonly bool IsFullyDownloaded;

            public Meta(int maxFileSize, int writtenBytesSize, bool isFullyDownloaded)
            {
                this.MaxFileSize = maxFileSize;
                this.WrittenBytesSize = writtenBytesSize;
                this.IsFullyDownloaded = isFullyDownloaded;
            }

            public void ToSpan(Span<byte> span)
            {
                var self = this;
                var origin = MemoryMarshal.CreateReadOnlySpan(ref self, 1);
                var raw = MemoryMarshal.AsBytes(origin);

                raw.CopyTo(span);
            }

            public static Meta FromSpan(ReadOnlySpan<byte> array) =>
                MemoryMarshal.Read<Meta>(array);

            public static async UniTask<Meta> FromStreamAsync(Stream mutStream)
            {
                int metaSize = MetaSize;
                using var buffer = MemoryPool<byte>.Shared!.Rent(metaSize)!;
                var memory = buffer.Memory.Slice(0, metaSize);
                mutStream.Seek(0, SeekOrigin.Begin);
                int read = await mutStream.ReadAsync(memory);

                if (read != MetaSize)
                {
                    var meta = new Meta(0, 0, false);
                    meta.ToSpan(memory.Span);
                    mutStream.Seek(0, SeekOrigin.Begin);
                    await mutStream.WriteAsync(memory);
                    return meta;
                }

                return FromSpan(buffer.Memory.Span);
            }
        }

        private class ReadOnlyViewStream : Stream
        {
            private readonly FileStream originStream;
            private readonly int startOffset;

            public ReadOnlyViewStream(FileStream originStream, int startOffset)
            {
                this.originStream = originStream;
                this.startOffset = startOffset;
            }

            public override void Flush()
            {
                throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count) =>
                originStream.Read(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin)
            {
                offset += startOffset;
                long result = originStream.Seek(offset, origin);
                return result - startOffset;
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => originStream.Length - startOffset;

            public override long Position
            {
                get => originStream.Position - startOffset;
                set => originStream.Position = value + startOffset;
            }
        }
    }
}
