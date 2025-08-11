using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
using System.Runtime.InteropServices;
#endif

using System.Text;
using UnityEngine;

namespace Utility
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

        /// <summary>
        ///     One-shot check of the primary storage (the volume backing Application.persistentDataPath).
        ///     Returns null on failure; logs the native error when available.
        /// </summary>
        public static DriveData? GetPrimaryStorageInfoUsingPersistentPath()
        {
#if UNITY_STANDALONE_OSX
        string path = Application.persistentDataPath;
        if (StatfsUtf8(path, out var st) != 0)
        {
            int err = Marshal.GetLastWin32Error();
            Debug.LogError($"[Disk] statfs failed for '{path}' (errno {err})");
            return null;
        }

        ulong total = st.f_blocks * st.f_bsize;
        ulong avail = st.f_bavail * st.f_bsize;

        return new DriveData
        {
            Name = path,
            AvailableFreeSpace = avail,
            TotalSize = total,
        };

#elif UNITY_STANDALONE_WIN
            string path = Application.persistentDataPath;

            if (!GetDiskFreeSpaceEx(path, out ulong freeBytes, out ulong totalBytes, out _))
            {
                // Fallback to drive root (e.g., C:\) if a deep path fails
                try
                {
                    string root = Path.GetPathRoot(path) ?? "C:\\";
                    if (!GetDiskFreeSpaceEx(root, out freeBytes, out totalBytes, out _))
                    {
                        int err = Marshal.GetLastWin32Error();
                        Debug.LogError($"[Disk] GetDiskFreeSpaceEx failed for '{path}' and root '{root}' (Win32 {err})");
                        return null;
                    }

                    path = root; // report root if we fell back
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Disk] Could not resolve drive root for '{path}': {ex.Message}");
                    return null;
                }
            }

            return new DriveData
            {
                Name = path, AvailableFreeSpace = freeBytes, TotalSize = totalBytes
            };
#else
    return null;
