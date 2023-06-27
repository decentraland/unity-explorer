using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using SceneRunner.ECSWorld.Plugins;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using Utility;

namespace Global
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

        // If it's 0, it will load every parcel in the range
        [SerializeField] private List<Vector2Int> StaticLoadPositions;
        private GlobalWorld globalWorld;

        private ISceneFacade sceneFacade;

        public SceneSharedContainer SceneSharedContainer { get; private set; }

        private void Awake()
        {
            InitializeAsync(destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            globalWorld?.Dispose();
        }

        private async UniTask InitializeAsync(CancellationToken ct)
        {
            // HACK!!! Load Local Asset Bundle Manifest Once

            UnityWebRequest wr = UnityWebRequestAssetBundle.GetAssetBundle($"{AssetBundlesPlugin.STREAMING_ASSETS_URL}AssetBundles");
            await wr.SendWebRequest().WithCancellation(ct);
            AssetBundle manifestAssetBundle = DownloadHandlerAssetBundle.GetContent(wr);
            AssetBundleManifest assetBundleManifest = manifestAssetBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");

            SceneSharedContainer = Install(assetBundleManifest, reportsHandlingSettings);

            Vector3 cameraPosition = ParcelMathHelper.GetPositionByParcelPosition(StartPosition);
            cameraPosition.y += 8.0f;

            camera.transform.position = cameraPosition;

            globalWorld = new GlobalWorld();

            List<Vector2Int> staticLoadPositions = StaticLoadPositions.Count > 0 ? StaticLoadPositions : null;

            globalWorld.Initialize(SceneSharedContainer.SceneFactory, camera, SceneLoadRadius, staticLoadPositions);
        }

        public static SceneSharedContainer Install(AssetBundleManifest localManifest, IReportsHandlingSettings reportsHandlingSettings)
        {
            Profiler.BeginSample($"{nameof(DynamicSceneLoader)}.Install");

            var componentsContainer = ComponentsContainer.Create();
            var sceneSharedContainer = SceneSharedContainer.Create(componentsContainer, localManifest, reportsHandlingSettings);

            Profiler.EndSample();
            return sceneSharedContainer;
        }
    }
}
