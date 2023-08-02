using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.CharacterCamera.Settings;
using Diagnostics.ReportsHandling;
using ECS.Prioritization;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using Utility;

namespace Global.Dynamic
{
    /// <summary>
    ///     An entry point to install and resolve all dependencies
    /// </summary>
    public class DynamicSceneLoader : MonoBehaviour
    {
        [SerializeField] private CharacterObject character;
        [SerializeField] private CinemachinePreset camera;
        [SerializeField] private Vector2Int StartPosition;
        [SerializeField] private int SceneLoadRadius = 4;
        [SerializeField] private ReportsHandlingSettings reportsHandlingSettings;
        [SerializeField] private RealmPartitionSettingsAsset realmPartitionSettingsAsset;
        [SerializeField] private PartitionSettingsAsset partitionSettingsAsset;

        // If it's 0, it will load every parcel in the range
        [SerializeField] private List<int2> StaticLoadPositions;
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

                await dynamicWorldContainer.RealmController.DisposeGlobalWorld(globalWorld).SuppressCancellationThrow();
            }

            DisposeAsync().Forget();
        }

        private async UniTask InitializeAsync(CancellationToken ct)
        {
            Install();

            Vector3 cameraPosition = ParcelMathHelper.GetPositionByParcelPosition(StartPosition);
            cameraPosition.y += 8.0f;
            camera.transform.position = cameraPosition;

            globalWorld = dynamicWorldContainer.GlobalWorldFactory.Create(sceneSharedContainer.SceneFactory, dynamicWorldContainer.EmptyScenesWorldFactory, character);
            await dynamicWorldContainer.RealmController.SetRealm(globalWorld, "https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main", destroyCancellationToken);
        }

        internal void Install()
        {
            Profiler.BeginSample($"{nameof(DynamicSceneLoader)}.Install");

            staticContainer = StaticContainer.Create(partitionSettingsAsset, reportsHandlingSettings);
            sceneSharedContainer = SceneSharedContainer.Create(in staticContainer);
            dynamicWorldContainer = DynamicWorldContainer.Create(in staticContainer, realmPartitionSettingsAsset, StaticLoadPositions, SceneLoadRadius);

            Profiler.EndSample();
        }
    }
}
