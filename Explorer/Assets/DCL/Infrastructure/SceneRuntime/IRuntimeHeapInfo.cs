namespace SceneRuntime
{
    public interface IRuntimeHeapInfo
    {
        ulong TotalHeapSize { get; }
        ulong TotalHeapSizeExecutable { get; }
        ulong TotalPhysicalSize { get; }
        ulong TotalAvailableSize { get; }
        ulong UsedHeapSize { get; }
        ulong HeapSizeLimit { get; }
        ulong TotalExternalSize { get; }
    }
}
