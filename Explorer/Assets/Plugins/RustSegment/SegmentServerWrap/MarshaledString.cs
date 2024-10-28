using System;
using System.Runtime.InteropServices;

namespace Plugins.RustSegment.SegmentServerWrap
{
    public readonly struct MarshaledString : IDisposable
    {
        public readonly IntPtr Ptr;

        public MarshaledString(string str)
        {
            Ptr = Marshal.StringToHGlobalAnsi(str);
        }

        public void Dispose() =>
            Marshal.FreeHGlobal(Ptr);
    }
}
