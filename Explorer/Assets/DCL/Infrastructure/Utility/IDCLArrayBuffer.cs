namespace Utility
{
    public interface IDCLArrayBuffer
    {
        ulong Size { get; }
        ulong ReadBytes(ulong index, ulong length, byte[] destination, ulong destinationIndex);
    }
}
