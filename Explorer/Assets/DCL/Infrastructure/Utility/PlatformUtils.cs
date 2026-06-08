using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
#endif

namespace DCL.Utility
{
    public static class PlatformUtils
    {
        public class DriveData
        {
            public string Name { get; set; }
            public ulong AvailableFreeSpace { get; set; }
            public ulong TotalSize { get; set; }

            public override string ToString()
            {
                double freeGB = (double)AvailableFreeSpace / (1024 * 1024 * 1024);
                double totalGB = (double)TotalSize / (1024 * 1024 * 1024);
                return $"{Name} - Free Space: {freeGB:F2} GB, Total Size: {totalGB:F2} GB";
            }
        }

        private static string? platformSuffix;

        public static string GetCurrentPlatform()
        {
            if (platformSuffix == null)
            {
                if (Application.platform is RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer)
                    platformSuffix = "_windows";
                else if (Application.platform is RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer)
                    platformSuffix = "_mac";
                else if (Application.platform is RuntimePlatform.LinuxEditor or RuntimePlatform.LinuxPlayer)
                    platformSuffix = "_linux";
                else
                    platformSuffix = string.Empty; // WebGL requires no platform suffix
            }

            return platformSuffix;
        }

        public static DriveData? GetPrimaryStorageInfoUsingPersistentPath()
        {
            string path = Application.persistentDataPath;
            string root = Path.GetPathRoot(path);

            try
            {
                var drive = new DriveInfo(root);
                return new DriveData
                {
                    Name = path,
                    AvailableFreeSpace = (ulong)drive.AvailableFreeSpace,
                    TotalSize = (ulong)drive.TotalSize
                };
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieves storage information for the single drive/volume that hosts the given path.
        /// Performs one native query for the relevant volume only, avoiding the cost of
        /// enumerating every mounted drive (which can be very slow when network drives are present).
        /// </summary>
        /// <returns>The drive data, or null on failure or unsupported platforms.</returns>
        public static DriveData? GetDriveInfoForPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            try
            {
#if UNITY_STANDALONE_WIN
                if (GetDiskFreeSpaceEx(path, out ulong freeBytes, out ulong totalBytes, out _))
                {
                    return new DriveData
                    {
                        Name = path,
                        AvailableFreeSpace = freeBytes,
                        TotalSize = totalBytes
                    };
                }

                return null;
#elif UNITY_STANDALONE_OSX
                return GetMacDriveInfoForPath(path);
#else
                return null;
#endif
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void ShellExecute(string fileName)
        {
#if UNITY_STANDALONE_WIN
            if ((int)ShellExecute(IntPtr.Zero, null, fileName, null, null, SW_NORMAL) <= 16)
            {
                int error = Marshal.GetLastWin32Error();
                var sb = new StringBuilder(1024);

                uint length = FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM, IntPtr.Zero, error, 0, sb,
                    sb.Capacity, IntPtr.Zero);

                string message = length > 0 ? sb.ToString() : $"error {error}";
                throw new Win32Exception(error, message.TrimEnd());
            }
#elif UNITY_STANDALONE_OSX
            string cmd = "open \"" + fileName + "\"";
            int code = ExecuteSystemCommand(cmd);
            if (code != 0)
            {
                var sb = new StringBuilder(1024);

                string message = code == -1 && strerror_r(code, sb, sb.Capacity) == 0
                    ? sb.ToString()
                    : "Unknown error";
                throw new Exception($"error {code}: {message}");
            }
#else
            throw new NotImplementedException();
#endif
        }


#if UNITY_STANDALONE_WIN

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint FormatMessage(uint dwFlags, IntPtr lpSource, int dwMessageId,
            uint dwLanguageId, StringBuilder lpBuffer, int nSize, IntPtr Arguments);

        private const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;

        [DllImport("shell32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr ShellExecute(IntPtr hWnd, string lpOperation, string lpFile,
            string lpParameters, string lpDirectory, int nShowCmd);

        private const int SW_NORMAL = 1;

#elif UNITY_STANDALONE_OSX

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct StatfsRaw
        {
            public uint  f_bsize;
            public int   f_iosize;
            public ulong f_blocks;
            public ulong f_bfree;
            public ulong f_bavail;
            public ulong f_files;
            public ulong f_ffree;
            public long  f_fsid_val1;
            public long  f_fsid_val2;
            public uint  f_owner;
            public uint  f_type;
            public uint  f_flags;
            public uint  f_fssubtype;

            // NUL-terminated UTF-8 byte arrays (no ANSI marshalling)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)] public byte[] f_mntonname;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)] public byte[] f_mntfromname;
        }

        private static string Utf8Z(byte[] arr)
        {
            int len = Array.IndexOf(arr, (byte)0);
            if (len < 0) len = arr.Length;
            return Encoding.UTF8.GetString(arr, 0, len);
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int statfs(string path, IntPtr buf);

        private static DriveData? GetMacDriveInfoForPath(string path)
        {
            int structSize = Marshal.SizeOf<StatfsRaw>();
            IntPtr buffer = Marshal.AllocHGlobal(structSize);

            try
            {
                if (statfs(path, buffer) != 0)
                    return null;

                StatfsRaw raw = Marshal.PtrToStructure<StatfsRaw>(buffer);

                return new DriveData
                {
                    Name = Utf8Z(raw.f_mntonname),
                    AvailableFreeSpace = raw.f_bavail * raw.f_bsize,
                    TotalSize = raw.f_blocks * raw.f_bsize
                };
            }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            }
        }

        [DllImport("libc", EntryPoint = "system")]
        private static extern int ExecuteSystemCommand([MarshalAs(UnmanagedType.LPStr)] string command);

        [DllImport("libc")]
        private static extern int strerror_r(int errnum, StringBuilder buf, int buflen);
#endif

    }
}
