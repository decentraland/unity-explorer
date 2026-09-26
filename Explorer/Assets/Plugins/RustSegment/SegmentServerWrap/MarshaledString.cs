using System;
using System.Runtime.InteropServices;

namespace Plugins.RustSegment.SegmentServerWrap
{
    public readonly struct MarshaledString : IDisposable
    {
        /// <summary>
        /// Ptr can be NULL
        /// </summary>
        public readonly IntPtr Ptr;

        public MarshaledString(string? str)
        {
            Ptr = str == null ? IntPtr.Zero : Marshal.StringToHGlobalAnsi(str);
        }

        public void Dispose()
        {
            if (Ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(Ptr);
        }
    }
}
