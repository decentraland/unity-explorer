using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using NUnit.Framework;
using System.Collections;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    /// <summary>
    ///     End-to-end proof of the CDN-miss fallback: given only a scene entity id and a GLB content path,
    ///     <see cref="AbgenAssetBundleFallback" /> must discover the GLB's external textures from its JSON
    ///     chunk, resolve them through the scene content mapping, fetch everything by content hash and
    ///     convert it in-process to a loadable AssetBundle.
    /// </summary>
    public class AbgenFallbackFullFlowShould
    {
        [UnityTest]
        public IEnumerator ConvertSingleTextureGlb() =>
            RunFallback("assets/models/pool/admin_toolkit.glb").ToCoroutine();

        [UnityTest]
        public IEnumerator ConvertMultiTextureGlb() =>
            RunFallback("assets/models/pool/airdrop.glb").ToCoroutine();

        private static async UniTask RunFallback(string glbPath)
        {
            AbgenTestScene sceneContent = await AbgenTestScene.FetchAsync(AbgenTestScene.GENESIS_PLAZA);

            AssetBundle bundle = await AbgenAssetBundleFallback.TryBuildAsync(glbPath, sceneContent, IWebRequestController.TEST, ReportData.UNSPECIFIED, CancellationToken.None);

            Assert.IsNotNull(bundle, $"fallback produced no bundle for {glbPath}");

            try
            {
                Object[] assets = bundle.LoadAllAssets();
                var shape = new StringBuilder($"[abgen] {glbPath} -> {assets.Length} asset(s):");
                foreach (Object asset in assets) shape.Append($"\n  {asset.name} ({asset.GetType().Name})");
                Debug.Log(shape.ToString());

                Assert.Greater(assets.Length, 0, "the abgen-built bundle contains no loadable assets");
            }
            finally { bundle.Unload(true); }
        }
    }
}
