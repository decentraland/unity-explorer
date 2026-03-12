using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SceneRuntime.WebClient
{
    internal static class Utf8Marshal
    {
        public static unsafe IntPtr StringToHGlobalUTF8(string str)
        {
            if (str == null)
                return IntPtr.Zero;

            int byteCount = Encoding.UTF8.GetByteCount(str);
            IntPtr ptr = Marshal.AllocHGlobal(byteCount + 1);

            Span<byte> dest = new Span<byte>(ptr.ToPointer(), byteCount + 1);
            Encoding.UTF8.GetBytes(str.AsSpan(), dest);
            dest[byteCount] = 0; // null terminator

            return ptr;
        }

        public static unsafe string PtrToStringUTF8(IntPtr ptr, int length)
        {
            if (ptr == IntPtr.Zero || length <= 0)
                return string.Empty;

            return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(ptr.ToPointer(), length));
        }

        /// <summary>
        /// </summary>
        public static unsafe string PtrToStringUTF8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return string.Empty;

            byte* p = (byte*)ptr.ToPointer();
            int length = 0;

            while (p[length] != 0)
            {
                length++;

                // Safety limit to prevent infinite loop on malformed data
                if (length > 1024 * 1024)
                    break;
            }

            if (length == 0)
                return string.Empty;

            return PtrToStringUTF8(ptr, length);
        }
    }
}
