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

            poolsRegistry = Substitute.For<IComponentPoolsRegistry>();
            sdkVirtualCameraPool = Substitute.For<IComponentPool<CinemachineFreeLook>>();
            poolsRegistry.GetReferenceTypePool<CinemachineFreeLook>().Returns(sdkVirtualCameraPool);
            sdkVirtualCameraPool.Get().Returns(virtualCamera);

            system = new VirtualCameraSystem(world, sdkVirtualCameraPool, sceneStateProvider);
        }

        [TearDown]
        public async Task Teardown()
        {
            await UniTask.Yield();
            sdkVirtualCameraPool.Dispose();
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

            Assert.IsFalse(world.TryGet(entity, out VirtualCameraComponent vCamComponent));
            system!.Update(1f);
            Assert.IsTrue(world.TryGet(entity, out vCamComponent));

            sdkVirtualCameraPool.Received().Get();

            Assert.AreEqual(lookAtEntity, vCamComponent.lookAtCRDTEntity!.Value.Id);
            Assert.AreSame(vCamComponent.virtualCameraInstance, virtualCamera);
        }

        [Test]
        public async Task HandleComponentRemoveCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var component = new PBVirtualCamera();
            world.Add(entity, component);

            system!.Update(1f);
            Assert.IsTrue(world.TryGet(entity, out VirtualCameraComponent vCamComponent));
            sdkVirtualCameraPool.Received().Get();
            virtualCamera.enabled = true; // emulates being active on the MainCamera component

            world.Remove<PBVirtualCamera>(entity);
            system.Update(1f);
            Assert.IsFalse(world.Has<VirtualCameraComponent>(entity));
            Assert.IsFalse(vCamComponent.virtualCameraInstance.enabled);
            sdkVirtualCameraPool.Received().Release(Arg.Any<CinemachineFreeLook>());
        }

        [Test]
        public async Task HandleEntityDestructionCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var component = new PBVirtualCamera();
            world.Add(entity, component);

            system!.Update(1f);
            Assert.IsTrue(world.TryGet(entity, out VirtualCameraComponent vCamComponent));
            sdkVirtualCameraPool.Received().Get();
            virtualCamera.enabled = true; // emulates being active on the MainCamera component

            world.Add<DeleteEntityIntention>(entity);
            system.Update(1f);
            Assert.IsFalse(vCamComponent.virtualCameraInstance.enabled);
            sdkVirtualCameraPool.Received().Release(Arg.Any<CinemachineFreeLook>());
        }
    }
}
