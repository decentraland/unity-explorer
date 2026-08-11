#nullable enable

using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using Decentraland.Abgen;
using DCL.Utility;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     In-process asset-bundle build for local scene development (<c>--local-ab</c>): fetch the source GLB and
    ///     the external textures/buffers it references from the scene content and convert them to a bundle with the
    ///     embedded abgen library (no Editor, no sidecar, no HTTP server). Results are cached on disk
    ///     (<see cref="AbgenBundleDiskCache" />) so a reload or next-day restart reconverts only what changed.
    /// </summary>
    internal static class AbgenAssetBundleFallback
    {
        private const uint GLB_MAGIC = 0x46546c67;
        private const uint JSON_CHUNK = 0x4e4f534a;

        private static bool threadsCapped;

        public static async UniTask<AssetBundle?> TryBuildAsync(string? glbPath, ISceneContent? sceneContent, IWebRequestController webRequestController, ReportData reportData, CancellationToken ct)
        {
            if (glbPath == null || sceneContent == null) return null;
            if (!glbPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) && !glbPath.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)) return null;

            if (!sceneContent.TryGetHash(glbPath, out string glbHash))
            {
                Debug.LogWarning($"[Juani][abgen] '{glbPath}' is not in the scene content mapping; skipping in-process conversion");
                return null;
            }

            if (!AbgenConverter.IsAbiCompatible())
            {
                Debug.LogWarning("[Juani][abgen] native library missing or ABI-incompatible (expected the abgen plugin under Packages/org.decentraland.abgen/Runtime/Plugins); skipping in-process conversion");
                return null;
            }

            // Read persistentDataPath here, on the main thread, before any await hops to the thread pool.
            string cacheRoot = AbgenBundleDiskCache.RootDirectory();

            AbgenConversionMetrics.INSTANCE.OnStarted(glbPath);

            try
            {
                byte[] glb = await FetchAsync(glbPath, sceneContent, webRequestController, reportData, ct);

                AbgenRequest request = new AbgenRequest { Platform = PlatformUtils.GetCurrentPlatform().TrimStart('_'), Mode = AbgenMode.ConvertOnly, OnlyGlb = glbPath, EntityHash = glbHash }
                   .AddFile(glbPath, glb)
                   .AddContentEntry(glbPath, glbHash);

                foreach (string uri in ExternalUris(glb))
                {
                    string path = ResolveContentPath(glbPath, uri);

                    // Unresolvable references are abgen's call, not ours: it emits the bundle with the
                    // texture slot empty (missing images are soft; missing buffers fail that GLB itself).
                    if (!sceneContent.TryGetHash(path, out string hash)) continue;

                    request.AddFile(path, await FetchAsync(path, sceneContent, webRequestController, reportData, ct))
                           .AddContentEntry(path, hash);
                }

                byte[] requestBlob = request.ToBytes();
                string cacheKey = AbgenBundleDiskCache.ComputeKey(requestBlob);

                if (AbgenBundleDiskCache.TryGetPath(cacheRoot, cacheKey, out string cachedPath))
                {
                    await UniTask.SwitchToMainThread();
                    AssetBundle? cached = AssetBundle.LoadFromFile(cachedPath);

                    if (cached != null)
                    {
                        long bytes = new System.IO.FileInfo(cachedPath).Length;
                        Debug.Log($"[Juani][abgen] disk-cache hit for '{glbPath}' ({bytes} B)");
                        AbgenConversionMetrics.INSTANCE.OnSucceeded(glbPath, "(disk) " + cacheKey.Substring(0, 8), (int)bytes, 0);
                        return cached;
                    }

                    // Corrupt or partially-written entry: drop it and reconvert.
                    Debug.LogWarning($"[Juani][abgen] disk-cache entry for '{glbPath}' failed to load; reconverting");
                    AbgenBundleDiskCache.Delete(cacheRoot, cacheKey);
                }

                if (!threadsCapped)
                {
                    // Process-wide and effective once: keep the native pool from competing with the frame budget.
                    AbgenConverter.SetMaxThreads((uint)Mathf.Clamp(SystemInfo.processorCount / 4, 2, 4));
                    threadsCapped = true;
                }

                (AbgenResult result, long elapsedMs) = await UniTask.RunOnThreadPool(() =>
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    AbgenResult r = AbgenConverter.Convert(request);
                    stopwatch.Stop();

                    // Persist off the main thread, before we hop back to load the freshly-written file.
                    if (r.Succeeded && r.Artifacts.Count > 0)
                        AbgenBundleDiskCache.Write(cacheRoot, cacheKey, r.Artifacts[0].Data);

                    return (r, stopwatch.ElapsedMilliseconds);
                }, cancellationToken: ct);

                if (!result.Succeeded || result.Artifacts.Count == 0)
                {
                    string error = $"status: {result.Status}, artifacts: {result.Artifacts.Count}, errors: {string.Join(" | ", result.Errors)}";
                    Debug.LogWarning($"[Juani][abgen] conversion of '{glbPath}' produced no bundle ({error})");
                    AbgenConversionMetrics.INSTANCE.OnFailed(glbPath, error);
                    return null;
                }

                await UniTask.SwitchToMainThread();
                Debug.Log($"[Juani][abgen] converted '{glbPath}' -> {result.Artifacts[0].Name} ({result.Artifacts[0].Data.Length} B) in {elapsedMs} ms");
                AbgenConversionMetrics.INSTANCE.OnSucceeded(glbPath, result.Artifacts[0].Name, result.Artifacts[0].Data.Length, elapsedMs);

                // Load the on-disk copy (memory-mapped) rather than the managed byte[] we just wrote.
                return AbgenBundleDiskCache.TryGetPath(cacheRoot, cacheKey, out string writtenPath)
                    ? AssetBundle.LoadFromFile(writtenPath)
                    : AssetBundle.LoadFromMemory(result.Artifacts[0].Data);
            }
            catch (OperationCanceledException)
            {
                AbgenConversionMetrics.INSTANCE.OnCancelled(glbPath);
                throw;
            }
            catch (Exception e)
            {
                // The caller's own "bundle is null" error handling stays authoritative.
                AbgenConversionMetrics.INSTANCE.OnFailed(glbPath, e.Message);
                ReportHub.LogException(e, reportData);
                return null;
            }
        }

        private static async UniTask<byte[]> FetchAsync(string contentPath, ISceneContent sceneContent, IWebRequestController webRequestController, ReportData reportData, CancellationToken ct)
        {
            if (!sceneContent.TryGetContentUrl(contentPath, out URLAddress url))
                throw new ArgumentException($"'{contentPath}' is not in the scene content");

            return await webRequestController.GetAsync(new CommonArguments(url), ct, reportData).GetDataCopyAsync();
        }

        /// <summary>External (non-embedded, non-remote) image and buffer URIs referenced by the model's glTF JSON, deduplicated.</summary>
        private static List<string> ExternalUris(byte[] model)
        {
            var uris = new List<string>();
            string? json = null;

            if (model.Length >= 20 && BitConverter.ToUInt32(model, 0) == GLB_MAGIC)
            {
                if (BitConverter.ToUInt32(model, 16) == JSON_CHUNK)
                    json = Encoding.UTF8.GetString(model, 20, (int)BitConverter.ToUInt32(model, 12));
            }
            else if (model.Length > 0 && model[0] == (byte)'{')
                json = Encoding.UTF8.GetString(model); // A .gltf file is the JSON itself.

            if (json == null) return uris;

            var root = JsonUtility.FromJson<GLTFast.Schema.Root>(json);
            var seen = new HashSet<string>();

            if (root.images != null)
                foreach (GLTFast.Schema.Image image in root.images)
                    if (!string.IsNullOrEmpty(image.uri) && !image.uri.Contains(':') && seen.Add(image.uri))
                        uris.Add(image.uri);

            if (root.buffers != null)
                foreach (GLTFast.Schema.Buffer buffer in root.buffers)
                    if (!string.IsNullOrEmpty(buffer.uri) && !buffer.uri.Contains(':') && seen.Add(buffer.uri))
                        uris.Add(buffer.uri);

            return uris;
        }

        /// <summary>Resolves a GLB-relative URI to a scene content path: percent-decoded, joined to the GLB's directory, with "./" and "../" collapsed.</summary>
        private static string ResolveContentPath(string glbPath, string uri) =>
            Uri.UnescapeDataString(new Uri(new Uri("file:///" + glbPath), uri).AbsolutePath).TrimStart('/');
    }
}
