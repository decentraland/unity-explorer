using Cysharp.Threading.Tasks;
using DCL.Utility;
using Decentraland.Abgen;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    /// <summary>
    ///     Visual proof that abgen-built bundles actually render through the client's own mechanics: convert
    ///     a real multi-texture model exactly as the fallback does (ConvertOnly + OnlyGlb), resolve the
    ///     bundle's metadata.json dependencies the way LoadAssetBundleSystem does — shader bundles from
    ///     StreamingAssets, texture bundles from the live CDN — instantiate the prefab, photograph it and
    ///     save the PNG for human inspection. The CDN control runs the identical harness on the real CDN
    ///     bundle of the same model, separating abgen defects from harness defects.
    /// </summary>
    public class AbgenFallbackVisualShould
    {
        private const string GLB = "assets/models/pool/airdrop.glb";
        private const string CDN_ASSETS = "https://ab-cdn.decentraland.org/v49/assets/";
        private const string CDN_BUNDLE = "bafkreiatt27yto3kmdsy4leml37dunzuzsgcdimylhwo67iz74j6xudsee_e5c72fc3319f18e60d1433628075dba9_windows";
        private const int SIZE = 768;

        private static readonly string[] FILES =
        {
            GLB,
            "assets/models/pool/airdrop_mat_baseColor.png",
            "assets/models/pool/airdrop_mat_emissive.png",
            "assets/models/pool/airdrop_mat_normal.png",
        };

        [UnityTest]
        public IEnumerator RenderConvertedModelToPng() =>
            RunAbgenAsync().ToCoroutine();

        [UnityTest]
        public IEnumerator RenderCdnControlToPng() =>
            RunCdnControlAsync().ToCoroutine();

        private static async UniTask RunAbgenAsync()
        {
            AbgenTestScene scene = await AbgenTestScene.FetchAsync(AbgenTestScene.GENESIS_PLAZA);
            Assert.IsTrue(scene.TryGetHash(GLB, out string glbHash));

            var request = new AbgenRequest { Platform = PlatformUtils.GetCurrentPlatform().TrimStart('_'), Mode = AbgenMode.ConvertOnly, OnlyGlb = GLB, EntityHash = glbHash };

            foreach (string file in FILES)
            {
                Assert.IsTrue(scene.TryGetHash(file, out string hash), $"{file} missing from scene content");
                request.AddFile(file, await AbgenTestScene.FetchContentAsync(hash)).AddContentEntry(file, hash);
            }

            AbgenResult result = await UniTask.RunOnThreadPool(() => AbgenConverter.Convert(request));
            Assert.IsTrue(result.Succeeded, $"abgen conversion failed: {string.Join(" | ", result.Errors)}");
            Assert.Greater(result.Artifacts.Count, 0, "abgen produced no artifact");
            Debug.Log($"[abgen-visual] converted: {result.Artifacts[0].Name} ({result.Artifacts[0].Data.Length}B)");

            await UniTask.SwitchToMainThread();
            await RenderBundleAsync(result.Artifacts[0].Data, "abgen");
        }

        private static async UniTask RunCdnControlAsync()
        {
            using UnityWebRequest req = UnityWebRequest.Get(CDN_ASSETS + CDN_BUNDLE);
            await req.SendWebRequest();
            Assert.AreEqual(UnityWebRequest.Result.Success, req.result, $"CDN fetch of {CDN_BUNDLE} failed: {req.error}");

            await RenderBundleAsync(req.downloadHandler.data, "cdn");
        }

        private static async UniTask RenderBundleAsync(byte[] mainBundleBytes, string tag)
        {
            string output = $@"C:\bugtest2\results\abgen-visual-{tag}.png";
            var bundles = new List<AssetBundle>();

            try
            {
                // The scene shader lives in the client's embedded bundle (CAB-51fbd4c9..., referenced by
                // every scene GLB bundle); in-world any earlier bundle load brings it in — do it explicitly here.
                string platform = PlatformUtils.GetCurrentPlatform();

                foreach (string shaderBundle in new[] { $"dcl/scene_ignore{platform}", $"dcl/universal render pipeline/lit_ignore{platform}" })
                {
                    AssetBundle loaded = AssetBundle.LoadFromFile(Path.Combine(Application.streamingAssetsPath, "AssetBundles", shaderBundle));
                    Assert.IsNotNull(loaded, $"[{tag}] embedded shader bundle missing: {shaderBundle}");
                    bundles.Add(loaded);
                }

                AssetBundle mainBundle = AssetBundle.LoadFromMemory(mainBundleBytes);
                Assert.IsNotNull(mainBundle, $"[{tag}] LoadFromMemory failed");
                bundles.Add(mainBundle);

                // Resolve dependencies the way LoadAssetBundleSystem does: metadata.json names them;
                // embedded shader bundles come from StreamingAssets, the rest from the CDN.
                TextAsset metadataJson = mainBundle.LoadAsset<TextAsset>("metadata.json");
                Assert.IsNotNull(metadataJson, $"[{tag}] bundle carries no metadata.json");
                var metadata = JsonUtility.FromJson<AssetBundleMetadata>(metadataJson.text);
                Debug.Log($"[abgen-visual] [{tag}] mainAsset={metadata.mainAsset} deps=[{string.Join(", ", metadata.dependencies)}]");

                foreach (string dependency in metadata.dependencies)
                {
                    string embeddedPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles", dependency);
                    AssetBundle depBundle;

                    if (File.Exists(embeddedPath))
                        depBundle = AssetBundle.LoadFromFile(embeddedPath);
                    else
                    {
                        using UnityWebRequest req = UnityWebRequest.Get(CDN_ASSETS + dependency);
                        await req.SendWebRequest();
                        Assert.AreEqual(UnityWebRequest.Result.Success, req.result, $"[{tag}] CDN fetch of dependency {dependency} failed: {req.error}");
                        depBundle = AssetBundle.LoadFromMemory(req.downloadHandler.data);
                    }

                    Assert.IsNotNull(depBundle, $"[{tag}] failed to load dependency bundle {dependency}");
                    bundles.Add(depBundle);
                }

                GameObject prefab = string.IsNullOrEmpty(metadata.mainAsset) ? null : mainBundle.LoadAsset<GameObject>(metadata.mainAsset);

                if (prefab == null)
                {
                    GameObject[] prefabs = mainBundle.LoadAllAssets<GameObject>();
                    if (prefabs.Length > 0) prefab = prefabs[0];
                }

                Assert.IsNotNull(prefab, $"[{tag}] no GameObject prefab in the bundle");

                GameObject instance = Object.Instantiate(prefab);
                var lightGo = new GameObject("abgen-visual-light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 2f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                var bounds = new Bounds(instance.transform.position, Vector3.zero);
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                Assert.Greater(bounds.extents.magnitude, 0f, $"[{tag}] instantiated model has no renderable bounds");

                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                    foreach (Material material in renderer.sharedMaterials)
                        Debug.Log($"[abgen-visual] [{tag}] renderer={renderer.name} shader={material.shader?.name ?? "NULL"} mainTex={(material.mainTexture != null ? material.mainTexture.name : "NONE")}");

                var camGo = new GameObject("abgen-visual-cam");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.10f, 0.10f, 0.25f);
                camGo.transform.position = bounds.center + (new Vector3(1f, 0.7f, 1f).normalized * ((bounds.extents.magnitude * 2.2f) + 0.5f));
                camGo.transform.LookAt(bounds.center);

                var rt = new RenderTexture(SIZE, SIZE, 24);
                cam.targetTexture = rt;

                // SRP renders all active cameras per frame; a manual Camera.Render() is unsupported there.
                for (var i = 0; i < 5; i++) await UniTask.Yield();

                RenderTexture.active = rt;
                var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, SIZE, SIZE), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                File.WriteAllBytes(output, tex.EncodeToPNG());

                Color32[] pixels = tex.GetPixels32();
                var distinct = new HashSet<int>();
                var modelPixels = 0;
                var background = (Color32)cam.backgroundColor;

                foreach (Color32 p in pixels)
                {
                    distinct.Add((p.r << 16) | (p.g << 8) | p.b);

                    if (Mathf.Abs(p.r - background.r) + Mathf.Abs(p.g - background.g) + Mathf.Abs(p.b - background.b) > 24)
                        modelPixels++;
                }

                float coverage = (float)modelPixels / pixels.Length;
                Debug.Log($"[abgen-visual] [{tag}] wrote {output}: {distinct.Count} distinct colors, {coverage:P1} model coverage");

                Assert.Greater(distinct.Count, 50, $"[{tag}] render is nearly flat — model likely untextured or did not draw");
                Assert.Greater(coverage, 0.02f, $"[{tag}] model covers almost none of the frame — likely invisible");

                Object.Destroy(instance);
                Object.Destroy(camGo);
                Object.Destroy(lightGo);
                Object.Destroy(rt);
            }
            finally
            {
                foreach (AssetBundle bundle in bundles) bundle.Unload(true);
            }
        }
    }
}
