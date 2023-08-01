using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.Unity.GLTFContainer.Asset.Tests
{
    public class GltfContainerTestResources
    {
        // 1 Collider, 196 Mesh Renderers
        internal const string SCENE_WITH_COLLIDER = "bafybeigwxyfyyarzmvqz262vet65xa2ovetct6hcnm27uwge7yxpmhfvoe";

        // 1 Mesh Renderer
        internal const string SIMPLE_RENDERER = "bafkreif6qazpaiulr6kcqgukopkw6r26lawpnisdjdoddaqeujd5ytaezy";

        internal const string NO_GAME_OBJECTS = "bafkreid3xecd44iujaz5qekbdrt5orqdqj3wivg5zc5mya3zkorjhyrkda";

        internal static readonly string TEST_FOLDER = $"file://{Application.dataPath + "/../TestResources/AssetBundles/"}";

        internal AssetBundle assetBundle;

        internal async UniTask<StreamableLoadingResult<AssetBundleData>> LoadAssetBundle(string hash)
        {
            using UnityWebRequest wr = UnityWebRequestAssetBundle.GetAssetBundle($"{TEST_FOLDER}{hash}");
            await wr.SendWebRequest();
            assetBundle = DownloadHandlerAssetBundle.GetContent(wr);

            GameObject gameObject = assetBundle.LoadAllAssets<GameObject>().Length > 0 ? assetBundle.LoadAllAssets<GameObject>()[0] : null;
            return new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(assetBundle, null, gameObject));
        }

        internal void UnloadBundle()
        {
            assetBundle?.Unload(false);
            assetBundle = null;
        }
    }
}
