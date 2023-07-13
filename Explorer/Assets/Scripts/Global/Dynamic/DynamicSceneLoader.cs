using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.Prioritization;
using SceneRunner.ECSWorld.Plugins;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using Utility;

namespace Global.Dynamic
{
    /// <summary>
    ///     An entry point to install and resolve all dependencies
    /// </summary>
    public class DynamicSceneLoader : MonoBehaviour
    {
        [SerializeField] private Camera camera;
        [SerializeField] private Vector2Int StartPosition;
        [SerializeField] private int SceneLoadRadius = 4;
        [SerializeField] private ReportsHandlingSettings reportsHandlingSettings;
        [SerializeField] private RealmPartitionSettingsAsset realmPartitionSettingsAsset;
        [SerializeField] private PartitionSettingsAsset partitionSettingsAsset;

        // If it's 0, it will load every parcel in the range
        [SerializeField] private List<Vector2Int> StaticLoadPositions;
        private DynamicWorldContainer dynamicWorldContainer;

        private GlobalWorld globalWorld;

        private SceneSharedContainer sceneSharedContainer;
        private StaticContainer staticContainer;

        private void Awake()
        {
            InitializeAsync(destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            async UniTaskVoid DisposeAsync()
            {
                if (globalWorld == null)
                    return;

                if (dynamicWorldContainer != null)
                    await dynamicWorldContainer.RealmController.UnloadCurrentRealm(globalWorld);

                globalWorld.Dispose();
            }

            DisposeAsync().Forget();
        }

        private async UniTask InitializeAsync(CancellationToken ct)
        {
            // HACK!!! Load Local Asset Bundle Manifest Once
            UnityWebRequest wr = UnityWebRequestAssetBundle.GetAssetBundle($"{AssetBundlesPlugin.STREAMING_ASSETS_URL}AssetBundles");
            await wr.SendWebRequest().WithCancellation(ct);
            AssetBundle manifestAssetBundle = DownloadHandlerAssetBundle.GetContent(wr);
            AssetBundleManifest assetBundleManifest = manifestAssetBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");

            Install(assetBundleManifest);

            Vector3 cameraPosition = ParcelMathHelper.GetPositionByParcelPosition(StartPosition);
            cameraPosition.y += 8.0f;
            camera.transform.position = cameraPosition;

            globalWorld = dynamicWorldContainer.GlobalWorldFactory.Create(sceneSharedContainer.SceneFactory, camera);
            await dynamicWorldContainer.RealmController.SetRealm(globalWorld, "https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main", destroyCancellationToken);
        }

        internal void Install(AssetBundleManifest localManifest)
        {
            Profiler.BeginSample($"{nameof(DynamicSceneLoader)}.Install");

            staticContainer = StaticContainer.Create(partitionSettingsAsset, reportsHandlingSettings);
            sceneSharedContainer = SceneSharedContainer.Create(in staticContainer, localManifest);
            dynamicWorldContainer = DynamicWorldContainer.Create(in staticContainer, realmPartitionSettingsAsset, StaticLoadPositions, SceneLoadRadius);

            Profiler.EndSample();
        }
    }
}
