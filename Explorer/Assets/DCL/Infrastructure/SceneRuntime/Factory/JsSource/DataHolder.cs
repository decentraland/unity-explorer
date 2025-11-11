using ECS.StreamableLoading.Cache.Disk;
using System;
using UnityEngine.Networking;

namespace SceneRuntime.Factory.JsSource
{
    /// <remarks>
    /// This should be a ref struct, but we use async. Please treat it like a disposable ref struct.
    /// </remarks>
    public readonly struct DataHolder : IDisposable
    {
        private readonly DownloadHandler? downloaded;
        private readonly SlicedOwnedMemory<byte> cached;

        internal DataHolder(DownloadHandler source)
        {
            downloaded = source;
            cached = default;
        }

        internal DataHolder(SlicedOwnedMemory<byte> source)
        {
            downloaded = null;
            cached = source;
        }

        public void Dispose()
        {
            if  (downloaded != null)
                downloaded.Dispose();
            else
                cached.Dispose();
        }

        public static implicit operator ReadOnlySpan<byte>(DataHolder self) =>
            self.downloaded != null
                ? self.downloaded.nativeData.AsReadOnlySpan() : self.cached.Memory.Span;

        public ReadOnlySpan<byte> AsReadOnlySpan() =>
            this;

        public int Length => downloaded != null ? downloaded.nativeData.Length : cached.Memory.Length;
    }
}
