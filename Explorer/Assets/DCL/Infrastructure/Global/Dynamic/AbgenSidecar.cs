#nullable enable

using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace Global.Dynamic
{
    /// <summary>
    ///     Runs the abgen JIT asset-bundle server as a supervised localhost sidecar. The client's unchanged
    ///     loading path consumes its base URL as <c>DecentralandUrl.AssetBundlesCDN</c>; the server
    ///     read-throughs the production CDN and converts only what the CDN lacks, caching bundles on disk.
    ///     The binary is probed in the download cache, then StreamingAssets; when absent it is fetched from
    ///     the pinned release in the background and the sidecar activates on the next launch. An explicit
    ///     --optimized-assets-url always takes precedence.
    /// </summary>
    public sealed class AbgenSidecar : IDisposable
    {
        private const string VERSION = "0.15.4";
        private const int MAX_RESTARTS = 3;
        private const int HEALTH_TIMEOUT_MS = 15000;
        private const int HEALTH_POLL_MS = 250;

        private static bool downloadStarted;

        private readonly string catalystContentUrl;
        private readonly string upstreamCdnUrl;
        private readonly string cacheRoot;

        private Process? process;
        private int restarts;
        private volatile bool disposed;

        public string BaseUrl { get; }

        private AbgenSidecar(int port, string catalystContentUrl, string upstreamCdnUrl, string cacheRoot)
        {
            BaseUrl = $"http://127.0.0.1:{port}";
            this.catalystContentUrl = catalystContentUrl;
            this.upstreamCdnUrl = upstreamCdnUrl;
            this.cacheRoot = cacheRoot;
        }

        public static string DownloadedExecutablePath =>
            Path.Combine(Application.persistentDataPath, "abgen", "bin", VERSION, $"abgen-v{VERSION}-{Platform()?.target}", IsWindows ? "abgen.exe" : "abgen");

        public static string StreamingAssetsExecutablePath =>
            Path.Combine(Application.streamingAssetsPath, IsWindows ? "abgen.exe" : "abgen");

        private static bool IsWindows => Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor;

        /// <summary>
        ///     Returns null when no binary is available yet (a background download is started — the sidecar
        ///     activates on the next launch) or the server never became healthy.
        /// </summary>
        public static async UniTask<AbgenSidecar?> TryStartAsync(string environmentDomain, CancellationToken ct, string? cacheRoot = null)
        {
            string exe = File.Exists(DownloadedExecutablePath) ? DownloadedExecutablePath
                : File.Exists(StreamingAssetsExecutablePath) ? StreamingAssetsExecutablePath : string.Empty;

            if (exe.Length == 0)
            {
                if (!downloadStarted && Platform() != null)
                {
                    downloadStarted = true;
                    DownloadBinaryAsync().Forget();
                }

                return null;
            }

            var sidecar = new AbgenSidecar(FreeLoopbackPort(),
                $"https://peer.decentraland.{environmentDomain}/content",
                $"https://ab-cdn.decentraland.{environmentDomain}",
                cacheRoot ?? Path.Combine(Application.persistentDataPath, "abgen"));

            if (sidecar.Launch(exe) && await sidecar.WaitHealthyAsync(ct))
                return sidecar;

            sidecar.Dispose();
            return null;
        }

        public void Dispose()
        {
            disposed = true;
            TryKill(process);
            process = null;
        }

        /// <summary>Release target triple and pinned archive sha256 for the current platform; null when unsupported.</summary>
        private static (string target, string sha256)? Platform() =>
            Application.platform switch
            {
                RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => ("x86_64-pc-windows-gnu", "2f94dd06e2940c9159abce896d94dc477768b83b5ea3b37a422d7593168e4418"),
                RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? ("aarch64-apple-darwin", "6f1f230c6a435cb92339c7238cfb9b857e18ae27f38fa93dec962c16a68563fe")
                    : ("x86_64-apple-darwin", "91d6f2a2fac5aa269e0ea2f1e6052b5acdf048dbf10c70c5e3fc118cb7c0a1ef"),
                RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor => ("x86_64-unknown-linux-gnu", "d1745095cb072b22cfaf5db9b05d4477b21f1a761c0258d828c1ef7bb7976b0e"),
                _ => null,
            };

        private static async UniTaskVoid DownloadBinaryAsync()
        {
            try
            {
                (string target, string sha256) = Platform()!.Value;
                string url = $"https://github.com/decentraland/abgen/releases/download/v{VERSION}/abgen-v{VERSION}-{target}.tar.gz";

                using UnityWebRequest req = UnityWebRequest.Get(url);
                req.timeout = 600;
                try { await req.SendWebRequest(); } catch { /* result checked below */ }

                if (req.result != UnityWebRequest.Result.Success)
                    throw new IOException($"abgen archive download failed: {req.error}");

                byte[] archive = req.downloadHandler.data;

                using (var sha = SHA256.Create())
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(archive)).Replace("-", "").ToLowerInvariant();

                    if (actual != sha256)
                        throw new IOException($"abgen archive checksum mismatch: {actual}");
                }

                await UniTask.RunOnThreadPool(() =>
                {
                    string finalDir = Path.Combine(Application.persistentDataPath, "abgen", "bin", VERSION);
                    string tmpDir = finalDir + ".tmp";
                    if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
                    ExtractTarGz(archive, tmpDir);

                    if (!IsWindows)
                        Process.Start(new ProcessStartInfo { FileName = "chmod", Arguments = $"-R a+rx \"{tmpDir}\"", UseShellExecute = false })?.WaitForExit();

                    if (Directory.Exists(finalDir)) Directory.Delete(finalDir, true);
                    Directory.Move(tmpDir, finalDir);
                });

                ReportHub.Log(ReportCategory.ASSET_BUNDLES, $"abgen sidecar binary v{VERSION} downloaded; activates next launch");
            }
            catch (Exception e)
            {
                downloadStarted = false;
                ReportHub.LogException(e, ReportCategory.ASSET_BUNDLES);
            }
        }

        /// <summary>Minimal ustar reader: extracts regular files and directories, preserving relative paths.</summary>
        private static void ExtractTarGz(byte[] archive, string destination)
        {
            using var gz = new GZipStream(new MemoryStream(archive), CompressionMode.Decompress);
            var header = new byte[512];

            while (ReadBlock(gz, header) && header[0] != 0)
            {
                string name = ReadString(header, 0, 100);
                string prefix = ReadString(header, 345, 155);
                if (prefix.Length > 0) name = prefix + "/" + name;
                long size = Convert.ToInt64(ReadString(header, 124, 12).Trim(), 8);
                byte type = header[156];
                string path = Path.Combine(destination, name);

                if (type == (byte)'5')
                    Directory.CreateDirectory(path);
                else if (type is (byte)'0' or 0 && size >= 0)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                    using FileStream file = File.Create(path);
                    var buffer = new byte[81920];
                    long remaining = size;

                    while (remaining > 0)
                    {
                        int n = gz.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                        if (n == 0) throw new IOException("truncated tar entry");
                        file.Write(buffer, 0, n);
                        remaining -= n;
                    }
                }
                else
                    SkipBytes(gz, size);

                SkipBytes(gz, (512 - (size % 512)) % 512);
            }
        }

        private static bool ReadBlock(Stream stream, byte[] block)
        {
            var read = 0;

            while (read < block.Length)
            {
                int n = stream.Read(block, read, block.Length - read);
                if (n == 0) return false;
                read += n;
            }

            return true;
        }

        private static void SkipBytes(Stream stream, long count)
        {
            var buffer = new byte[512];

            while (count > 0)
            {
                int n = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
                if (n == 0) return;
                count -= n;
            }
        }

        private static string ReadString(byte[] block, int offset, int length)
        {
            int end = offset;
            while (end < offset + length && block[end] != 0) end++;
            return System.Text.Encoding.UTF8.GetString(block, offset, end - offset);
        }

        private bool Launch(string executablePath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            psi.EnvironmentVariables["HTTP_SERVER_HOST"] = "127.0.0.1";
            psi.EnvironmentVariables["HTTP_SERVER_PORT"] = new Uri(BaseUrl).Port.ToString();
            psi.EnvironmentVariables["ABGEN_CACHE_DIR"] = Path.Combine(cacheRoot, "cache");
            psi.EnvironmentVariables["ABGEN_OUT_ROOT"] = Path.Combine(cacheRoot, "out");
            psi.EnvironmentVariables["ABGEN_CATALYST_URL"] = catalystContentUrl;
            psi.EnvironmentVariables["ABGEN_UPSTREAM_AB_CDN"] = upstreamCdnUrl;

            try
            {
                process = Process.Start(psi);
                if (process == null) return false;

                // Drain pipes so the child never blocks on a full stdout/stderr buffer.
                process.OutputDataReceived += static (_, _) => { };
                process.ErrorDataReceived += static (_, _) => { };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.EnableRaisingEvents = true;
                process.Exited += (_, _) => OnExited(executablePath);
                return true;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.ASSET_BUNDLES);
                return false;
            }
        }

        private void OnExited(string executablePath)
        {
            if (disposed || Interlocked.Increment(ref restarts) > MAX_RESTARTS)
                return;

            ReportHub.LogWarning(ReportCategory.ASSET_BUNDLES, $"abgen sidecar exited; restart {restarts}/{MAX_RESTARTS}");

            // Same port on purpose: consumers already hold BaseUrl.
            if (!Launch(executablePath))
                ReportHub.LogWarning(ReportCategory.ASSET_BUNDLES, "abgen sidecar restart failed; asset bundles fall back to direct CDN errors");
        }

        private async UniTask<bool> WaitHealthyAsync(CancellationToken ct)
        {
            float deadline = Time.realtimeSinceStartup + (HEALTH_TIMEOUT_MS / 1000f);

            while (Time.realtimeSinceStartup < deadline && !ct.IsCancellationRequested)
            {
                using (UnityWebRequest req = UnityWebRequest.Head(BaseUrl))
                {
                    req.timeout = 1;
                    try { await req.SendWebRequest(); } catch { /* not up yet */ }

                    // Any HTTP response (even 404) proves the server is listening.
                    if (req.responseCode > 0) return true;
                }

                await UniTask.Delay(HEALTH_POLL_MS, DelayType.Realtime, cancellationToken: ct).SuppressCancellationThrow();
            }

            return false;
        }

        private static int FreeLoopbackPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static void TryKill(Process? proc)
        {
            if (proc == null) return;

            try
            {
                if (!proc.HasExited) proc.Kill();
                proc.Dispose();
            }
            catch (Exception)
            {
                // Already exited or inaccessible — nothing to clean up.
            }
        }
    }
}
