using System.Runtime.InteropServices;

namespace Plugins.MachineInfo.MachineInfoWrap
{
    public static class NativeMethods
    {
        private const string LIBRARY_NAME = "machine-info";

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "machine_info_uuid")]
        internal static extern string MachineInfoUUID();
    }
}
