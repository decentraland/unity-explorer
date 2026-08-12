#nullable enable

using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utility;
using ECS.StreamableLoading.AssetBundles;
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

// ReSharper disable InconsistentNaming

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
        private const int PROGRESS_POLL_MS = 500;

        private static bool downloadStarted;

        private readonly string catalystContentUrl;
        private readonly string upstreamCdnUrl;
        private readonly string cacheRoot;
        private readonly bool jitContentDigest;

        private Process? process;
        private int restarts;
        private volatile bool disposed;

        public string BaseUrl { get; }

        private AbgenSidecar(int port, string catalystContentUrl, string upstreamCdnUrl, string cacheRoot, bool jitContentDigest)
        {
            BaseUrl = $"http://127.0.0.1:{port}";
            this.catalystContentUrl = catalystContentUrl;
            this.upstreamCdnUrl = upstreamCdnUrl;
            this.cacheRoot = cacheRoot;
            this.jitContentDigest = jitContentDigest;
        }

        public static string DownloadedExecutablePath =>
            Path.Combine(Application.persistentDataPath, "abgen", "bin", VERSION, $"abgen-v{VERSION}-{Platform()?.target}", IsWindows ? "abgen.exe" : "abgen");

        public static string StreamingAssetsExecutablePath =>
            Path.Combine(Application.streamingAssetsPath, IsWindows ? "abgen.exe" : "abgen");

        private static bool IsWindows => Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor;

        /// <summary>
        ///     Returns null when no binary is available yet (a background download is started — the sidecar
        ///     activates on the next launch) or the server never became healthy.
        ///     <para>
        ///     <paramref name="contentUrlOverride" /> points the server at a non-catalyst content source (the
        ///     local-scene-development preview server); its cache is kept apart from the catalyst one.
        ///     <paramref name="jitContentDigest" /> enables abgen's dev-mode freshness: every manifest request
        ///     re-downloads and re-hashes the entity's content, so edits reconvert even under LSD's
        ///     path-derived hashes, which never change.
        ///     </para>
        /// </summary>
        public static async UniTask<AbgenSidecar?> TryStartAsync(string environmentDomain, CancellationToken ct, string? cacheRoot = null, string? contentUrlOverride = null, bool jitContentDigest = false)
        {
            string exe = File.Exists(DownloadedExecutablePath) ? DownloadedExecutablePath
                : File.Exists(StreamingAssetsExecutablePath) ? StreamingAssetsExecutablePath : string.Empty;

            if (exe.Length == 0)
            {
                if (!downloadStarted && Platform() != null)
                {
                    downloadStarted = true;
                    AbgenConversionMetrics.INSTANCE.OnMilestone("abgen binary not installed — downloading in the background; local conversion activates next launch");
                    DownloadBinaryAsync().Forget();
                }

                return null;
            }

            var sidecar = new AbgenSidecar(FreeLoopbackPort(),
                contentUrlOverride ?? $"https://peer.decentraland.{environmentDomain}/content",
                $"https://ab-cdn.decentraland.{environmentDomain}",
                cacheRoot ?? Path.Combine(Application.persistentDataPath, contentUrlOverride == null ? "abgen" : "abgen-lsd"),
                jitContentDigest);

            if (sidecar.Launch(exe) && await sidecar.WaitHealthyAsync(ct))
                return sidecar;

            AbgenConversionMetrics.INSTANCE.OnMilestone("abgen sidecar failed to start — asset bundles come straight from the CDN");
            sidecar.Dispose();
            return null;
        }

        /// <summary>
        ///     Eager scene pre-conversion: resolves the realm's scene entity from its /about and requests that
        ///     entity's manifest, which makes the server JIT-convert every convertible file of the scene into
        ///     its corpus in one pass (observable at <c>/progress/{entity}</c>). Bundle requests that arrive
        ///     while the build runs coalesce with it; anything requested after is a disk hit. Failures are
        ///     logged and harmless — the lazy per-request lane still converts on demand.
        /// </summary>
        public async UniTaskVoid WarmUpLocalSceneAsync(CancellationToken ct)
        {
            string? convertingFile = null;

            try
            {
                string realmRoot = catalystContentUrl.EndsWith(RealmLaunchSettings.CONTENT_PATH, StringComparison.Ordinal)
                    ? catalystContentUrl[..^RealmLaunchSettings.CONTENT_PATH.Length]
                    : catalystContentUrl;

                using UnityWebRequest aboutRequest = UnityWebRequest.Get($"{realmRoot}/about");
                aboutRequest.timeout = 10;
                await aboutRequest.SendWebRequest().WithCancellation(ct);

                string? entityId = ParseFirstSceneEntityId(aboutRequest.downloadHandler.text)
                                   ?? await ResolveEntityIdFromParcelAsync(aboutRequest.downloadHandler.text, ct);

                if (entityId == null)
                {
                    AbgenConversionMetrics.INSTANCE.OnMilestone("warm-up skipped — could not resolve the scene entity (no scenesUrn or localSceneParcels in the realm's /about)");
                    ReportHub.LogWarning(ReportCategory.ASSET_BUNDLES, "abgen warm-up skipped: could not resolve the scene entity from the realm's /about");
                    return;
                }

                AbgenConversionMetrics.INSTANCE.OnWarmUpStarted(entityId);
                AbgenConversionMetrics.INSTANCE.OnMilestone($"warm-up started — converting scene {entityId} in the background");
                ReportHub.Log(ReportCategory.ASSET_BUNDLES, $"abgen warm-up: converting scene {entityId} — asset bundles are being built in the background");

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                using UnityWebRequest manifestRequest = UnityWebRequest.Get($"{BaseUrl}/manifest/{entityId}{PlatformUtils.GetCurrentPlatform()}.json");
                manifestRequest.timeout = 0; // a cold heavy scene converts for minutes; the server paces the build
                UnityWebRequestAsyncOperation manifestOperation = manifestRequest.SendWebRequest();

                // While the server holds the manifest request, mirror its per-file build progress into
                // the metrics the scene dev console's AB tab renders.
                var plannedTotal = -1;
                var sawBuildProgress = false;

                while (!manifestOperation.isDone)
                {
                    await UniTask.Delay(PROGRESS_POLL_MS, DelayType.Realtime, cancellationToken: ct);

                    BuildProgress? progress = await TryGetBuildProgressAsync(entityId, ct);
                    if (progress == null) continue;

                    sawBuildProgress = true;
                    AbgenConversionMetrics metrics = AbgenConversionMetrics.INSTANCE;

                    if (progress.total != plannedTotal)
                    {
                        plannedTotal = progress.total;
                        metrics.OnPlanned(plannedTotal);
                    }

                    if (progress.file != convertingFile)
                    {
                        if (convertingFile != null)
                            metrics.OnProcessed(convertingFile);

                        convertingFile = null;

                        if (!string.IsNullOrEmpty(progress.file))
                        {
                            metrics.OnStarted(progress.file);
                            convertingFile = progress.file;
                        }
                    }
                }

                if (convertingFile != null)
                {
                    AbgenConversionMetrics.INSTANCE.OnProcessed(convertingFile);
                    convertingFile = null;
                }

                if (manifestRequest.result != UnityWebRequest.Result.Success)
                    throw new IOException($"manifest request failed ({manifestRequest.responseCode}): {manifestRequest.error}");

                AbgenConversionMetrics.INSTANCE.OnWarmUpReady((float)stopwatch.Elapsed.TotalSeconds, alreadyWarm: !sawBuildProgress);

                AbgenConversionMetrics.INSTANCE.OnMilestone(sawBuildProgress
                    ? $"manifest retrieved — asset bundles ready in {stopwatch.Elapsed.TotalSeconds:F1}s"
                    : "asset bundles already converted — manifest served from warm cache");

                ReportHub.Log(ReportCategory.ASSET_BUNDLES, sawBuildProgress
                    ? $"abgen warm-up: manifest retrieved — asset bundles READY for scene {entityId} in {stopwatch.Elapsed.TotalSeconds:F1}s"
                    : $"abgen warm-up: asset bundles already converted (warm cache) — manifest for scene {entityId} served in {stopwatch.Elapsed.TotalSeconds:F1}s");

                int exitCode = JsonUtility.FromJson<CorpusManifest>(manifestRequest.downloadHandler.text).exitCode;

                if (exitCode != 0)
                {
                    AbgenConversionMetrics.INSTANCE.OnMilestone($"some files failed server-side conversion (manifest exitCode {exitCode})");
                    ReportHub.LogWarning(ReportCategory.ASSET_BUNDLES, $"abgen warm-up: manifest exitCode {exitCode} — some files failed server-side conversion; check the sidecar's cache logs");
                }
            }
            catch (OperationCanceledException)
            {
                if (convertingFile != null)
                    AbgenConversionMetrics.INSTANCE.OnCancelled(convertingFile);
            }
            catch (Exception e)
            {
                if (convertingFile != null)
                    AbgenConversionMetrics.INSTANCE.OnCancelled(convertingFile);

                AbgenConversionMetrics.INSTANCE.OnWarmUpFailed();
                AbgenConversionMetrics.INSTANCE.OnMilestone($"warm-up failed ({e.Message}) — bundles still convert lazily per request");
                ReportHub.LogWarning(ReportCategory.ASSET_BUNDLES, "abgen warm-up failed — bundles still convert lazily per request");
                ReportHub.LogException(e, ReportCategory.ASSET_BUNDLES);
            }
        }

        /// <summary>Null when no build for the entity is in flight (the route 404s before the build registers and after it finishes).</summary>
        private async UniTask<BuildProgress?> TryGetBuildProgressAsync(string entityId, CancellationToken ct)
        {
            using UnityWebRequest request = UnityWebRequest.Get($"{BaseUrl}/progress/{entityId}");
            request.timeout = 2;

            try { await request.SendWebRequest().WithCancellation(ct); }
            catch (OperationCanceledException) { throw; }
            catch { return null; }

            return JsonUtility.FromJson<BuildProgress>(request.downloadHandler.text);
        }

        /// <summary>
        ///     Resolves the scene entity by parcel pointer, for realms whose /about carries no scenesUrn —
        ///     the LSD preview server advertises <c>localSceneParcels</c> instead. Null when that field is
        ///     absent too or the content server returns no active entity for the parcel.
        /// </summary>
        private async UniTask<string?> ResolveEntityIdFromParcelAsync(string aboutJson, CancellationToken ct)
        {
            string? parcel = ParseJsonStringAfter(aboutJson, "\"localSceneParcels\":[\"");
            if (parcel == null) return null;

            using UnityWebRequest request = UnityWebRequest.Post($"{catalystContentUrl}/entities/active", $"{{\"pointers\":[\"{parcel}\"]}}", "application/json");
            request.timeout = 10;
            await request.SendWebRequest().WithCancellation(ct);

            return ParseJsonStringAfter(request.downloadHandler.text, "\"id\":\"");
        }

        private static string? ParseJsonStringAfter(string json, string marker)
        {
            int start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return null;
            start += marker.Length;

            int end = json.IndexOf('"', start);
            return end > start ? json[start..end] : null;
        }

        /// <summary>First <c>urn:decentraland:entity:{id}</c> in the /about JSON; the id runs until the urn's query string or the JSON string ends.</summary>
        private static string? ParseFirstSceneEntityId(string aboutJson)
        {
            const string URN_PREFIX = "urn:decentraland:entity:";

            int start = aboutJson.IndexOf(URN_PREFIX, StringComparison.Ordinal);
            if (start < 0) return null;
            start += URN_PREFIX.Length;

            int end = start;
            while (end < aboutJson.Length && aboutJson[end] != '?' && aboutJson[end] != '"' && aboutJson[end] != '\\') end++;

            return end > start ? aboutJson[start..end] : null;
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

            if (jitContentDigest)
                psi.EnvironmentVariables["ABGEN_JIT_CONTENT_DIGEST"] = "1";

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

        /// <summary>abgen <c>GET /progress/{entity}</c> response (crate/src/abcdn/handlers/status.rs).</summary>
        [Serializable]
        private class BuildProgress
        {
            public int total;
            public string file = null!;
        }

        /// <summary>The corpus manifest's failure indicator (crate/src/manifest.rs); the rest of the document is ignored here.</summary>
        [Serializable]
        private class CorpusManifest
        {
            public int exitCode;
        }
    }
}
