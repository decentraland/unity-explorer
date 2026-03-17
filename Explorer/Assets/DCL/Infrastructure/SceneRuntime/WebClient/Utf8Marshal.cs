using System;
using System.Runtime.InteropServices;
using System.Text;
using DCL.Diagnostics;

namespace SceneRuntime.WebClient
{
    internal static class Utf8Marshal
    {
        public static unsafe IntPtr StringToHGlobalUTF8(string str)
        {
            if (string.IsNullOrEmpty(str))
                return IntPtr.Zero;

            int byteCount = Encoding.UTF8.GetByteCount(str);
            IntPtr ptr = Marshal.AllocHGlobal(byteCount + 1);

            var dest = new Span<byte>(ptr.ToPointer(), byteCount + 1);
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

            var p = (byte*)ptr.ToPointer();
            var length = 0;

            while (p[length] != 0)
            {
                length++;

                // Safety limit to prevent infinite loop on malformed data
                if (length > 1024 * 1024)
                {
                    ReportHub.Log(ReportCategory.WEB_CLIENT, $"[Utf8Marshal] PtrToStringUTF8: string exceeded 1 MB safety limit and was truncated at {length} bytes.");
                    break;
                }
            }

            if (length == 0)
                return string.Empty;

            return PtrToStringUTF8(ptr, length);
        }
    }
}
