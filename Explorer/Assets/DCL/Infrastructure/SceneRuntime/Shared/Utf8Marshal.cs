#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
using System;
using System.Runtime.InteropServices;
using System.Text;
using DCL.Diagnostics;

namespace SceneRuntime.WebClient
{
    /// <summary>
    ///     Low-level helpers for marshalling strings across the C#/jslib P/Invoke boundary as null-terminated UTF-8 buffers.
    ///     <para>
    ///         <see cref="StringToHGlobalUTF8" /> allocates unmanaged memory that the caller must free with
    ///         <c>Marshal.FreeHGlobal</c> after the P/Invoke call returns.
    ///         <see cref="PtrToStringUTF8(IntPtr,int)" /> reads a known-length buffer;
    ///         <see cref="PtrToStringUTF8(IntPtr)" /> scans for the null terminator with a 1 MB safety cap.
    ///     </para>
    /// </summary>
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
        ///     Reads a null-terminated UTF-8 string from an unmanaged pointer by scanning for the null byte.
        ///     Truncates at 1 MB to prevent runaway reads on malformed data.
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
#endif
