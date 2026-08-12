#nullable enable

using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using Decentraland.Abgen;
using DCL.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
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

                (byte[] model, List<string> bufferUris) = await InlineExternalImagesAsync(glb, glbPath, sceneContent, webRequestController, reportData, ct);

                // The model's own entry carries the scene hash: abgen names the assets inside the bundle after
                // it, and that must equal the hash the client extracts the prefab by (the b64- path-derived hash
                // in LSD). The model is never byte-resolved — OnlyGlb matches it by file name.
                AbgenRequest request = new AbgenRequest { Platform = PlatformUtils.GetCurrentPlatform().TrimStart('_'), Mode = AbgenMode.ConvertOnly, OnlyGlb = glbPath, EntityHash = glbHash }
                   .AddFile(glbPath, model)
                   .AddContentEntry(glbPath, glbHash);

                foreach (string uri in bufferUris)
                {
                    string path = ResolveContentPath(glbPath, uri);

                    // An unresolvable buffer is abgen's call, not ours: it fails that one model with a
                    // per-file error instead of the whole request.
                    if (!sceneContent.TryGetHash(path, out string _)) continue;

                    byte[] dependency = await FetchAsync(path, sceneContent, webRequestController, reportData, ct);

                    // Buffer entries MUST carry the sha256 of the uploaded bytes: it is abgen's key from a
                    // resolved external URI into the uploaded files, so any other value (the scene hash)
                    // leaves the buffer unresolvable and hard-fails the model. Buffers never become
                    // metadata.json dependencies, so the sha256 cannot leak into bundle references.
                    request.AddFile(path, dependency)
                           .AddContentEntry(path, Sha256Hex(dependency));
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
                    string error = DescribeFailure(result);
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

        /// <summary>
        ///     Per-model failures are "file-error" JSON events in <see cref="AbgenResult.Events" />, not entries in
        ///     <see cref="AbgenResult.Errors" /> (that list only carries fatal run-level errors), so a failure
        ///     description has to pull from both.
        /// </summary>
        private static string DescribeFailure(AbgenResult result)
        {
            var sb = new StringBuilder();
            sb.Append("status: ").Append(result.Status).Append(", artifacts: ").Append(result.Artifacts.Count);

            foreach (string error in result.Errors)
                sb.Append(" | ").Append(error);

            foreach (string ev in result.Events)
                if (ev.Contains("\"file-error\""))
                    sb.Append(" | ").Append(ev);

            return sb.ToString();
        }

        private static string Sha256Hex(byte[] data)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(data);

            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static async UniTask<byte[]> FetchAsync(string contentPath, ISceneContent sceneContent, IWebRequestController webRequestController, ReportData reportData, CancellationToken ct)
        {
            if (!sceneContent.TryGetContentUrl(contentPath, out URLAddress url))
                throw new ArgumentException($"'{contentPath}' is not in the scene content");

            return await webRequestController.GetAsync(new CommonArguments(url), ct, reportData).GetDataCopyAsync();
        }

        /// <summary>
        ///     Rewrites every resolvable external image reference into a data: URI (fetching the bytes from the
        ///     scene content) and collects the external buffer URIs that must be uploaded alongside the model.
        ///     Inlining keeps the produced bundle self-contained: abgen turns a resolvable external image URI
        ///     into a metadata.json dependency on a separate texture bundle — which only the CDN pipeline
        ///     builds — while a data: URI is compressed into the model bundle itself.
        /// </summary>
        private static async UniTask<(byte[] model, List<string> bufferUris)> InlineExternalImagesAsync(byte[] model, string glbPath, ISceneContent sceneContent,
            IWebRequestController webRequestController, ReportData reportData, CancellationToken ct)
        {
            var bufferUris = new List<string>();

            string? json = ExtractGltfJson(model);
            if (json == null) return (model, bufferUris);

            var root = JObject.Parse(json);
            var inlined = new Dictionary<string, string>();

            if (root["images"] is JArray images)
                foreach (JToken image in images)
                {
                    string? uri = image["uri"]?.Value<string>();

                    // Scheme'd URIs and images backed by a bufferView are abgen's own soft-skip cases.
                    if (string.IsNullOrEmpty(uri) || uri.Contains(':') || image["bufferView"] != null) continue;

                    if (!inlined.TryGetValue(uri, out string dataUri))
                    {
                        string path = ResolveContentPath(glbPath, uri);

                        // A missing image is soft for abgen: the texture slot stays empty.
                        if (!sceneContent.TryGetHash(path, out string _)) continue;

                        byte[] bytes = await FetchAsync(path, sceneContent, webRequestController, reportData, ct);
                        dataUri = "data:application/octet-stream;base64," + Convert.ToBase64String(bytes);
                        inlined[uri] = dataUri;
                    }

                    image["uri"] = dataUri;
                }

            if (root["buffers"] is JArray buffers)
                foreach (JToken buffer in buffers)
                {
                    string? uri = buffer["uri"]?.Value<string>();

                    if (!string.IsNullOrEmpty(uri) && !uri.Contains(':') && !bufferUris.Contains(uri))
                        bufferUris.Add(uri);
                }

            if (inlined.Count == 0) return (model, bufferUris);

            byte[] jsonBytes = Encoding.UTF8.GetBytes(root.ToString(Formatting.None));
            return (IsGlb(model) ? RebuildGlb(model, jsonBytes) : jsonBytes, bufferUris);
        }

        private static string? ExtractGltfJson(byte[] model)
        {
            if (IsGlb(model))
                return BitConverter.ToUInt32(model, 16) == JSON_CHUNK
                    ? Encoding.UTF8.GetString(model, 20, (int)BitConverter.ToUInt32(model, 12))
                    : null;

            // A .gltf file is the JSON itself.
            return model.Length > 0 && model[0] == (byte)'{' ? Encoding.UTF8.GetString(model) : null;
        }

        private static bool IsGlb(byte[] model) =>
            model.Length >= 20 && BitConverter.ToUInt32(model, 0) == GLB_MAGIC;

        /// <summary>
        ///     Replaces the JSON chunk (padded to 4 bytes with spaces, per the glTF spec) and fixes up the
        ///     container length; every chunk after the JSON one is copied verbatim.
        /// </summary>
        private static byte[] RebuildGlb(byte[] glb, byte[] json)
        {
            int paddedLength = (json.Length + 3) & ~3;
            int oldJsonLength = (int)BitConverter.ToUInt32(glb, 12);
            int restStart = 20 + oldJsonLength;
            int restLength = glb.Length - restStart;

            var result = new byte[20 + paddedLength + restLength];
            Buffer.BlockCopy(glb, 0, result, 0, 12);
            BitConverter.GetBytes((uint)result.Length).CopyTo(result, 8);
            BitConverter.GetBytes((uint)paddedLength).CopyTo(result, 12);
            BitConverter.GetBytes(JSON_CHUNK).CopyTo(result, 16);
            Buffer.BlockCopy(json, 0, result, 20, json.Length);

            for (int i = 20 + json.Length; i < 20 + paddedLength; i++)
                result[i] = 0x20;

            Buffer.BlockCopy(glb, restStart, result, 20 + paddedLength, restLength);
            return result;
        }

        /// <summary>Resolves a GLB-relative URI to a scene content path: percent-decoded, joined to the GLB's directory, with "./" and "../" collapsed.</summary>
        private static string ResolveContentPath(string glbPath, string uri) =>
            Uri.UnescapeDataString(new Uri(new Uri("file:///" + glbPath), uri).AbsolutePath).TrimStart('/');
    }
}
