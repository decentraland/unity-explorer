#nullable enable

using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utility;
using ECS.StreamableLoading.AssetBundles;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;
#if UNITY_EDITOR
using System.Diagnostics;
#elif !UNITY_STANDALONE_WIN
using Plugins.DclNativeProcesses;
using RichTypes;
#endif

// ReSharper disable InconsistentNaming

namespace Global.Dynamic
{
    /// <summary>
    ///     Runs the abgen JIT asset-bundle server as a supervised localhost sidecar. The client's unchanged
    ///     loading path consumes its base URL as the optimized-assets source; the server JIT-converts the
    ///     local scene and answers everything else from the production upstream (ab-cdn read-through and
    ///     registry pass-through), caching converted bundles on disk.
    ///     Two-step lifecycle: <see cref="ReserveBaseUrl" /> (synchronous — a loopback port only, so the
    ///     URL can seed the URL sources built early in startup), then AbgenSidecarPlugin creates the
    ///     instance on that URL (<see cref="TryCreate" />) and launches it (<see cref="StartAsync" />),
    ///     owning it from there on.
    ///     The binary is never embedded in the build: on first run the pinned release is downloaded
    ///     (<see cref="EnsurePinnedBinaryAsync" />) and verified against its compile-time sha256. Only the
    ///     pinned version is ever executed — a compromised GitHub release cannot propagate here without a
    ///     deliberate pin+checksum bump in this file. StreamingAssets acts as an explicit developer override.
    ///     An explicit --optimized-assets-url always takes precedence.
    /// </summary>
    public sealed class AbgenSidecar : IDisposable
    {
        private const string PINNED_VERSION = "0.16.0";
        private const int MAX_RESTARTS = 3;
        private const int HEALTH_TIMEOUT_MS = 15000;
        private const int HEALTH_POLL_MS = 250;
        private const int PROGRESS_POLL_MS = 500;
        private const int SUPERVISION_POLL_MS = 2000;

        /// <summary>Path under the realm root where a content server exposes its entities and files.</summary>
        private const string CONTENT_PATH = "/content";

        private readonly string executablePath;
        private readonly string realmRoot;
        private readonly string catalystContentUrl;
        private readonly string upstreamCdnUrl;
        private readonly string cacheRoot;
        private readonly bool jitContentDigest;

        // System.Diagnostics.Process cannot spawn under IL2CPP (Win32Exception "Native error= Success"),
        // so player builds hold the child as a raw OS handle/pid; only the editor's Mono runtime keeps
        // the managed Process object (and its drained stdout/stderr pipes).
#if UNITY_EDITOR
        private Process? process;
#elif UNITY_STANDALONE_WIN
        private IntPtr processHandle;
#else
        private int processId;
#endif
        private int restarts;
        private volatile bool disposed;

        public string BaseUrl { get; }

        private AbgenSidecar(string baseUrl, string executablePath, string realmRoot, string upstreamCdnUrl, string cacheRoot, bool jitContentDigest)
        {
            BaseUrl = baseUrl;
            this.executablePath = executablePath;
            this.realmRoot = realmRoot;
            catalystContentUrl = realmRoot + CONTENT_PATH;
            this.upstreamCdnUrl = upstreamCdnUrl;
            this.cacheRoot = cacheRoot;
            this.jitContentDigest = jitContentDigest;
        }

        public static string StreamingAssetsExecutablePath =>
            Path.Combine(Application.streamingAssetsPath, IsWindows ? "abgen.exe" : "abgen");

        private static bool IsWindows => Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor;

        /// <summary>
        ///     Reserves a free loopback endpoint for the server WITHOUT creating or starting anything —
        ///     synchronous, so the URL can seed the URL sources built early in startup. The server is
        ///     created on this URL later via <see cref="TryCreate" />.
        /// </summary>
        public static string ReserveBaseUrl() =>
            $"http://127.0.0.1:{FreeLoopbackPort()}";

