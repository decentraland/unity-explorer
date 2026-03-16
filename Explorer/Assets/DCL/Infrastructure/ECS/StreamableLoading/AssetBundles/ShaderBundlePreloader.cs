using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.AssetBundles
{
    //TODO FRAN: Validate if this is still necessary.
    /// <summary>
    ///     Preloads shader bundles so they're in memory before wearables and scene asset bundles load.
    ///     Asset Bundle materials reference these shaders but if they aren't loaded, materials deserialize with
    ///     Hidden/InternalErrorShader and textures/colors may appear wrong.
    ///     Bundles are cached so LoadAssetBundleSystem can return them when requested as dependencies.
    /// </summary>
    public static class ShaderBundlePreloader
    {
        /// <summary>
        ///     Returns a preloaded shader bundle if we have it. Used by LoadAssetBundleSystem to avoid loading twice.
        /// </summary>
        public static bool TryGetPreloadedBundle(string hash, out AssetBundle? bundle)
        {
#if !UNITY_WEBGL
            bundle = null;
            return false;
#else
            if (string.IsNullOrEmpty(hash))
            {
                bundle = null;
                return false;
            }

            lock (PRELOADED_BUNDLES) { return PRELOADED_BUNDLES.TryGetValue(hash, out bundle); }
#endif
        }

        /// <summary>
        ///     Preloads shader bundles from StreamingAssets. Call early in bootstrap, before avatar/wearable loading.
        ///     No-op on non-WebGL platforms (shaders are in main build or load differently).
        /// </summary>
        public static async UniTask PreloadAsync(CancellationToken ct = default)
        {
#if !UNITY_WEBGL
            await UniTask.CompletedTask;
            return;
#else
            string streamingAssetsPath = Application.streamingAssetsPath;

            foreach (string bundleName in SHADER_BUNDLES)
            {
                ct.ThrowIfCancellationRequested();

                lock (PRELOADED_BUNDLES)
                {
                    if (PRELOADED_BUNDLES.ContainsKey(bundleName))
                        continue;
                }

                var bundlePath = $"{streamingAssetsPath}/AssetBundles/{bundleName}";

                try
                {
                    using UnityWebRequest? request = UnityWebRequestAssetBundle.GetAssetBundle(bundlePath);
                    await request.SendWebRequest().WithCancellation(ct);

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        AssetBundle? bundle = DownloadHandlerAssetBundle.GetContent(request);

                        if (bundle != null)
                        {
                            bundle.LoadAllAssets<Shader>();
                            ShaderVariantCollection[]? variants = bundle.LoadAllAssets<ShaderVariantCollection>();

                            foreach (ShaderVariantCollection v in variants)
                                v.WarmUp();

                            lock (PRELOADED_BUNDLES) { PRELOADED_BUNDLES[bundleName] = bundle; }
                        }
                    }
                    else
                    {
                        ReportHub.LogWarning(ReportCategory.ASSET_BUNDLES, $"[ShaderBundlePreloader] Failed to load '{bundleName}': {request.error}");
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { ReportHub.LogWarning(ReportCategory.ASSET_BUNDLES, $"[ShaderBundlePreloader] Error loading '{bundleName}': {e.Message}"); }
            }
#endif
        }
#if UNITY_WEBGL
        private static readonly string[] SHADER_BUNDLES =
        {
            "dcl/scene_ignore",
            "dcl/universal render pipeline/lit_ignore",
            "dcl/toon_ignore",
        };

        private static readonly Dictionary<string, AssetBundle> PRELOADED_BUNDLES = new ();
#endif
    }
}
