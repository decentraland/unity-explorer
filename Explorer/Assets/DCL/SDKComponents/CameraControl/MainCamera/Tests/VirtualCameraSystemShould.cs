using Arch.Core;
using Cinemachine;
using CRDT;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.CameraControl.MainCamera.Components;
using DCL.SDKComponents.CameraControl.MainCamera.Systems;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.SDKComponents.CameraControl.MainCamera.Tests
{
    public class VirtualCameraSystemShould : UnitySystemTestBase<VirtualCameraSystem>
    {
        private Entity entity;
        private ISceneStateProvider sceneStateProvider;
        private IComponentPoolsRegistry poolsRegistry;
        private IComponentPool<CinemachineFreeLook> sdkVirtualCameraPool;
        private CinemachineFreeLook virtualCamera;

        [SetUp]
        public async void Setup()
        {
            entity = world.Create(PartitionComponent.TOP_PRIORITY, new CRDTEntity(565), new TransformComponent());

            // Setup system
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

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
            world.Dispose();
        }

        [Test]
        public async Task SetupVirtualCameraComponentCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            uint lookAtEntity = 358;
            var component = new PBVirtualCamera()
            {
                LookAtEntity = lookAtEntity,
                IsDirty = true
            };

            world.Add(entity, component);

            system.Update(1f);
            Assert.AreEqual(sdkVirtualCameraPool.CountInactive, 0);

            Assert.IsTrue(world.TryGet(entity, out VirtualCameraComponent vCamComponent));
            Assert.AreEqual(vCamComponent.lookAtCRDTEntity!.Value.Id, lookAtEntity);
            Assert.AreSame(vCamComponent.virtualCameraInstance, virtualCamera);
        }

        [Test]
        public async Task HandleComponentRemoveCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.Yield();
            await UniTask.WaitUntil(() => system != null);

            var component = new PBVirtualCamera();
            world.Add(entity, component);

            system.Update(1f);
            Assert.IsTrue(world.Has<VirtualCameraComponent>(entity));
            Assert.AreEqual(sdkVirtualCameraPool.CountInactive, 0);
            virtualCamera.enabled = true; // emulates being active on the MainCamera component

            world.Remove<PBVirtualCamera>(entity);
            system.Update(1f);
            Assert.IsFalse(world.Has<VirtualCameraComponent>(entity));
            Assert.IsFalse(virtualCamera.enabled);
            Assert.AreEqual(sdkVirtualCameraPool.CountInactive, 1);
        }

        [Test]
        public async Task HandleEntityDestructionCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.Yield();
            await UniTask.WaitUntil(() => system != null);

            var component = new PBVirtualCamera();
            world.Add(entity, component);

            system.Update(1f);
            Assert.IsTrue(world.Has<VirtualCameraComponent>(entity));
            Assert.AreEqual(sdkVirtualCameraPool.CountInactive, 0);
            virtualCamera.enabled = true; // emulates being active on the MainCamera component

            world.Add<DeleteEntityIntention>(entity);
            system.Update(1f);
            Assert.IsFalse(virtualCamera.enabled);
            Assert.AreEqual(sdkVirtualCameraPool.CountInactive, 1);
        }
    }
}
