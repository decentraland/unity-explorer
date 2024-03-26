using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.Unity.GLTFContainer.Asset.Tests
{
    public class GltfContainerTestResources
    {
        public const string ANIMATION = "shark";


        // 1 Collider, 196 Mesh Renderers, 0 Animations
        internal const string SCENE_WITH_COLLIDER = "bafybeigwxyfyyarzmvqz262vet65xa2ovetct6hcnm27uwge7yxpmhfvoe";

        // 1 Mesh Renderer // 1 Animation
        public const string SIMPLE_RENDERER = "bafkreif6qazpaiulr6kcqgukopkw6r26lawpnisdjdoddaqeujd5ytaezy";

        internal const string NO_GAME_OBJECTS = "bafkreid3xecd44iujaz5qekbdrt5orqdqj3wivg5zc5mya3zkorjhyrkda";

        internal static readonly string TEST_FOLDER = $"file://{Application.dataPath + "/../TestResources/AssetBundles/"}";

        internal AssetBundle assetBundle;

        public async UniTask<StreamableLoadingResult<AssetBundleData>> LoadAssetBundle(string hash)
        {
            using UnityWebRequest wr = UnityWebRequestAssetBundle.GetAssetBundle($"{TEST_FOLDER}{hash}");
            await wr.SendWebRequest();
            assetBundle = DownloadHandlerAssetBundle.GetContent(wr);

            try
            {
                return await LoadAssetBundleSystem.CreateAssetBundleData(assetBundle, null, typeof(GameObject), new AssetBundleLoadingMutex(), Array.Empty<AssetBundleData>(),
                    ReportCategory.ASSET_BUNDLES, CancellationToken.None);
            }
            catch (Exception e)
            {
                return new StreamableLoadingResult<AssetBundleData>(e);
            }
        }

        public void UnloadBundle()
        {
            assetBundle?.Unload(false);
            assetBundle = null;
        }
    }
}
