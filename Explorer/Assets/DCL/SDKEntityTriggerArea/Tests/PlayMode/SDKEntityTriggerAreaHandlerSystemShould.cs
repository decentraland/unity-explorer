using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.SDKEntityTriggerArea.Components;
using DCL.SDKEntityTriggerArea.Systems;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Utilities;
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

namespace DCL.SDKEntityTriggerArea.Tests
{
    public class SDKEntityTriggerAreaHandlerSystemShould : UnitySystemTestBase<SDKEntityTriggerAreaHandlerSystem>
    {
        private Entity entity;
        private TransformComponent entityTransformComponent;
        private ISceneStateProvider sceneStateProvider;
        private GameObject fakeMainPlayerGO;
        private GameObject fakeMainPlayerAvatarGO;
        private SDKEntityTriggerArea sdkEntityTriggerArea;
        private ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private IComponentPoolsRegistry poolsRegistry;
        private IComponentPool<SDKEntityTriggerArea> sdkEntityTriggerAreaPool;
        private ICharacterObject characterObject;

        [SetUp]
        public async void Setup()
        {
            entity = world.Create(PartitionComponent.TOP_PRIORITY);

            entityTransformComponent = AddTransformToEntity(entity);
            entityTransformComponent.SetTransform(Vector3.one * 30, Quaternion.identity, Vector3.one);
            world.Set(entity, entityTransformComponent);

            fakeMainPlayerGO = await Addressables.LoadAssetAsync<GameObject>("Character Object");
            fakeMainPlayerGO = Object.Instantiate(fakeMainPlayerGO.GetComponent<CharacterObject>()).gameObject;
            fakeMainPlayerGO.transform.position = Vector3.zero;

            GameObject sdkEntityTriggerAreaBaseGO = await Addressables.LoadAssetAsync<GameObject>("SDKEntityTriggerArea");
            sdkEntityTriggerArea = Object.Instantiate(sdkEntityTriggerAreaBaseGO.GetComponent<SDKEntityTriggerArea>());

            fakeMainPlayerAvatarGO = new GameObject();

            mainPlayerAvatarBaseProxy = new ObjectProxy<AvatarBase>();
            mainPlayerAvatarBaseProxy.SetObject(fakeMainPlayerAvatarGO.AddComponent<AvatarBase>());
            characterObject = Substitute.For<ICharacterObject>();
            characterObject.Transform.Returns(fakeMainPlayerGO.transform);

            // Setup system
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            poolsRegistry = new ComponentPoolsRegistry();
            poolsRegistry.AddGameObjectPool(() => sdkEntityTriggerArea, onRelease: area => area.Dispose());
            sdkEntityTriggerAreaPool = poolsRegistry.GetReferenceTypePool<SDKEntityTriggerArea>();

            system = new SDKEntityTriggerAreaHandlerSystem(world, sdkEntityTriggerAreaPool, mainPlayerAvatarBaseProxy, sceneStateProvider, characterObject);

            UnityEngine.Physics.simulationMode = SimulationMode.Script;
        }

        protected override void OnTearDown()
        {
            poolsRegistry.Dispose();
            Object.DestroyImmediate(entityTransformComponent.Transform.gameObject);
            Object.DestroyImmediate(fakeMainPlayerGO);
            Object.DestroyImmediate(fakeMainPlayerAvatarGO);
        }

        [Test]
        public async Task SetupMonobehaviourCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbComponent = new PBCameraModeArea();
            var component = new SDKEntityTriggerAreaComponent(areaSize: new Vector3(15, 6, 18));

            world.Add(entity, component, pbComponent);

            system.Update(0);

            Assert.IsNotNull(world.Get<SDKEntityTriggerAreaComponent>(entity));
        }

        [Test]
        public async Task SetupTriggerAreaSizeCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var targetAreaSize = new Vector3(15, 6, 18);

            Assert.AreNotEqual(sdkEntityTriggerArea.BoxCollider.size, targetAreaSize);

            var pbComponent = new PBCameraModeArea();
            var component = new SDKEntityTriggerAreaComponent(areaSize: targetAreaSize);

