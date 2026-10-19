using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.SDKEntityTriggerArea.Components;
using DCL.SDKEntityTriggerArea.Systems;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility.Multithreading;
using CrdtEcsBridge.Physics;

namespace DCL.SDKEntityTriggerArea.Tests
{
    public class SDKEntityTriggerAreaHandlerSystemShould : UnitySystemTestBase<SDKEntityTriggerAreaHandlerSystem>
    {
        private Entity entity;
        private TransformComponent entityTransformComponent;
        private ISceneStateProvider sceneStateProvider = null!;
        private GameObject fakeMainPlayerGO = null!;
        private GameObject fakeMainPlayerAvatarGO = null!;
        private SDKEntityTriggerArea sdkEntityTriggerArea = null!;
        private ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy = null!;
        private IComponentPoolsRegistry poolsRegistry = null!;
        private IComponentPool<SDKEntityTriggerArea> sdkEntityTriggerAreaPool = null!;
        private ICharacterObject characterObject = null!;
        // NUnit reuses the fixture instance, so the async SetUp resets it before its first await
        private bool isSetUp;

        [SetUp]
        public async void Setup()
        {
            isSetUp = false;

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
            sceneStateProvider.State.Returns(new Atomic<SceneState>(SceneState.Running));

            poolsRegistry = new ComponentPoolsRegistry();
            poolsRegistry.AddGameObjectPool(() => sdkEntityTriggerArea, onRelease: area => area.Dispose());
            sdkEntityTriggerAreaPool = poolsRegistry.GetReferenceTypePool<SDKEntityTriggerArea>();

            system = new SDKEntityTriggerAreaHandlerSystem(world, sdkEntityTriggerAreaPool, mainPlayerAvatarBaseProxy, sceneStateProvider, characterObject);

            Physics.simulationMode = SimulationMode.Script;
            isSetUp = true;
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
            await UniTask.WaitUntil(() => isSetUp);

            var component = new SDKEntityTriggerAreaComponent(areaSize: new Vector3(15, 6, 18));

            world.Add(entity, component);

            system.Update(0);

            Assert.IsNotNull(world.Get<SDKEntityTriggerAreaComponent>(entity));
        }

        [Test]
        public async Task SetupTriggerAreaSizeCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);

            var targetAreaSize = new Vector3(15, 6, 18);

            Assert.AreNotEqual(sdkEntityTriggerArea.BoxCollider.size, targetAreaSize);

            var component = new SDKEntityTriggerAreaComponent(areaSize: targetAreaSize);

            world.Add(entity, component);

            system.Update(0);

