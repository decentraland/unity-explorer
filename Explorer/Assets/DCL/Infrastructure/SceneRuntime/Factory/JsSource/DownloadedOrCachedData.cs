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
        private readonly SlicedOwnedMemory<byte> cached;

        public DownloadedOrCachedData(DownloadHandler downloadHandler)
        {
            downloaded = downloadHandler;
            cached = default;
        }

        public DownloadedOrCachedData(SlicedOwnedMemory<byte> memory)
        {
            downloaded = null;
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
            downloaded != null ? new MemoryManager(downloaded).Memory : cached.Memory;

        public ReadOnlySpan<byte> AsReadOnlySpan() =>
            downloaded != null ? downloaded.nativeData.AsReadOnlySpan() : cached.Memory.Span;

        public SingleMemoryIterator GetMemoryIterator() =>
            new (AsReadOnlyMemory());

        public int Length => downloaded != null ? downloaded.nativeData.Length : cached.Memory.Length;

        public static implicit operator ReadOnlySpan<byte>(DownloadedOrCachedData self) =>
            self.AsReadOnlySpan();

        private sealed class MemoryManager : MemoryManager<byte>
        {
            private readonly DownloadHandler downloadHandler;

            public MemoryManager(DownloadHandler downloadHandler)
            {
                this.downloadHandler = downloadHandler;
            }

            protected override void Dispose(bool disposing) { }

            public override unsafe Span<byte> GetSpan()
            {
                NativeArray<byte>.ReadOnly nativeData = downloadHandler.nativeData;
                return new Span<byte>(nativeData.GetUnsafeReadOnlyPtr(), nativeData.Length);
            }

            public override unsafe MemoryHandle Pin(int elementIndex = 0) =>
                new ((byte*)downloadHandler.nativeData.GetUnsafeReadOnlyPtr() + elementIndex);

            public override void Unpin() { }
        }
    }
}
