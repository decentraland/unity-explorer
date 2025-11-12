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
            downloaded != null ? MemoryManager.GetMemory(downloaded) : cached.Memory;

        public ReadOnlySpan<byte> AsReadOnlySpan() =>
            downloaded != null ? downloaded.nativeData.AsReadOnlySpan() : cached.Memory.Span;

        public SingleMemoryIterator GetMemoryIterator() =>
            new (AsReadOnlyMemory());

        public int Length => downloaded != null ? downloaded.nativeData.Length : cached.Memory.Length;

        public static implicit operator ReadOnlySpan<byte>(DownloadedOrCachedData self) =>
            self.AsReadOnlySpan();

        private sealed class MemoryManager : MemoryManager<byte>
        {
            private DownloadHandler? downloadHandler;
            private static readonly MemoryManager INSTANCE = new ();

            public static Memory<byte> GetMemory(DownloadHandler downloadHandler)
            {
                // Evil code gymnastics to turn a span into a memory because async methods cannot work
                // with spans because they're ref structs.
                INSTANCE.downloadHandler = downloadHandler;
                Memory<byte> memory = INSTANCE.Memory;
                INSTANCE.downloadHandler = null;
                return memory;
            }

            protected override void Dispose(bool disposing) { }

            public override unsafe Span<byte> GetSpan()
            {
                NativeArray<byte>.ReadOnly nativeData = downloadHandler!.nativeData;
                return new Span<byte>(nativeData.GetUnsafeReadOnlyPtr(), nativeData.Length);
            }

            public override unsafe MemoryHandle Pin(int elementIndex = 0) =>
                new ((byte*)downloadHandler!.nativeData.GetUnsafeReadOnlyPtr() + elementIndex);

            public override void Unpin() { }
        }
    }
}
