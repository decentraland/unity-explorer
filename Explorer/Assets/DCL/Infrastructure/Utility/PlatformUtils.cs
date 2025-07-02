using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
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
                //return GetMacDrivesInfo();
                return new List<DriveData>();
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
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                {
                    if (GetDiskFreeSpaceEx(drive.Name, out ulong freeBytes, out ulong totalBytes, out _))
                    {
                        allDrivesData.Add(new DriveData
                        {
                            Name = drive.Name,
                            AvailableFreeSpace = freeBytes,
                            TotalSize = totalBytes
                        });
                    }
                }
            }
            return allDrivesData;
        }
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
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
        [DllImport("libc", EntryPoint = "system")]
        private static extern int ExecuteSystemCommand([MarshalAs(UnmanagedType.LPStr)] string command);

        [DllImport("libc")]
        private static extern int strerror_r(int errnum, StringBuilder buf, int buflen);
#endif

    }
}
