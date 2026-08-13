using Cysharp.Threading.Tasks;
using Global.Dynamic;
using NUnit.Framework;
using System;
using System.Collections;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    /// <summary>
    ///     End-to-end proof of the sidecar architecture: spawn the abgen JIT server exactly the way
    ///     MainSceneLoader does, JIT-convert a real (small) scene through the manifest lane,
    ///     fetch a produced bundle through the same UnityWebRequestAssetBundle path the client's loading
    ///     flow uses, and verify Dispose kills the child.
    /// </summary>
    public class AbgenSidecarShould
    {
        private const string SMALL_SCENE = "bafkreicylzyfld7ittipww6rot5oeldgikc77222d64lwyp2m4slr43lny";

        [UnityTest]
        public IEnumerator ConvertAndServeASceneJit() =>
            RunAsync().ToCoroutine();

        private static async UniTask RunAsync()
        {
            if (!File.Exists(AbgenSidecar.StreamingAssetsExecutablePath))
                Assert.Ignore($"abgen server executable not provisioned at {AbgenSidecar.StreamingAssetsExecutablePath}");

            string cacheRoot = Path.Combine(Path.GetTempPath(), "abgen-sidecar-test-" + Guid.NewGuid().ToString("N")[..8]);

            AbgenSidecar? created = AbgenSidecar.TryCreate(AbgenSidecar.ReserveBaseUrl(), "org", cacheRoot);
            Assert.IsNotNull(created, "no abgen binary was resolved");
            AbgenSidecar sidecar = created!;
            Assert.IsTrue(await sidecar.StartAsync(CancellationToken.None), "sidecar did not become healthy");

            try
            {
                // The manifest request JIT-converts the whole (2-GLB) entity.
                using UnityWebRequest manifestReq = UnityWebRequest.Get($"{sidecar.BaseUrl}/manifest/{SMALL_SCENE}_windows.json");
                manifestReq.timeout = 180;
                await manifestReq.SendWebRequest();
                Assert.AreEqual(UnityWebRequest.Result.Success, manifestReq.result, $"manifest JIT failed: {manifestReq.error}");

                var manifest = JsonUtility.FromJson<ManifestDto>(manifestReq.downloadHandler.text);
                Assert.Greater(manifest.files.Length, 0, "manifest lists no bundles");
                Debug.Log($"[abgen-sidecar] JIT manifest: [{string.Join(", ", manifest.files)}]");

                string? bundleName = null;

                foreach (string file in manifest.files)
                    if (file.EndsWith("_windows", StringComparison.Ordinal) && file.Length > 20)
                    {
                        bundleName = file;
                        break;
                    }

                Assert.IsNotNull(bundleName, "no per-model bundle in the JIT manifest");

                using UnityWebRequest bundleReq = UnityWebRequestAssetBundle.GetAssetBundle($"{sidecar.BaseUrl}/v49/{SMALL_SCENE}/{bundleName}");
                bundleReq.timeout = 60;
                await bundleReq.SendWebRequest();
                Assert.AreEqual(UnityWebRequest.Result.Success, bundleReq.result, $"bundle fetch failed: {bundleReq.error}");

                AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(bundleReq);
                Assert.IsNotNull(bundle, "served bytes are not a Unity AssetBundle");

                try
                {
                    Assert.Greater(bundle.GetAllAssetNames().Length, 0, "served bundle is empty");
                }
                finally { bundle.Unload(true); }
            }
            finally { sidecar.Dispose(); }

            // After Dispose the server must be gone: the port stops answering.
            await UniTask.Delay(500);

            using UnityWebRequest dead = UnityWebRequest.Head(sidecar.BaseUrl);
            dead.timeout = 2;

            try { await dead.SendWebRequest(); }
            catch (Exception) { /* connection refused is the expected outcome */ }

            Assert.AreEqual(0, dead.responseCode, "sidecar still listening after Dispose");
        }

        // Server schema: abgen /manifest/{entity}_{platform}.json response.
        [Serializable]
        private class ManifestDto
        {
            public string[] files = null!;
        }
    }
}
