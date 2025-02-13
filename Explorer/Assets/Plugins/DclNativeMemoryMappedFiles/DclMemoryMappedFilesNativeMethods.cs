using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Plugins.DclNativeMemoryMappedFiles
{
    /// <summary>
    /// IL2CPP doesn't fully support Unix functionality for Memory Mapped Files: IRC communication between user space processes
    /// </summary>
    internal static class DclMemoryMappedFilesNativeMethods
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
#else
        private const string LIB_NAME = "libDCL_NMMF.dylib";
#endif

        [StructLayout(LayoutKind.Sequential)]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
        internal struct nmmf_t
        {
            public unsafe void* memory;
            public int fd;
            public long size;
        }

        [DllImport(LIB_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern nmmf_t dcl_nmmf_new(string name, long size);

        [DllImport(LIB_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void dcl_nmmf_close(nmmf_t instance);
    }

    public readonly struct NamedMemoryMappedFile : IDisposable
    {
        private readonly DclMemoryMappedFilesNativeMethods.nmmf_t native;

        public long Size => native.size;

        public NamedMemoryMappedFile(string name, long size)
        {
            native = DclMemoryMappedFilesNativeMethods.dcl_nmmf_new(name, size);

            unsafe
            {
                if (new IntPtr(native.memory) == IntPtr.Zero)
                    throw new Exception($"Cannot create nmmf with name {name} and size {size}");
            }
        }

        public void Write(ReadOnlySpan<byte> span, int position)
        {
            if (span.Length + position > Size)
                throw new Exception($"Cannot write {span.Length} with offset {position} to nmmf with size {Size}");

            unsafe
            {
                fixed (byte* source = span)
                {
                    var destination = (byte*)native.memory;
                    destination += position;
                    Buffer.MemoryCopy(source, destination, Size - position, span.Length);
                }
            }
        }

        public void Read(int position, Span<byte> output)
        {
            if (position + output.Length > Size)
                throw new Exception($"Cannot read {output.Length} with offset {position} from nmmf with size {Size}");

            unsafe
            {
                fixed (byte* destination = output)
                {
                    var source = (byte*)native.memory;
                    source += position;
                    Buffer.MemoryCopy(source, destination, output.Length, output.Length);
                }
            }
        }

        public void Dispose()
        {
            DclMemoryMappedFilesNativeMethods.dcl_nmmf_close(native);
        }
    }
}
