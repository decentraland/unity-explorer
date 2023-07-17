using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using SceneRunner.ECSWorld.Plugins;
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
        private IRealmController realmController;

        public SceneSharedContainer SceneSharedContainer { get; private set; }

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

                if (realmController != null)
                    await realmController.UnloadCurrentRealm(globalWorld.World);

                globalWorld.Dispose();
            }

            DisposeAsync().Forget();
        }

        private async UniTask InitializeAsync(CancellationToken ct)
        {
            SceneSharedContainer = Install(reportsHandlingSettings);

            Vector3 cameraPosition = ParcelMathHelper.GetPositionByParcelPosition(StartPosition);
            cameraPosition.y += 8.0f;

            camera.transform.position = cameraPosition;

            globalWorld = new GlobalWorld();
            globalWorld.Initialize(SceneSharedContainer.SceneFactory, camera);

            realmController = new RealmController(SceneLoadRadius, StaticLoadPositions);

            await realmController.SetRealm(globalWorld.World, "https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main", destroyCancellationToken);
        }

        public static SceneSharedContainer Install(IReportsHandlingSettings reportsHandlingSettings)
        {
            Profiler.BeginSample($"{nameof(DynamicSceneLoader)}.Install");

            var componentsContainer = ComponentsContainer.Create();
            var sceneSharedContainer = SceneSharedContainer.Create(componentsContainer, reportsHandlingSettings);

            Profiler.EndSample();
            return sceneSharedContainer;
        }
    }
}
