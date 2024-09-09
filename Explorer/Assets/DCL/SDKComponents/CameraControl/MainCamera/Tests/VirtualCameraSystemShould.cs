using Arch.Core;
using Cinemachine;
using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using DCL.SDKComponents.CameraControl.MainCamera.Systems;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.SDKComponents.CameraControl.MainCamera.Tests
{
    public class VirtualCameraSystemShould : UnitySystemTestBase<VirtualCameraSystem>
    {
        private Entity entity;
        // private TransformComponent entityTransformComponent;
        private ISceneStateProvider sceneStateProvider;
        private IComponentPoolsRegistry poolsRegistry;
        private IComponentPool<CinemachineFreeLook> sdkVirtualCameraPool;

        private CinemachineFreeLook virtualCamera;

        [SetUp]
        public async void Setup()
        {
            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            /*entityTransformComponent = AddTransformToEntity(entity);
            entityTransformComponent.SetTransform(Vector3.one * 30, Quaternion.identity, Vector3.one);
            world.Set(entity, entityTransformComponent);*/

            // Setup system
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            // CinemachineFreeLook virtualCameraPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.VirtualCameraPrefab, ct: ct)).Value.GetComponent<CinemachineFreeLook>();
            GameObject virtualCameraPrefabGO = await Addressables.LoadAssetAsync<GameObject>("SDKVirtualCamera");
            virtualCamera = Object.Instantiate(virtualCameraPrefabGO.GetComponent<CinemachineFreeLook>());
            poolsRegistry = new ComponentPoolsRegistry();
            poolsRegistry.AddGameObjectPool(() => virtualCamera, onGet: virtualCam => virtualCam.enabled = false, onRelease: virtualCam => virtualCam.enabled = false);
            sdkVirtualCameraPool = poolsRegistry.GetReferenceTypePool<CinemachineFreeLook>();

            system = new VirtualCameraSystem(world, sdkVirtualCameraPool, sceneStateProvider);
        }

        [TearDown]
        public void Teardown()
        {
            poolsRegistry.Dispose();
            Object.DestroyImmediate(virtualCamera.gameObject);
        }

        [Test]
        public void SetupVirtualCameraComponentCorrectly()
        {

        }

        [Test]
        public void UpdateVirtualCameraCorrectly()
        {

        }

        [Test]
        public void HandleComponentRemoveCorrectly()
        {

        }

        [Test]
        public void HandleEntityDestructionCorrectly()
        {

        }

        /*[Test]
        public void UpdateCinemachineCorrectly()
        {

        }

        [Test]
        public void UpdateMainCameraTransformValuesCorrectly()
        {

        }

        [Test]
        public void HandleMinimumLookAtDistanceCorrectly()
        {

        }*/

        /*
        - Active VCam updates
        - Ignoring of LookAt with same VCam Entity or with MainCamera Entity
        */
    }
}