        /// <summary>
        ///     Resolves the server binary and creates the (not yet started) sidecar on
        ///     <paramref name="baseUrl" />; <see cref="StartAsync" /> launches it. Returns null when no
        ///     binary is installed — <see cref="EnsurePinnedBinaryAsync" /> downloads it.
        ///     <para>
        ///     <paramref name="realmRootOverride" /> points the server at a non-catalyst realm (the
        ///     local-scene-development preview server), whose /content endpoints the scene is read through;
        ///     its cache is kept apart from the catalyst one.
        ///     <paramref name="jitContentDigest" /> enables abgen's dev-mode freshness: every manifest request
        ///     re-downloads and re-hashes the entity's content, so edits reconvert even under LSD's
        ///     path-derived hashes, which never change.
        ///     </para>
        /// </summary>
        public static AbgenSidecar? TryCreate(string baseUrl, string environmentDomain, string? cacheRoot = null, string? realmRootOverride = null, bool jitContentDigest = false)
        {
            string? exe = TryFindPinnedExecutable() ?? (File.Exists(StreamingAssetsExecutablePath) ? StreamingAssetsExecutablePath : null);

            if (exe == null)
                return null;

            return new AbgenSidecar(baseUrl,
                exe,
                realmRootOverride?.TrimEnd('/') ?? $"https://peer.decentraland.{environmentDomain}",
                $"https://ab-cdn.decentraland.{environmentDomain}",
                cacheRoot ?? Path.Combine(Application.persistentDataPath, realmRootOverride == null ? AbgenBundleDiskCache.SIDECAR_DIR : AbgenBundleDiskCache.SIDECAR_LSD_DIR),
                jitContentDigest);
        }

        /// <summary>
        ///     Launches the reserved server process and waits until it answers on <see cref="BaseUrl" />.
        ///     False when it could not start or never became healthy — the process is disposed and a
        ///     milestone row reports it; requests to <see cref="BaseUrl" /> then fail fast on the dead
        ///     loopback port.
        /// </summary>
        public async UniTask<bool> StartAsync(CancellationToken ct)
        {
            if (Launch(executablePath) && await WaitHealthyAsync(ct))
            {
                SuperviseAsync(ct).Forget();
                return true;
            }

            AbgenConversionMetrics.INSTANCE.OnMilestone("abgen sidecar failed to start — the scene loads as raw GLTFs");
            Dispose();
            return false;
        }

