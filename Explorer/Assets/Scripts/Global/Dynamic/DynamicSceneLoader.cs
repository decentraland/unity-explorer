using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.CharacterCamera.Settings;
using Diagnostics.ReportsHandling;
using ECS.CharacterMotion.Settings;
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
        [SerializeField] private CharacterControllerSettings characterControllerSettings;

        // If it's 0, it will load every parcel in the range
        [SerializeField] private List<int2> StaticLoadPositions;
        private DynamicWorldContainer dynamicWorldContainer;

        private GlobalWorld globalWorld;

        private SceneSharedContainer sceneSharedContainer;
        private StaticContainer staticContainer;

        [SerializeField] private RealmLauncher realmLauncher;
        [SerializeField] private string[] realms;

        private void Awake()
        {
            realmLauncher.Initialize(realms);
            Install();

            realmLauncher.OnRealmSelected += SetRealm;
        }

        private void SetRealm(string selectedRealm)
        {
            ChangeRealm(destroyCancellationToken, selectedRealm).Forget();
        }

        private void OnDestroy()
        {
            async UniTaskVoid DisposeAsync()
            {
                if (globalWorld == null)
                    return;

                await dynamicWorldContainer.RealmController.DisposeGlobalWorld(globalWorld).SuppressCancellationThrow();
            }

            realmLauncher.OnRealmSelected -= SetRealm;
            DisposeAsync().Forget();
        }

        private async UniTask ChangeRealm(CancellationToken ct, string selectedRealm)
        {
            if (globalWorld != null)
                await dynamicWorldContainer.RealmController.UnloadCurrentRealm(globalWorld);

            await UniTask.SwitchToMainThread();

            Vector3 cameraPosition = ParcelMathHelper.GetPositionByParcelPosition(StartPosition);
            cameraPosition.y += 1f;
            character.transform.position = cameraPosition;
            await dynamicWorldContainer.RealmController.SetRealm(globalWorld, selectedRealm, ct);
        }

        internal void Install()
        {
            Profiler.BeginSample($"{nameof(DynamicSceneLoader)}.Install");

            staticContainer = StaticContainer.Create(partitionSettingsAsset, reportsHandlingSettings);
            sceneSharedContainer = SceneSharedContainer.Create(in staticContainer);

            dynamicWorldContainer = DynamicWorldContainer.Create(
                in staticContainer,
                realmPartitionSettingsAsset,
                camera,
                characterControllerSettings,
                character,
                StaticLoadPositions,
                SceneLoadRadius);

            globalWorld = dynamicWorldContainer.GlobalWorldFactory.Create(sceneSharedContainer.SceneFactory, dynamicWorldContainer.EmptyScenesWorldFactory, character);

            Profiler.EndSample();
        }
    }
}