            world.Add(entity, component, pbComponent);

            system.Update(0);

            Assert.AreEqual(targetAreaSize, sdkEntityTriggerArea.BoxCollider.size);
        }

        [Test]
        public async Task UpdateTriggerAreaSizeCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var targetAreaSize = new Vector3(15, 6, 18);

            var pbComponent = new PBCameraModeArea();
            var component = new SDKEntityTriggerAreaComponent(areaSize: targetAreaSize);

            world.Add(entity, component, pbComponent);

            system.Update(0);

            Assert.AreEqual(targetAreaSize, sdkEntityTriggerArea.BoxCollider.size);

            // update component values
            targetAreaSize /= 3;
            component.UpdateAreaSize(targetAreaSize);
            world.Set(entity, component);

            Assert.AreNotEqual(targetAreaSize, sdkEntityTriggerArea.BoxCollider.size);

            system.Update(0);

            Assert.AreEqual(targetAreaSize, sdkEntityTriggerArea.BoxCollider.size);
        }

        [Test]
        public async Task UpdateTransformCorrectlyIgnoringScale()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbComponent = new PBCameraModeArea();
            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one);

            world.Add(entity, component, pbComponent);

            system.Update(0);

            Assert.AreEqual(entityTransformComponent.Transform.position, sdkEntityTriggerArea.transform.position);

            entityTransformComponent.SetTransform(Vector3.one * 10, Quaternion.identity, Vector3.one);
            world.Set(entity, entityTransformComponent);

            system.Update(0);
            Assert.AreEqual(entityTransformComponent.Transform.position, sdkEntityTriggerArea.transform.position);

            entityTransformComponent.SetTransform(Vector3.one * 38, Quaternion.Euler(33, 65, 59), Vector3.one * 66);
            world.Set(entity, entityTransformComponent);

            system.Update(0);

            Assert.AreEqual(entityTransformComponent.Transform.position, sdkEntityTriggerArea.transform.position);
            Assert.AreEqual(entityTransformComponent.Transform.rotation, sdkEntityTriggerArea.transform.rotation);
            Assert.AreNotEqual(entityTransformComponent.Transform.localScale, sdkEntityTriggerArea.BoxCollider.size);
            Assert.AreEqual(Vector3.one, sdkEntityTriggerArea.BoxCollider.size);
        }

        [Test]
        public async Task TrackEnterExitCollectionsCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbComponent = new PBCameraModeArea();

            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4);

            world.Add(entity, component, pbComponent);

            system.Update(0);
            component = world.Get<SDKEntityTriggerAreaComponent>(entity);

            Assert.AreEqual(0, component.EnteredAvatarsToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedAvatarsToBeProcessed.Count);

            // Move character inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;

            await WaitForPhysics();

            Assert.AreEqual(1, component.EnteredAvatarsToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedAvatarsToBeProcessed.Count);
            component.TryClear();

            // Move character outside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position + (Vector3.one * 50);

            await WaitForPhysics();

            Assert.AreEqual(0, component.EnteredAvatarsToBeProcessed.Count);
            Assert.AreEqual(1, component.ExitedAvatarsToBeProcessed.Count);
        }

        [Test]
        public async Task HandlePlayerLeaveSceneCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbComponent = new PBCameraModeArea();

            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4);

            world.Add(entity, component, pbComponent);

            system.Update(0);
            component = world.Get<SDKEntityTriggerAreaComponent>(entity);

            Assert.AreEqual(0, component.EnteredAvatarsToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedAvatarsToBeProcessed.Count);

            // Move character inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;

            await WaitForPhysics();

            Assert.AreEqual(1, component.EnteredAvatarsToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedAvatarsToBeProcessed.Count);
            component.TryClear();

            // Simulate "getting outside current scene"
            sceneStateProvider.IsCurrent.Returns(false);

            system.Update(0);

            Assert.AreEqual(0, component.EnteredAvatarsToBeProcessed.Count);
            Assert.AreEqual(1, component.ExitedAvatarsToBeProcessed.Count);
            Assert.IsFalse(sdkEntityTriggerArea.BoxCollider.enabled);
        }

        [Test]
        public async Task DetectCharacterWhenAlreadyInside()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            // Move character inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;

            var pbComponent = new PBCameraModeArea();

            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4);

            world.Add(entity, component, pbComponent);

            Assert.AreEqual(0, component.EnteredAvatarsToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedAvatarsToBeProcessed.Count);

            system.Update(0);
            await WaitForPhysics();
            component = world.Get<SDKEntityTriggerAreaComponent>(entity);

            Assert.AreEqual(1, component.EnteredAvatarsToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedAvatarsToBeProcessed.Count);
        }

        [Test]
        public async Task WaitUntilAvatarBaseIsConfiguredBeforeDetecting()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            // Move character inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;

            // Use fresh non-initialized MainPlayerAvatarBaseProxy
            mainPlayerAvatarBaseProxy = new ObjectProxy<AvatarBase>();
            system = new SDKEntityTriggerAreaHandlerSystem(world, sdkEntityTriggerAreaPool, mainPlayerAvatarBaseProxy, sceneStateProvider, characterObject);

            var pbComponent = new PBCameraModeArea();

            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4);

            world.Add(entity, component, pbComponent);

            Assert.AreEqual(0, component.EnteredAvatarsToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedAvatarsToBeProcessed.Count);

            system.Update(0);
            await WaitForPhysics();
            component = world.Get<SDKEntityTriggerAreaComponent>(entity);

            Assert.AreEqual(0, component.EnteredAvatarsToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedAvatarsToBeProcessed.Count);

            mainPlayerAvatarBaseProxy.SetObject(fakeMainPlayerAvatarGO.GetComponent<AvatarBase>());

            system.Update(0);
            await WaitForPhysics();
            component = world.Get<SDKEntityTriggerAreaComponent>(entity);

            Assert.AreEqual(1, component.EnteredAvatarsToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedAvatarsToBeProcessed.Count);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task DiscriminateCharacterTypeCorrectly(bool onlyMainPlayer)
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            GameObject? fakeOtherPlayerGO = await Addressables.LoadAssetAsync<GameObject>("Character Object");
            fakeOtherPlayerGO = Object.Instantiate(fakeOtherPlayerGO.GetComponent<CharacterObject>()).gameObject;
            fakeOtherPlayerGO.transform.position = Vector3.zero;

            var pbComponent = new PBCameraModeArea();
            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4, targetOnlyMainPlayer: onlyMainPlayer);

            world.Add(entity, component, pbComponent);

            system.Update(0);
            component = world.Get<SDKEntityTriggerAreaComponent>(entity);

            Assert.AreEqual(0, component.EnteredAvatarsToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedAvatarsToBeProcessed.Count);

            // Move both characters inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;
            fakeOtherPlayerGO.transform.position = entityTransformComponent.Transform.position;

            await WaitForPhysics();

            Assert.AreEqual(onlyMainPlayer ? 1 : 2, component.EnteredAvatarsToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedAvatarsToBeProcessed.Count);
            component.TryClear();

            // Move both characters outside area
            fakeMainPlayerGO.transform.position += Vector3.one * 30;
            fakeOtherPlayerGO.transform.position += Vector3.one * 30;

            await WaitForPhysics();

            Assert.AreEqual(0, component.EnteredAvatarsToBeProcessed.Count);
            Assert.AreEqual(onlyMainPlayer ? 1 : 2, component.ExitedAvatarsToBeProcessed.Count);

            // Cleanup
            Object.DestroyImmediate(fakeOtherPlayerGO);
        }

        private static async Task WaitForPhysics()
        {
            // Wait several frames to allow CI detect physics on its non-deterministic hardware.
            var framesToWait = 10;

            for (var i = 0; i < framesToWait; i++)
            {
                UnityEngine.Physics.Simulate(0.01f);
                await UniTask.Yield();
            }
        }
    }
}
