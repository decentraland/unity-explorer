using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Preloads shader bundles so they're in memory before wearable/GLTF asset bundles load.
    ///     Wearable materials reference DCL_Toon; if the shader isn't loaded, materials deserialize with
    ///     Hidden/InternalErrorShader and textures/colors appear wrong.
    ///     Bundles are cached so LoadAssetBundleSystem can return them when requested as dependencies.
    /// </summary>
    public static class ShaderBundlePreloader
    {
#if UNITY_WEBGL
        private static readonly string[] SHADER_BUNDLES =
        {
            "dcl/scene_ignore",
            "dcl/universal render pipeline/lit_ignore",
            "dcl/toon_ignore",
        };

        private static readonly Dictionary<string, AssetBundle> PreloadedBundles = new();
#endif

        /// <summary>
        ///     Returns a preloaded shader bundle if we have it. Used by LoadAssetBundleSystem to avoid loading twice.
        /// </summary>
        public static bool TryGetPreloadedBundle(string hash, out AssetBundle? bundle)
        {
#if !UNITY_WEBGL
            bundle = null;
            return false;
#else
            if (hash == null)
            {
                bundle = null;
                return false;
            }
            lock (PreloadedBundles)
            {
                return PreloadedBundles.TryGetValue(hash, out bundle);
            }
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

                lock (PreloadedBundles)
                {
                    if (PreloadedBundles.ContainsKey(bundleName))
                        continue;
                }

                string bundlePath = $"{streamingAssetsPath}/AssetBundles/{bundleName}";

                try
                {
                    using var request = UnityWebRequestAssetBundle.GetAssetBundle(bundlePath);
                    await request.SendWebRequest().WithCancellation(ct);

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        AssetBundle? bundle = DownloadHandlerAssetBundle.GetContent(request);
                        if (bundle != null)
                        {
                            bundle.LoadAllAssets<Shader>();
                            var variants = bundle.LoadAllAssets<ShaderVariantCollection>();
                            foreach (var v in variants)
                                v.WarmUp();

                            lock (PreloadedBundles)
                            {
                                PreloadedBundles[bundleName] = bundle;
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[ShaderBundlePreloader] Failed to load '{bundleName}': {request.error}");
                    }
                }
                catch (System.OperationCanceledException)
                {
                    throw;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[ShaderBundlePreloader] Error loading '{bundleName}': {e.Message}");
                }
            }
#endif
        }
    }
}