        /// <summary>
        ///     Eager scene pre-conversion: resolves the realm's scene entity from its /about and requests that
        ///     entity's manifest, which makes the server JIT-convert every convertible file of the scene into
        ///     its corpus in one pass (observable at <c>/progress/{entity}</c>). Bundle requests that arrive
        ///     while the build runs coalesce with it; anything requested after is a disk hit. Failures are
        ///     logged and harmless — the lazy per-request lane still converts on demand.
        /// </summary>
        public async UniTask WarmUpLocalSceneAsync(CancellationToken ct)
        {
            string? convertingFile = null;

            try
            {
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
                // the metrics the scene dev console's AB tab renders. done/total are the server's own
                // authoritative counters — the sampled per-file rows are color, not the count.
                var lastDone = -1;
                var lastTotal = -1;
                var sawBuildProgress = false;

                while (!manifestOperation.isDone)
                {
                    await UniTask.Delay(PROGRESS_POLL_MS, DelayType.Realtime, cancellationToken: ct);

                    BuildProgress? progress = await TryGetBuildProgressAsync(entityId, ct);
                    if (progress == null) continue;

                    sawBuildProgress = true;
                    AbgenConversionMetrics metrics = AbgenConversionMetrics.INSTANCE;

                    if (progress.done != lastDone || progress.total != lastTotal)
                    {
                        lastDone = progress.done;
                        lastTotal = progress.total;
                        metrics.OnWarmUpProgress(progress.done, progress.total);
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

                // The progress poll only samples whichever file is converting at each tick, so fast files
                // leave no row; backfill the panel with the scene's full convertible file list.
                await ReconcileCensusAsync(entityId, ct);

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

        /// <summary>
        ///     Backfills the AB panel with every convertible file of the scene entity, sourced from the
        ///     entity definition's content list (readable paths — the manifest only carries hashed artifact
        ///     names). Best effort: a failure leaves the sampled rows as they are.
        /// </summary>
        private async UniTask ReconcileCensusAsync(string entityId, CancellationToken ct)
        {
            try
            {
                using UnityWebRequest request = UnityWebRequest.Get($"{catalystContentUrl}/contents/{entityId}");
                request.timeout = 10;
                await request.SendWebRequest().WithCancellation(ct);

                EntityContent? entity = JsonUtility.FromJson<EntityContent>(request.downloadHandler.text);
                if (entity?.content == null) return;

                var files = new List<string>(entity.content.Length);

                foreach (EntityContent.FileEntry entry in entity.content)
                    if (IsConvertible(entry.file))
                        files.Add(entry.file);

                AbgenConversionMetrics.INSTANCE.ReconcileWarmUpCensus(files);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogException(e, ReportCategory.ASSET_BUNDLES);
            }
        }

        /// <summary>The extensions abgen's corpus build converts: models and the standalone images they reference.</summary>
        private static bool IsConvertible(string file) =>
            file.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);

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
            KillChild();
        }

        /// <summary>Release target triple and the pinned release's archive sha256 for the current platform; null when unsupported.</summary>
        private static (string target, string sha256)? Platform() =>
            Application.platform switch
            {
                RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => ("x86_64-pc-windows-gnu", "026c425d2d203c173876d7a33af66a292cd54e3db3d99677b05503f1a3826d1a"),
                RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? ("aarch64-apple-darwin", "ccff87a8192d5f329f0427e68fa850a490d4cefa53839f11062f91afe8420278")
                    : ("x86_64-apple-darwin", "8e3cb8957a8f1916b820e008d369be4cff0f1b024ff6764998f5c2bc5d151950"),
                RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor => ("x86_64-unknown-linux-gnu", "c155d8f27653bd357f42ca3cf7b848809d0c07804445f79011bce79c6a8594d3"),
                _ => null,
            };

        /// <summary>The pinned version installed under <c>bin/{version}/abgen-v{version}-{target}/</c>, or null. Other installed versions are never executed.</summary>
        private static string? TryFindPinnedExecutable()
        {
            string? target = Platform()?.target;
            if (target == null) return null;

            string exe = Path.Combine(Application.persistentDataPath, AbgenBundleDiskCache.SIDECAR_DIR, "bin",
                PINNED_VERSION, $"abgen-v{PINNED_VERSION}-{target}", IsWindows ? "abgen.exe" : "abgen");

            return File.Exists(exe) ? exe : null;
        }

        /// <summary>
        ///     Downloads and installs the pinned release, verified against its compile-time sha256.
        ///     Progress is reported to the AB panel as milestone rows. True when the binary is installed
        ///     and <see cref="TryCreate" /> will resolve it; false on an unsupported platform,
        ///     cancellation or a failed download.
        /// </summary>
        public static async UniTask<bool> EnsurePinnedBinaryAsync(CancellationToken ct)
        {
            if (Platform() == null)
                return false;

            try
            {
                (string target, string sha256) = Platform()!.Value;
                await DownloadAndInstallAsync(PINNED_VERSION, target, sha256, ct);
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception e)
            {
                AbgenConversionMetrics.INSTANCE.OnMilestone($"abgen download failed ({e.Message}) — retried on the next launch");
                ReportHub.LogException(e, ReportCategory.ASSET_BUNDLES);
                return false;
            }
        }

        private static async UniTask DownloadAndInstallAsync(string version, string target, string sha256, CancellationToken ct)
        {
            string url = $"https://github.com/decentraland/abgen/releases/download/v{version}/abgen-v{version}-{target}.tar.gz";

            AbgenConversionMetrics.INSTANCE.OnMilestone($"abgen binary not installed — downloading the pinned release v{version}");

            using UnityWebRequest req = UnityWebRequest.Get(url);
            req.timeout = 600;
            UnityWebRequestAsyncOperation downloadOperation = req.SendWebRequest();
            var lastReportedQuarter = 0;

            // Disposing the request (the using above) aborts the transfer when ct fires mid-download.
            while (!downloadOperation.isDone)
            {
                await UniTask.Delay(500, DelayType.Realtime, cancellationToken: ct);

                var quarter = (int)(req.downloadProgress * 4f);

                if (quarter > lastReportedQuarter && quarter < 4)
                {
                    lastReportedQuarter = quarter;
                    AbgenConversionMetrics.INSTANCE.OnMilestone($"downloading abgen v{version} — {quarter * 25}% ({req.downloadedBytes / (1024 * 1024)} MB)");
                }
            }

            if (req.result != UnityWebRequest.Result.Success)
                throw new IOException($"abgen archive download failed: {req.error}");

            byte[] archive = req.downloadHandler.data;

            using (var sha = SHA256.Create())
            {
                string actual = BitConverter.ToString(sha.ComputeHash(archive)).Replace("-", "").ToLowerInvariant();

                if (actual != sha256)
                    throw new IOException($"abgen archive checksum mismatch: {actual}");
            }

            await DCLTask.RunOnThreadPool(() =>
            {
                string finalDir = Path.Combine(Application.persistentDataPath, AbgenBundleDiskCache.SIDECAR_DIR, "bin", version);
                string tmpDir = finalDir + ".tmp";
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
                ExtractTarGz(archive, tmpDir);

                if (!IsWindows)
                {
                    // Straight through libc — a chmod subprocess needs System.Diagnostics.Process,
                    // which cannot spawn under IL2CPP.
                    PosixChmod(tmpDir, UNIX_MODE_755);

                    foreach (string entry in Directory.GetFileSystemEntries(tmpDir, "*", SearchOption.AllDirectories))
                        PosixChmod(entry, UNIX_MODE_755);
                }

                if (Directory.Exists(finalDir)) Directory.Delete(finalDir, true);
                Directory.Move(tmpDir, finalDir);
            });

            AbgenConversionMetrics.INSTANCE.OnMilestone($"abgen v{version} installed");
            ReportHub.Log(ReportCategory.ASSET_BUNDLES, $"abgen sidecar binary v{version} downloaded and installed");
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

                // Defense in depth: a ".." component would escape the destination. The archive is
                // sha256-pinned, so this only fires on a hostile or corrupt file — skip the entry.
                if (name.Contains(".."))
                    SkipBytes(gz, size);
                else if (type == (byte)'5')
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
            try
            {
                // abgen is configured entirely through environment variables, and every spawn path
                // below launches the child with this process's environment.
                Environment.SetEnvironmentVariable("HTTP_SERVER_HOST", "127.0.0.1");
                Environment.SetEnvironmentVariable("HTTP_SERVER_PORT", new Uri(BaseUrl).Port.ToString());
                Environment.SetEnvironmentVariable("ABGEN_CACHE_DIR", Path.Combine(cacheRoot, "cache"));
                Environment.SetEnvironmentVariable("ABGEN_OUT_ROOT", Path.Combine(cacheRoot, "out"));
                Environment.SetEnvironmentVariable("ABGEN_CATALYST_URL", catalystContentUrl);
                Environment.SetEnvironmentVariable("ABGEN_UPSTREAM_AB_CDN", upstreamCdnUrl);
                Environment.SetEnvironmentVariable("ABGEN_JIT_CONTENT_DIGEST", jitContentDigest ? "1" : null);

                // With no backend pinned abgen auto-tries its GPU BC7/BC5 encoder, and arming CUDA
                // costs ~60s before the HTTP listener binds — far past HEALTH_TIMEOUT_MS, so the
                // sidecar is killed before it ever answers. The CPU encoder produces byte-identical
                // bundles, so pinning it off only trades encode throughput for a ~1s startup.
                Environment.SetEnvironmentVariable("ABGEN_GPU_BACKEND", "off");

                return LaunchChild(executablePath);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.ASSET_BUNDLES);
                return false;
            }
        }

        private bool LaunchChild(string executablePath)
        {
#if UNITY_EDITOR
            var psi = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            process = Process.Start(psi);
            if (process == null) return false;

            // Drain pipes so the child never blocks on a full stdout/stderr buffer.
            process.OutputDataReceived += static (_, _) => { };
            process.ErrorDataReceived += static (_, _) => { };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return true;
#elif UNITY_STANDALONE_WIN
            // CREATE_NO_WINDOW keeps the console-subsystem server from opening a console window;
            // the child's null std handles are swallowed by its runtime.
            var startupInfo = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };

            if (!CreateProcessW(null, new System.Text.StringBuilder($"\"{executablePath}\""), IntPtr.Zero, IntPtr.Zero, false,
                    CREATE_NO_WINDOW, IntPtr.Zero, null, ref startupInfo, out PROCESS_INFORMATION processInformation))
            {
                ReportHub.LogWarning(ReportCategory.ASSET_BUNDLES, $"abgen CreateProcess failed (err {Marshal.GetLastWin32Error()})");
                return false;
            }

            CloseHandle(processInformation.hThread);

            if (processHandle != IntPtr.Zero) CloseHandle(processHandle);
            processHandle = processInformation.hProcess;
            return true;
#else
            Result<int> result = DclProcesses.Start(executablePath, Array.Empty<string>());

            if (!result.Success)
            {
                ReportHub.LogWarning(ReportCategory.ASSET_BUNDLES, $"abgen spawn failed: {result.ErrorMessage}");
                return false;
            }

            processId = result.Value;
            return true;
#endif
        }

