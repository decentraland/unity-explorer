using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Buffers;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Networking;

namespace SceneRuntime.Factory.JsSource
{
    /// <summary>
    /// Code gymnastics to turn a downloaded buffer into ReadOnlyMemory&lt;byte&gt; without copying the
    /// whole thing.
    /// </summary>
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

        public ReadOnlyMemory<byte> AsReadOnlyMemory() =>
            downloaded != null ? MemoryManager.GetMemory(downloaded) : cached.Memory;

        public ReadOnlySpan<byte> AsReadOnlySpan() =>
            downloaded != null ? downloaded.nativeData.AsReadOnlySpan() : cached.Memory.Span;

        public MemoryIterator GetMemoryIterator() =>
            new (AsReadOnlyMemory());

        public int Length => downloaded != null ? downloaded.nativeData.Length : cached.Memory.Length;

        public static implicit operator ReadOnlySpan<byte>(DownloadedOrCachedData self) =>
            self.AsReadOnlySpan();

        public struct MemoryIterator : IMemoryIterator
        {
            private int index;

            internal MemoryIterator(ReadOnlyMemory<byte> memory)
            {
                Current = memory;
                index = -1;
            }

            public void Dispose() { }

            public ReadOnlyMemory<byte> Current { get; }

            public int? TotalSize => Current.Length;

            public bool MoveNext() =>
                index++ < 0;
        }

        private sealed class MemoryManager : MemoryManager<byte>
        {
            private DownloadHandler downloadHandler;
            private static readonly MemoryManager INSTANCE = new ();

            public static Memory<byte> GetMemory(DownloadHandler downloadHandler)
            {
                INSTANCE.downloadHandler = downloadHandler;
                return INSTANCE.Memory;
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
