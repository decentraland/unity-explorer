using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SceneRuntime.WebClient
{
    internal static class Utf8Marshal
    {
        private const int STACKALLOC_THRESHOLD = 256;

        public static unsafe IntPtr StringToHGlobalUTF8(string str)
        {
            if (str == null)
                return IntPtr.Zero;

            int byteCount = Encoding.UTF8.GetByteCount(str);
            IntPtr ptr = Marshal.AllocHGlobal(byteCount + 1);

            if (byteCount <= STACKALLOC_THRESHOLD)
            {
                Span<byte> buffer = stackalloc byte[byteCount];
                int written = Encoding.UTF8.GetBytes(str.AsSpan(), buffer);
                Span<byte> dest = new Span<byte>(ptr.ToPointer(), byteCount + 1);
                buffer[..written].CopyTo(dest);
                dest[written] = 0;
            }
            else
            {
                Span<byte> buffer = new byte[byteCount];
                int written = Encoding.UTF8.GetBytes(str.AsSpan(), buffer);
                Span<byte> dest = new Span<byte>(ptr.ToPointer(), byteCount + 1);
                buffer[..written].CopyTo(dest);
                dest[written] = 0;
            }

            return ptr;
        }

        public static unsafe string PtrToStringUTF8(IntPtr ptr, int length)
        {
            if (ptr == IntPtr.Zero || length <= 0)
                return string.Empty;

            if (length <= STACKALLOC_THRESHOLD)
            {
                Span<byte> buffer = stackalloc byte[length];
                Span<byte> src = new Span<byte>(ptr.ToPointer(), length);
                src.CopyTo(buffer);
                return Encoding.UTF8.GetString(buffer);
            }
            else
            {
                var buffer = new byte[length];
                Marshal.Copy(ptr, buffer, 0, length);
                return Encoding.UTF8.GetString(buffer);
            }
        }
        
        /// <summary>
        /// Reads a null-terminated UTF8 string from an unmanaged pointer.
        /// </summary>
        public static unsafe string PtrToStringUTF8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return string.Empty;
            
            // Find the null terminator to determine length
            byte* p = (byte*)ptr.ToPointer();
            int length = 0;
            while (p[length] != 0)
            {
                length++;
                // Safety limit to prevent infinite loop
                if (length > 1024 * 1024)
                    break;
            }
            
            if (length == 0)
                return string.Empty;
            
            return PtrToStringUTF8(ptr, length);
        }
    }
}
