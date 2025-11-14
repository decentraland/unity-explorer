using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Buffers;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Networking;

namespace SceneRuntime.Factory.JsSource
{
    public readonly struct DownloadedOrCachedData : IDisposable
    {
        private readonly DownloadHandler? downloaded;
        private readonly NativeArray<byte>.ReadOnly downloadedData;
        private readonly SlicedOwnedMemory<byte> cached;

        /// <remarks>
        /// Must be called from the main thread.
        /// </remarks>
        public DownloadedOrCachedData(DownloadHandler downloadHandler)
        {
            downloaded = downloadHandler;
            downloadedData = downloadHandler.nativeData;
            cached = default;
        }

        public DownloadedOrCachedData(SlicedOwnedMemory<byte> memory)
        {
            downloaded = null;
            downloadedData = default;
            cached = memory;
        }

        public void Dispose()
        {
            if (downloaded != null)
                downloaded.Dispose();
            else
                cached.Dispose();
        }

        private ReadOnlyMemory<byte> AsReadOnlyMemory() =>
            downloaded != null ? new MemoryManager(downloadedData).Memory : cached.Memory;

        public ReadOnlySpan<byte> AsReadOnlySpan() =>
            downloaded != null ? downloadedData.AsReadOnlySpan() : cached.Memory.Span;

        public SingleMemoryIterator GetMemoryIterator() =>
            new (AsReadOnlyMemory());

        public int Length => downloaded != null ? downloadedData.Length : cached.Memory.Length;

        public static implicit operator ReadOnlySpan<byte>(DownloadedOrCachedData self) =>
            self.AsReadOnlySpan();

        private sealed class MemoryManager : MemoryManager<byte>
        {
            private readonly NativeArray<byte>.ReadOnly data;

            public MemoryManager(NativeArray<byte>.ReadOnly data)
            {
                this.data = data;
            }

            protected override void Dispose(bool disposing) { }

            public override unsafe Span<byte> GetSpan() =>
                new (data.GetUnsafeReadOnlyPtr(), data.Length);

            public override unsafe MemoryHandle Pin(int elementIndex = 0) =>
                new ((byte*)data.GetUnsafeReadOnlyPtr() + elementIndex);

            public override void Unpin() { }
        }
    }
}
