using System;

namespace Plugins.MachineInfo.MachineInfoWrap
{
    public static class MachineInfo
    {
        public static string UUID()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return WindowsUUID();
#endif
            return UnixUUID();
        }

        private static string WindowsUUID()
        {
            throw new NotImplementedException();
        }

        private static string UnixUUID()
        {
            throw new NotImplementedException();
        }
    }
}
