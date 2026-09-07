#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace DCL.VideoPlayback
{
    /// <summary>
    /// Named-pipe plumbing for the decoder child process: creation under the temp
    /// directory, a read end that opens without waiting for a writer, and unlinking.
    /// The descriptor is driven through libc directly because the runtime's own
    /// file streams only accept handles they opened themselves.
    /// </summary>
    internal static class PosixFifo
    {
        private const int O_RDONLY = 0;
        private const int O_NONBLOCK = 0x800;
        private const int F_GETFL = 3;
        private const int F_SETFL = 4;
        private const int EINTR = 4;
        private const int EAGAIN = 11;
        private const uint OWNER_READ_WRITE = 0x180;

        [DllImport("libc", SetLastError = true)]
        private static extern int mkfifo(string path, uint mode);

        [DllImport("libc", SetLastError = true)]
        private static extern int open(string path, int flags);

        [DllImport("libc", SetLastError = true)]
        private static extern int fcntl(int fd, int cmd, int arg);

        [DllImport("libc", SetLastError = true)]
        private static extern IntPtr read(int fd, ref byte buffer, IntPtr count);

        [DllImport("libc", SetLastError = true)]
        private static extern int close(int fd);

        public static string Create(string suffix)
        {
            string path = Path.Combine(Path.GetTempPath(), $"videoplayback-linux-{Guid.NewGuid():N}.{suffix}");

            if (mkfifo(path, OWNER_READ_WRITE) != 0)
                throw new IOException($"mkfifo({path}) failed with errno {Marshal.GetLastWin32Error()}");

            return path;
        }

        /// <summary>Unlinks the FIFO; false when it is already gone or cannot be removed.</summary>
        public static bool Delete(string path)
        {
            if (!File.Exists(path)) return false;

            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Read end of a FIFO. Opening never waits for a writer; afterwards the
        /// descriptor is switched back to blocking mode so reads sleep on data
        /// instead of spinning. Before a writer attaches, and after the last one
        /// closes, reads return zero bytes.
        /// </summary>
        public sealed class Reader : IDisposable
        {
            private int fd;

            public Reader(string path)
            {
                fd = open(path, O_RDONLY | O_NONBLOCK);

                if (fd < 0)
                    throw new IOException($"open({path}) failed with errno {Marshal.GetLastWin32Error()}");

                int flags = fcntl(fd, F_GETFL, 0);

                if (flags < 0 || fcntl(fd, F_SETFL, flags & ~O_NONBLOCK) < 0)
                {
                    int errno = Marshal.GetLastWin32Error();
                    close(fd);
                    fd = -1;
                    throw new IOException($"fcntl({path}) failed with errno {errno}");
                }
            }

            public int Read(byte[] buffer, int offset, int count)
            {
                if (count <= 0) return 0;
                if (offset < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

                while (true)
                {
                    int descriptor = fd;
                    if (descriptor < 0) throw new ObjectDisposedException(nameof(Reader));

                    long got = read(descriptor, ref buffer[offset], (IntPtr)count).ToInt64();
                    if (got >= 0) return (int)got;

                    int errno = Marshal.GetLastWin32Error();

                    if (errno == EINTR) continue;

                    if (errno == EAGAIN)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    throw new IOException($"read on fifo failed with errno {errno}");
                }
            }

            public void Dispose()
            {
                int descriptor = fd;
                fd = -1;
                if (descriptor >= 0) close(descriptor);
            }
        }
    }
}
#endif
