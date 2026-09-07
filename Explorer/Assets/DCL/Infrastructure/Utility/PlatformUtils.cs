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
            public ulong AvailableFreeSpace { get; set; }
            public ulong TotalSize { get; set; }

            public override string ToString()
            {
                double freeGB = (double)AvailableFreeSpace / (1024 * 1024 * 1024);
                double totalGB = (double)TotalSize / (1024 * 1024 * 1024);
                return $"Free Space: {freeGB:F2} GB, Total Size: {totalGB:F2} GB";
            }
        }

        // The shader bundles the Linux build ships, compiled for its graphics APIs under the Windows bundle identity.
        private static readonly (string windows, string linux)[] LINUX_SHADER_BUNDLES =
        {
            ("dcl/scene_ignore_windows", "dcl/scene_ignore_linux"),
            ("dcl/scene_texarray_ignore_windows", "dcl/scene_texarray_ignore_linux"),
        };

        private static string? platformSuffix;

        /// <summary>
        ///     Linux consumes Windows content bundles, whose materials bind to the scene shaders through the Windows
        ///     bundle identity (internal CAB name and object ids). The scene and LOD texture-array shader bundles are
        ///     opened from the files compiled for Linux graphics APIs under that identity. Content bundles, the legacy
        ///     URP Lit bundle (no Linux build) and other platforms open the name as given.
        /// </summary>
        public static string GetEmbeddedShaderBundleName(string bundleName) =>
            GetEmbeddedShaderBundleName(bundleName, Application.platform);

        public static string GetEmbeddedShaderBundleName(string bundleName, RuntimePlatform platform)
        {
            if (platform is not (RuntimePlatform.LinuxEditor or RuntimePlatform.LinuxPlayer))
                return bundleName;

            foreach ((string windows, string linux) in LINUX_SHADER_BUNDLES)
                if (bundleName.Equals(windows, StringComparison.OrdinalIgnoreCase))
                    return linux;

            return bundleName;
        }

        public static string GetCurrentPlatform()
        {
            if (platformSuffix == null)
            {
                // Golden-capture parity override: asset-bundle URLs carry this platform suffix,
                // so forcing another platform's suffix makes every bundle fetch miss and the
                // client fall back to raw GLTF conversion — the only content pipeline that exists
                // on platforms without CDN bundles. Cross-platform goldens need both machines on
                // that same pipeline.
                platformSuffix = Environment.GetEnvironmentVariable("DCL_GOLDEN_AB_PLATFORM");

                if (platformSuffix != null)
                    return platformSuffix;

                if (Application.platform is RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer)
                    platformSuffix = "_windows";
                else if (Application.platform is RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer)
                    platformSuffix = "_mac";
                else if (Application.platform is RuntimePlatform.LinuxEditor or RuntimePlatform.LinuxPlayer)

                    // The CDN builds no Linux bundles; desktop asset bundles are cross-platform
                    // except shaders, which the client strips and replaces from its local
                    // scene_ignore bundle anyway (AssetBundleManifestVersion reads the windows
                    // manifest for Linux on the same grounds). Requesting "_linux" made every
                    // fetch 404 and silently dropped the whole platform to raw GLTF conversion.
                    platformSuffix = "_windows";
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

            // Platform #if directives resolve to the active build target, not the OS the process
            // runs on: a Linux editor (or a Linux player produced from a non-Linux target build)
            // would otherwise compile the kernel32/libc native path and fail at runtime. Resolve
            // via managed DriveInfo (statvfs under Mono) whenever the runtime is actually Linux.
            if (Application.platform is RuntimePlatform.LinuxEditor or RuntimePlatform.LinuxPlayer)
                return GetManagedDriveInfoForPath(path);

            try
            {
#if UNITY_STANDALONE_WIN
                if (GetDiskFreeSpaceEx(path, out ulong freeBytes, out ulong totalBytes, out _))
                {
                    return new DriveData
                    {
                        AvailableFreeSpace = freeBytes,
                        TotalSize = totalBytes
                    };
                }

                return null;
#elif UNITY_STANDALONE_OSX
                return GetMacDriveInfoForPath(path);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                return GetManagedDriveInfoForPath(path);
#else
                return null;
#endif
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Queries the volume that actually hosts the path via statvfs, the same way the Windows and macOS
        // branches query the path directly. Path.GetPathRoot collapses to "/", which under the editor's
        // FHS sandbox is a small overlay rather than the bind-mounted volume that holds the path.
        private static DriveData? GetManagedDriveInfoForPath(string path)
        {
            try
            {
                if (statvfs(path, out StatvfsRaw vfs) != 0)
                    return null;

                ulong unit = vfs.f_frsize != 0 ? vfs.f_frsize : vfs.f_bsize;

                return new DriveData
                {
                    AvailableFreeSpace = vfs.f_bavail * unit,
                    TotalSize = vfs.f_blocks * unit,
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        // glibc statvfs on the Linux x86_64 ABI: every field is unsigned long (8 bytes) and fsblkcnt_t is
        // 64-bit, so no _FILE_OFFSET_BITS variant is needed. Only the block counts are read.
        [StructLayout(LayoutKind.Sequential)]
        private struct StatvfsRaw
        {
            public ulong f_bsize;
            public ulong f_frsize;
            public ulong f_blocks;
            public ulong f_bfree;
            public ulong f_bavail;
            public ulong f_files;
            public ulong f_ffree;
            public ulong f_favail;
            public ulong f_fsid;
            public ulong f_flag;
            public ulong f_namemax;
            private readonly int spare0;
            private readonly int spare1;
            private readonly int spare2;
            private readonly int spare3;
            private readonly int spare4;
            private readonly int spare5;
        }

        [DllImport("libc", SetLastError = true, EntryPoint = "statvfs")]
        private static extern int statvfs(string path, out StatvfsRaw buf);

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
#elif UNITY_STANDALONE_LINUX
            // Launch through /bin/sh so the child survives the client exiting. The 3s delay lets the
            // current player release locks held by the launcher wrapper (e.g. the NixOS flock) before
            // the replacement launcher starts: the caller exits the client ~2s after spawning us.
            var psi = new System.Diagnostics.ProcessStartInfo("/bin/sh", "-c \"sleep 3; exec '" + fileName + "'\"")
            {
                UseShellExecute = false,
            };

            if (System.Diagnostics.Process.Start(psi) == null)
                throw new Exception($"Failed to start {fileName}");
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

        // Mirrors the native 64-bit-inode `struct statfs` on macOS. Only the block-count fields
        // are read, but every field (including the trailing name/reserved buffers) must be present
        // so the struct matches the native size — statfs writes the whole struct, and a short
        // buffer would corrupt adjacent stack memory. Uses fixed buffers so the struct is blittable
        // and can be stack-allocated and passed by pointer with no heap marshalling.
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private unsafe struct StatfsRaw
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
            public fixed byte f_fstypename[16];
            public fixed byte f_mntonname[1024];
            public fixed byte f_mntfromname[1024];
            public fixed byte f_reserved[32];
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int statfs(string path, out StatfsRaw buf);

        private static DriveData? GetMacDriveInfoForPath(string path)
        {
            if (statfs(path, out StatfsRaw raw) != 0)
                return null;

            return new DriveData
            {
                AvailableFreeSpace = raw.f_bavail * raw.f_bsize,
                TotalSize = raw.f_blocks * raw.f_bsize
            };
        }

        [DllImport("libc", EntryPoint = "system")]
        private static extern int ExecuteSystemCommand([MarshalAs(UnmanagedType.LPStr)] string command);

        [DllImport("libc")]
        private static extern int strerror_r(int errnum, StringBuilder buf, int buflen);
#endif

    }
}