            Assert.AreEqual(targetAreaSize, sdkEntityTriggerArea.BoxCollider.size);
        }

        [Test]
        public async Task UpdateTriggerAreaSizeCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);

            var targetAreaSize = new Vector3(15, 6, 18);

            var component = new SDKEntityTriggerAreaComponent(areaSize: targetAreaSize);

            world.Add(entity, component);

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
            await UniTask.WaitUntil(() => isSetUp);

            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one);

            world.Add(entity, component);

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
        [TestCase(1f, 1f, 1f)]
        [TestCase(512f, 400f, 512f)]
        public async Task KeepExplicitAreaSizeWhenReusingAreaReleasedFromScaledEntity(float reusingScaleX, float reusingScaleY, float reusingScaleZ)
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);

            // A camera-mode-like area whose Transform is scaled to its own area size, later removed by the scene
            entityTransformComponent.SetTransform(new Vector3(64, 20, 64), Quaternion.identity, new Vector3(128, 40, 128));
            world.Set(entity, entityTransformComponent);

            var scaledEntityComponent = new SDKEntityTriggerAreaComponent(areaSize: new Vector3(128, 40, 128), targetOnlyMainPlayer: true);
            world.Add(entity, scaledEntityComponent);

            system.Update(0);

            scaledEntityComponent = world.Get<SDKEntityTriggerAreaComponent>(entity);
            scaledEntityComponent.TryRelease(sdkEntityTriggerAreaPool);
            world.Remove<SDKEntityTriggerAreaComponent>(entity);

            // An avatar-modifier-like area that gets the released instance from the pool
            Entity reusingEntity = world.Create(PartitionComponent.TOP_PRIORITY);
            TransformComponent reusingTransformComponent = AddTransformToEntity(reusingEntity);
            reusingTransformComponent.SetTransform(new Vector3(256, 150, 256), Quaternion.identity, new Vector3(reusingScaleX, reusingScaleY, reusingScaleZ));
            world.Set(reusingEntity, reusingTransformComponent);

            var targetAreaSize = new Vector3(512, 400, 512);
            world.Add(reusingEntity, new SDKEntityTriggerAreaComponent(areaSize: targetAreaSize));

            system.Update(0);

            Vector3 worldAreaSize = Vector3.Scale(sdkEntityTriggerArea.BoxCollider.size, sdkEntityTriggerArea.transform.lossyScale);
            Transform areaParent = sdkEntityTriggerArea.transform.parent;
            Object.DestroyImmediate(reusingTransformComponent.Transform.gameObject);

            Assert.AreEqual(reusingTransformComponent.Transform, areaParent);
            Assert.AreEqual(0f, Vector3.Distance(targetAreaSize, worldAreaSize), 1e-2f, $"World area size was {worldAreaSize}");
        }

        [Test]
        public async Task KeepExplicitAreaSizeWhenEntityScaleChangesAfterAssignment()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);

            var targetAreaSize = new Vector3(4, 4, 4);
            world.Add(entity, new SDKEntityTriggerAreaComponent(areaSize: targetAreaSize));

            system.Update(0);

            entityTransformComponent.SetTransform(Vector3.one * 30, Quaternion.identity, new Vector3(3, 5, 2));
            world.Set(entity, entityTransformComponent);

            system.Update(0);

            Vector3 worldAreaSize = WorldAreaSize();
            Assert.AreEqual(0f, Vector3.Distance(targetAreaSize, worldAreaSize), 1e-3f, $"World area size was {worldAreaSize}");
        }

        [Test]
        public async Task KeepExplicitAreaSizeWhenParentScaleChangesAfterAssignment()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);

            var parent = new GameObject();
            entityTransformComponent.Transform.SetParent(parent.transform);

            var targetAreaSize = new Vector3(4, 4, 4);
            world.Add(entity, new SDKEntityTriggerAreaComponent(areaSize: targetAreaSize));

            system.Update(0);

            parent.transform.localScale = new Vector3(3, 5, 2);

            system.Update(0);

            Vector3 worldAreaSize = WorldAreaSize();
            entityTransformComponent.Transform.SetParent(null);
            Object.DestroyImmediate(parent);

            Assert.AreEqual(0f, Vector3.Distance(targetAreaSize, worldAreaSize), 1e-3f, $"World area size was {worldAreaSize}");
        }

        [Test]
        public async Task FollowEntityScaleOnlyWhileAreaSizeIsZero()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);

            var entityScale = new Vector3(3, 5, 2);
            entityTransformComponent.SetTransform(Vector3.one * 30, Quaternion.identity, entityScale);
            world.Set(entity, entityTransformComponent);

            var explicitAreaSize = new Vector3(4, 4, 4);
            world.Add(entity, new SDKEntityTriggerAreaComponent(areaSize: explicitAreaSize));

            system.Update(0);

            Vector3 explicitWorldAreaSize = WorldAreaSize();

            SDKEntityTriggerAreaComponent component = world.Get<SDKEntityTriggerAreaComponent>(entity);
            component.UpdateAreaSize(Vector3.zero);
            world.Set(entity, component);

            system.Update(0);

            Vector3 scaleDrivenWorldAreaSize = WorldAreaSize();

            component = world.Get<SDKEntityTriggerAreaComponent>(entity);
            component.UpdateAreaSize(explicitAreaSize);
            world.Set(entity, component);

            system.Update(0);

            Vector3 restoredWorldAreaSize = WorldAreaSize();

            Assert.AreEqual(0f, Vector3.Distance(explicitAreaSize, explicitWorldAreaSize), 1e-3f, $"Explicit world area size was {explicitWorldAreaSize}");
            Assert.AreEqual(0f, Vector3.Distance(entityScale, scaleDrivenWorldAreaSize), 1e-3f, $"Scale-driven world area size was {scaleDrivenWorldAreaSize}");
            Assert.AreEqual(0f, Vector3.Distance(explicitAreaSize, restoredWorldAreaSize), 1e-3f, $"Restored world area size was {restoredWorldAreaSize}");
        }

        [Test]
        public async Task TrackEnterExitCollectionsCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);

            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4);

            world.Add(entity, component);

            system.Update(0);
            component = world.Get<SDKEntityTriggerAreaComponent>(entity);

            Assert.AreEqual(0, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedEntitiesToBeProcessed.Count);

            // Move character inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;

            await WaitForPhysics();

            Assert.AreEqual(1, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedEntitiesToBeProcessed.Count);
            component.TryClear();

            // Move character outside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position + (Vector3.one * 50);

            await WaitForPhysics();

            Assert.AreEqual(0, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(1, component.ExitedEntitiesToBeProcessed.Count);
        }

        [Test]
        public async Task HandlePlayerLeaveSceneCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);

            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4);

            world.Add(entity, component);

            system.Update(0);
            component = world.Get<SDKEntityTriggerAreaComponent>(entity);

            Assert.AreEqual(0, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedEntitiesToBeProcessed.Count);

            // Move character inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;

            await WaitForPhysics();

            Assert.AreEqual(1, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedEntitiesToBeProcessed.Count);
            component.TryClear();

            // Simulate "getting outside current scene"
            sceneStateProvider.IsCurrent.Returns(false);
            system.OnSceneIsCurrentChanged(false);

            system.Update(0);

            Assert.AreEqual(0, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(1, component.ExitedEntitiesToBeProcessed.Count);
            Assert.IsFalse(sdkEntityTriggerArea.BoxCollider.enabled);

            // Simulate "returning to the scene"
            sceneStateProvider.IsCurrent.Returns(true);
            system.OnSceneIsCurrentChanged(true);

            system.Update(0);

            Assert.IsTrue(sdkEntityTriggerArea.BoxCollider.enabled);
        }

        [Test]
        public async Task DetectCharacterWhenAlreadyInside()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);

            // Move character inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;

            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4);

            world.Add(entity, component);

            Assert.AreEqual(0, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedEntitiesToBeProcessed.Count);

            system.Update(0);
            await WaitForPhysics();
            component = world.Get<SDKEntityTriggerAreaComponent>(entity);

            Assert.AreEqual(1, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedEntitiesToBeProcessed.Count);
        }

        [Test]
        public async Task WaitUntilAvatarBaseIsConfiguredBeforeDetecting()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);

            // Move character inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;

            // Use fresh non-initialized MainPlayerAvatarBaseProxy
            mainPlayerAvatarBaseProxy = new ObjectProxy<AvatarBase>();
            system = new SDKEntityTriggerAreaHandlerSystem(world, sdkEntityTriggerAreaPool, mainPlayerAvatarBaseProxy, sceneStateProvider, characterObject);

            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4);

            world.Add(entity, component);

            Assert.AreEqual(0, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedEntitiesToBeProcessed.Count);

            system.Update(0);
            await WaitForPhysics();
            component = world.Get<SDKEntityTriggerAreaComponent>(entity);

            Assert.AreEqual(0, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedEntitiesToBeProcessed.Count);

            mainPlayerAvatarBaseProxy.SetObject(fakeMainPlayerAvatarGO.GetComponent<AvatarBase>());

            system.Update(0);
            await WaitForPhysics();
            component = world.Get<SDKEntityTriggerAreaComponent>(entity);

            Assert.AreEqual(1, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedEntitiesToBeProcessed.Count);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task DiscriminateCharacterTypeCorrectly(bool onlyMainPlayer)
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);

            GameObject? fakeOtherPlayerGO = await Addressables.LoadAssetAsync<GameObject>("Character Object");
            fakeOtherPlayerGO = Object.Instantiate(fakeOtherPlayerGO.GetComponent<CharacterObject>()).gameObject;
            fakeOtherPlayerGO.transform.position = Vector3.zero;

            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4, targetOnlyMainPlayer: onlyMainPlayer);

            world.Add(entity, component);

            system.Update(0);
            component = world.Get<SDKEntityTriggerAreaComponent>(entity);

            Assert.AreEqual(0, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedEntitiesToBeProcessed.Count);

            // Move both characters inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;
            fakeOtherPlayerGO.transform.position = entityTransformComponent.Transform.position;

            await WaitForPhysics();

            Assert.AreEqual(onlyMainPlayer ? 1 : 2, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedEntitiesToBeProcessed.Count);
            component.TryClear();

            // Move both characters outside area
            fakeMainPlayerGO.transform.position += Vector3.one * 30;
            fakeOtherPlayerGO.transform.position += Vector3.one * 30;

            await WaitForPhysics();

            Assert.AreEqual(0, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(onlyMainPlayer ? 1 : 2, component.ExitedEntitiesToBeProcessed.Count);

            // Cleanup
            Object.DestroyImmediate(fakeOtherPlayerGO);
        }

        [Test]
        [TestCase(SDKEntityTriggerAreaMeshType.Box)]
        [TestCase(SDKEntityTriggerAreaMeshType.Sphere)]
        public async Task SetupColliderTypeCorrectly(SDKEntityTriggerAreaMeshType meshType)
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);
            var targetAreaSize = new Vector3(15, 6, 18);

            var component = new SDKEntityTriggerAreaComponent(areaSize: targetAreaSize, meshType: meshType);

            world.Add(entity, component);

            system.Update(0);

            if (meshType == SDKEntityTriggerAreaMeshType.Box)
            {
                Assert.IsTrue(sdkEntityTriggerArea.BoxCollider.enabled);
                Assert.IsFalse(sdkEntityTriggerArea.SphereCollider.enabled);
                Assert.AreEqual(targetAreaSize, sdkEntityTriggerArea.BoxCollider.size);
            }
            else
            {
                Assert.IsFalse(sdkEntityTriggerArea.BoxCollider.enabled);
                Assert.IsTrue(sdkEntityTriggerArea.SphereCollider.enabled);
                var expectedRadius = targetAreaSize.magnitude / 2f;
                Assert.AreEqual(expectedRadius, sdkEntityTriggerArea.SphereCollider.radius, 1e-4f);
            }
        }

        [Test]
        public async Task UsePlayerLayerByDefault()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);
            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4);
            Assert.AreEqual(ColliderLayer.ClPlayer, component.LayerMask);
        }

        [Test]
        public async Task UseSDKAvatarTriggerAreaLayerWhenOnlyTargetingPlayers()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);
            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4, layerMask: ColliderLayer.ClPlayer);

            world.Add(entity, component);

            system.Update(0);

            Assert.AreEqual(PhysicsLayers.SDK_AVATAR_TRIGGER_AREA, sdkEntityTriggerArea.gameObject.layer);

            component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4, layerMask: (ColliderLayer)((uint)ColliderLayer.ClPlayer | (uint)ColliderLayer.ClCustom6));
            world.Set(entity, component);

            system.Update(0);

            Assert.AreEqual(PhysicsLayers.SDK_ENTITY_TRIGGER_AREA, sdkEntityTriggerArea.gameObject.layer);
        }

        [Test]
        public async Task DetectSDKEntitiesWithTargetSDKLayer()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => isSetUp);
            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4, layerMask: ColliderLayer.ClCustom1);

            world.Add(entity, component);

            system.Update(0);
            component = world.Get<SDKEntityTriggerAreaComponent>(entity);

            Assert.AreEqual(0, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedEntitiesToBeProcessed.Count);

            // Create an SDK entity collider on the SDK custom layer
            var sdkEntityGO = new GameObject("FakeSDKEntity");
            sdkEntityGO.layer = PhysicsLayers.SDK_CUSTOM_LAYER;
            sdkEntityGO.AddComponent<BoxCollider>();

            // Move SDK entity inside area
            sdkEntityGO.transform.position = entityTransformComponent.Transform.position;
            await WaitForPhysics();

            component = world.Get<SDKEntityTriggerAreaComponent>(entity);
            Assert.AreEqual(1, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(0, component.ExitedEntitiesToBeProcessed.Count);
            component.TryClear();

            // Move SDK entity outside area
            sdkEntityGO.transform.position = entityTransformComponent.Transform.position + (Vector3.one * 50);
            await WaitForPhysics();

            component = world.Get<SDKEntityTriggerAreaComponent>(entity);
            Assert.AreEqual(0, component.EnteredEntitiesToBeProcessed.Count);
            Assert.AreEqual(1, component.ExitedEntitiesToBeProcessed.Count);

            // Cleanup
            Object.DestroyImmediate(sdkEntityGO);
        }

        private Vector3 WorldAreaSize() =>
            Vector3.Scale(sdkEntityTriggerArea.BoxCollider.size, sdkEntityTriggerArea.transform.lossyScale);

        private static async Task WaitForPhysics()
        {
            // Wait several frames to allow CI detect physics on its non-deterministic hardware.
            var framesToWait = 10;

            for (var i = 0; i < framesToWait; i++)
            {
                Physics.Simulate(0.01f);
                await UniTask.Yield();
            }
        }
    }
}
