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
    ///     Last resort when the prebuilt bundle is absent from the CDN: fetch the source GLB and the external
    ///     textures/buffers it references from the scene content and convert them to a bundle in-process with
    ///     abgen (no Editor, no sidecar, no HTTP server).
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
            if (!sceneContent.TryGetHash(glbPath, out string glbHash) || !AbgenConverter.IsAbiCompatible()) return null;

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

                if (!threadsCapped)
                {
                    // Process-wide and effective once: keep the native pool from competing with the frame budget.
                    AbgenConverter.SetMaxThreads((uint)Mathf.Clamp(SystemInfo.processorCount / 4, 2, 4));
                    threadsCapped = true;
                }

                AbgenResult result = await UniTask.RunOnThreadPool(() => AbgenConverter.Convert(request), cancellationToken: ct);

                if (!result.Succeeded || result.Artifacts.Count == 0) return null;

                await UniTask.SwitchToMainThread();
                return AssetBundle.LoadFromMemory(result.Artifacts[0].Data);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                // The caller's own "bundle is null" error handling stays authoritative.
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