#endif
        }

        /// <summary>
        ///     Convenience: returns true if the primary storage has at least minRequiredBytes.
        /// </summary>
        public static bool HasEnoughDiskSpace(ulong minRequiredBytes, out DriveData? driveInfo)
        {
            driveInfo = GetPrimaryStorageInfoUsingPersistentPath();
            return driveInfo != null && driveInfo.AvailableFreeSpace >= minRequiredBytes;
        }

        /// <summary>
        /// Retrieves a list of all local, fixed drives with their storage information.
        /// Uses native P/Invoke calls for high performance and reliability.
        /// </summary>
        /// <returns>A list of DriveData objects. Returns an empty list on failure or unsupported platforms.</returns>
        public static List<DriveData> GetAllDrivesInfo()
        {
            try
            {
#if UNITY_STANDALONE_WIN
                return GetWindowsDrivesInfo();
#elif UNITY_STANDALONE_OSX
                return GetMacDrivesInfo();
#else
                return new List<DriveData>();
#endif
            }
            catch (Exception e)
            {
                return new List<DriveData>();
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
        
        private static List<DriveData> GetWindowsDrivesInfo()
        {
            var allDrivesData = new List<DriveData>();
            var driveLetters = GetDrivesByBitmask();
            foreach (string driveLetter in driveLetters)
            {
                if (GetDiskFreeSpaceEx(driveLetter, out ulong freeBytes, out ulong totalBytes, out _))
                {
                    allDrivesData.Add(new DriveData
                    {
                        Name = driveLetter,
                        AvailableFreeSpace = freeBytes,
                        TotalSize = totalBytes
                    });
                }
            }
            return allDrivesData;
        }
        
        // Kernel32.dll exports GetLogicalDrives, no parameters:
        [DllImport("kernel32.dll")]
        private static extern uint GetLogicalDrives();
        /// <summary>
        /// Returns all existing drive letters (e.g. ["C:\\", "D:\\", ...]).
        /// </summary>
        private static List<string> GetDrivesByBitmask()
        {
            uint bitmask = GetLogicalDrives();
            var drives = new List<string>();
            for (int i = 0; i < 26; i++)
            {
                if ((bitmask & (1u << i)) != 0)
                    drives.Add($"{(char)('A' + i)}:\\");
            }
            return drives;
        }

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
        [StructLayout(LayoutKind.Sequential)]
        private struct StatfsSimple
        {
            public uint  f_bsize;
            public int   f_iosize;
            public ulong f_blocks;
            public ulong f_bfree;
            public ulong f_bavail;
        }

        // Raw pointer version to avoid LPStr/ANSI issues.
        [DllImport("libc", SetLastError = true, EntryPoint = "statfs")]
        private static extern int statfs_ptr(IntPtr path, out StatfsSimple st);

        // Encode path as UTF-8 + NUL and pass pointer
        private static int StatfsUtf8(string path, out StatfsSimple st)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(path + "\0");
            IntPtr p = IntPtr.Zero;
            try
            {
                p = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, p, bytes.Length);
                return statfs_ptr(p, out st);
            }
            finally
            {
                if (p != IntPtr.Zero) Marshal.FreeHGlobal(p);
            }
        }

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

         // This struct mirrors the native `statfs` structure on macOS.
         // It's used to receive file system statistics from the getfsstat call.
         // NOT USED ANYMORE, but kept for reference.
         [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
         private struct Statfs
         {
             public uint f_bsize;            // fundamental file system block size
             public int f_iosize;            // optimal transfer block size
             public ulong f_blocks;          // total data blocks in file system
             public ulong f_bfree;           // free blocks in fs
             public ulong f_bavail;          // free blocks avail to non-superuser
             public ulong f_files;           // total file nodes in file system
             public ulong f_ffree;           // free file nodes in fs
             public long f_fsid_val1;        // file system id
             public long f_fsid_val2;
             public uint f_owner;            // user that mounted the filesystem
             public uint f_type;             // type of filesystem
             public uint f_flags;            // copy of mount exported flags
             public uint f_fssubtype;        // fs sub-type (flavor)
             // The mount point path (e.g., "/" or "/Volumes/MyDisk")
             [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] // MNAMELEN = 1024 on modern macOS
             public string f_mntonname;
             // The underlying device path (e.g., "/dev/disk1s1")
             [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
             public string f_mntfromname;
         }

         // P/Invoke declaration for getfsstat, which retrieves info for all mounted file systems.
         // We use EntryPoint "getfsstat" which is correct for 64-bit systems.
         [DllImport("libc", SetLastError = true)]
         private static extern int getfsstat(IntPtr buf, int bufsize, int flags);

        private static string Utf8Z(byte[] arr)
        {
            int len = Array.IndexOf(arr, (byte)0);
            if (len < 0) len = arr.Length;
            return Encoding.UTF8.GetString(arr, 0, len);
        }

         private static List<DriveData> GetMacDrivesInfo()
        {
            var allDrivesData = new List<DriveData>();
            const int MNT_NOWAIT = 2;

            int count = getfsstat(IntPtr.Zero, 0, MNT_NOWAIT);
            if (count <= 0) return allDrivesData;

            int structSize = Marshal.SizeOf<StatfsRaw>();
            int bufferSize = count * structSize;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);

            try
            {
                count = getfsstat(buffer, bufferSize, MNT_NOWAIT);
                if (count <= 0) return allDrivesData;

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        IntPtr currentPtr = new IntPtr(buffer.ToInt64() + (i * structSize));
                        StatfsRaw raw = Marshal.PtrToStructure<StatfsRaw>(currentPtr);

                        string mntOn = Utf8Z(raw.f_mntonname);
                        if (mntOn.StartsWith("/System/Volumes/")) continue;

                        allDrivesData.Add(new DriveData
                        {
                            Name = mntOn,
                            AvailableFreeSpace = raw.f_bavail * raw.f_bsize,
                            TotalSize = raw.f_blocks * raw.f_bsize
                        });
                    }
                    catch (Exception e)
                    {
                        continue;
                    }
                }
            }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            }

            return allDrivesData;
        }

        [DllImport("libc", EntryPoint = "system")]
        private static extern int ExecuteSystemCommand([MarshalAs(UnmanagedType.LPStr)] string command);

        [DllImport("libc")]
        private static extern int strerror_r(int errnum, StringBuilder buf, int buflen);
#endif

    }
}
