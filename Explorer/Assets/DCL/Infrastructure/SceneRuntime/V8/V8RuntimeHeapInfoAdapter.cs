using Microsoft.ClearScript.V8;

namespace SceneRuntime
{
    /// <summary>
    /// Adapts ClearScript's <see cref="Microsoft.ClearScript.V8.V8RuntimeHeapInfo"/> to <see cref="IRuntimeHeapInfo"/>,
    /// exposing V8 heap memory statistics (used heap, heap limit, external size, etc.) for scene memory monitoring.
    /// </summary>
    public class V8RuntimeHeapInfoAdapter : IRuntimeHeapInfo
    {
        private readonly V8RuntimeHeapInfo v8HeapInfo;

        public V8RuntimeHeapInfoAdapter(V8RuntimeHeapInfo v8HeapInfo)
        {
            this.v8HeapInfo = v8HeapInfo;
        }

        public ulong TotalHeapSize => v8HeapInfo.TotalHeapSize;
        public ulong TotalHeapSizeExecutable => v8HeapInfo.TotalHeapSizeExecutable;
        public ulong TotalPhysicalSize => v8HeapInfo.TotalPhysicalSize;
        public ulong TotalAvailableSize => v8HeapInfo.TotalAvailableSize;
        public ulong UsedHeapSize => v8HeapInfo.UsedHeapSize;
        public ulong HeapSizeLimit => v8HeapInfo.HeapSizeLimit;
        public ulong TotalExternalSize => v8HeapInfo.TotalExternalSize;
    }
}
