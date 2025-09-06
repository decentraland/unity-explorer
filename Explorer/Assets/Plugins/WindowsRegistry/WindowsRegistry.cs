#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Text;

public static class WindowsRegistry
{
    public static readonly IntPtr HKEY_LOCAL_MACHINE = new (unchecked((int)0x80000002));
    private const int KEY_READ = 0x20019;
    private const int REG_SZ = 1;

    private const string LIB_NAME = "advapi32.dll";

    [DllImport(LIB_NAME, CharSet = CharSet.Unicode)]
    private static extern int RegOpenKeyExW(
        IntPtr hKey,
        string subKey,
        int opt,
        int sam,
        out IntPtr phk
    );

    [DllImport(LIB_NAME, CharSet = CharSet.Unicode)]
    private static extern int RegQueryValueExW(
        IntPtr hKey,
        string name,
        IntPtr res,
        out int type,
        byte[] data,
        ref int size
    );

    [DllImport(LIB_NAME)]
    private static extern int RegCloseKey(IntPtr hKey);

    public static string? ReadString(string subKey, string value)
    {
        if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, subKey, 0, KEY_READ, out var hKey) != 0) return null;

        try
        {
            int size = 0;
            RegQueryValueExW(hKey, value, IntPtr.Zero, out int type, null!, ref size);
            if (type != REG_SZ) return null;
            var buf = new byte[size];

            return RegQueryValueExW(hKey, value, IntPtr.Zero, out type, buf, ref size) != 0
                ? null
                : Encoding.Unicode.GetString(buf).TrimEnd('\0');
        }
        finally { RegCloseKey(hKey); }
    }
}
#endif