        private bool ChildAlive()
        {
#if UNITY_EDITOR
            try { return process is { HasExited: false }; }
            catch (Exception) { return false; }
#elif UNITY_STANDALONE_WIN
            return processHandle != IntPtr.Zero && WaitForSingleObject(processHandle, 0) == WAIT_TIMEOUT;
#else
            return processId > 0 && kill(processId, 0) == 0;
#endif
        }

        private void KillChild()
        {
#if UNITY_EDITOR
            try
            {
                if (process is { HasExited: false }) process.Kill();
                process?.Dispose();
            }
            catch (Exception)
            {
                // Already exited or inaccessible — nothing to clean up.
            }

            process = null;
#elif UNITY_STANDALONE_WIN
            if (processHandle == IntPtr.Zero) return;
            TerminateProcess(processHandle, 0);
            CloseHandle(processHandle);
            processHandle = IntPtr.Zero;
#else
            if (processId > 0) kill(processId, SIGKILL);
            processId = 0;
#endif
        }

        /// <summary>
        ///     Liveness is polled rather than event-driven — an Exited event needs the managed Process
        ///     object, which player builds don't have. A dead child is relaunched on the same port
        ///     (consumers already hold <see cref="BaseUrl" />) up to <see cref="MAX_RESTARTS" /> times.
        /// </summary>
        private async UniTaskVoid SuperviseAsync(CancellationToken ct)
        {
            while (!disposed && !ct.IsCancellationRequested)
            {
                await UniTask.Delay(SUPERVISION_POLL_MS, DelayType.Realtime, cancellationToken: ct).SuppressCancellationThrow();

                if (disposed || ct.IsCancellationRequested) return;
                if (ChildAlive()) continue;

                // Main-thread only: UniTask.Delay resumes this loop on the player loop, so no atomicity is needed.
                if (++restarts > MAX_RESTARTS)
                {
                    ReportHub.LogWarning(ReportCategory.ASSET_BUNDLES, "abgen sidecar keeps exiting; asset bundles fall back to direct CDN errors");
                    return;
                }

                ReportHub.LogWarning(ReportCategory.ASSET_BUNDLES, $"abgen sidecar exited; restart {restarts}/{MAX_RESTARTS}");

                if (!Launch(executablePath))
                {
                    ReportHub.LogWarning(ReportCategory.ASSET_BUNDLES, "abgen sidecar restart failed; asset bundles fall back to direct CDN errors");
                    return;
                }
            }
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

        private const uint UNIX_MODE_755 = 0x1ED; // rwxr-xr-x

        // Never called on Windows (IsWindows-guarded call sites); the import only binds on first call.
        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int PosixChmod(string path, uint mode);

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        private const uint CREATE_NO_WINDOW = 0x08000000;
        private const uint WAIT_TIMEOUT = 0x102;

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessW(string? lpApplicationName, System.Text.StringBuilder lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
            bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string? lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
#elif !UNITY_EDITOR
        private const int SIGKILL = 9;

        [DllImport("libc", SetLastError = true)]
        private static extern int kill(int pid, int sig);
#endif

        /// <summary>abgen <c>GET /progress/{entity}</c> response (crate/src/abcdn/handlers/status.rs).</summary>
        [Serializable]
        private class BuildProgress
        {
            public int done;
            public int total;
            public string file = null!;
        }

        /// <summary>The corpus manifest's failure indicator (crate/src/manifest.rs); the rest of the document is ignored here.</summary>
        [Serializable]
        private class CorpusManifest
        {
            public int exitCode;
        }

        /// <summary>The content mapping of a deployed entity (ADR-80 entity schema); the rest of the document is ignored here.</summary>
        [Serializable]
        private class EntityContent
        {
            public FileEntry[] content = null!;

            [Serializable]
            public class FileEntry
            {
                public string file = null!;
            }
        }
    }
}
