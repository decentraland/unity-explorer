using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.CharacterTriggerArea.Components;
using DCL.CharacterTriggerArea.Systems;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
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

namespace DCL.CharacterTriggerArea.Tests
{
    public class CharacterTriggerAreaHandlerSystemShould : UnitySystemTestBase<CharacterTriggerAreaHandlerSystem>
    {
        private Entity entity;
        private TransformComponent entityTransformComponent;
        private ISceneStateProvider sceneStateProvider;
        private GameObject fakeMainPlayerGO;
        private GameObject fakeMainPlayerAvatarGO;
        private CharacterTriggerArea characterTriggerArea;
        private MainPlayerReferences mainPlayerReferences;

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

            GameObject characterTriggerAreaBaseGO = await Addressables.LoadAssetAsync<GameObject>("CharacterTriggerArea");
            characterTriggerArea = Object.Instantiate(characterTriggerAreaBaseGO.GetComponent<CharacterTriggerArea>());

            fakeMainPlayerAvatarGO = new GameObject();

            mainPlayerReferences = new MainPlayerReferences
            {
                MainPlayerAvatarBase = new MainPlayerAvatarBase(),
                MainPlayerTransform = new MainPlayerTransform(),
            };

            mainPlayerReferences.MainPlayerAvatarBase.SetAvatarBase(fakeMainPlayerAvatarGO.AddComponent<AvatarBase>());
            mainPlayerReferences.MainPlayerTransform.SetTransform(fakeMainPlayerGO.transform);

            // Setup system
            IComponentPoolsRegistry poolsRegistry = Substitute.For<IComponentPoolsRegistry>();
            IComponentPool<CharacterTriggerArea> characterTriggerAreaPool = Substitute.For<IComponentPool<CharacterTriggerArea>>();
            poolsRegistry.GetReferenceTypePool<CharacterTriggerArea>().Returns(characterTriggerAreaPool);
            characterTriggerAreaPool.Get().Returns(characterTriggerArea);

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            system = new CharacterTriggerAreaHandlerSystem(world, characterTriggerAreaPool, mainPlayerReferences, sceneStateProvider);
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(entityTransformComponent.Transform.gameObject);
            Object.DestroyImmediate(fakeMainPlayerGO);
            Object.DestroyImmediate(fakeMainPlayerAvatarGO);
            Object.DestroyImmediate(characterTriggerArea.gameObject);
        }

        [Test]
        public async Task SetupTriggerAreaSizeCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var targetAreaSize = new Vector3(15, 6, 18);

            Assert.AreNotEqual(characterTriggerArea.BoxCollider.size, targetAreaSize);

            var pbComponent = new PBCameraModeArea();

            var component = new CharacterTriggerAreaComponent
            {
                AreaSize = targetAreaSize,
                IsDirty = true,
            };

            world.Add(entity, component, pbComponent);

            system.Update(0);

            Assert.AreEqual(targetAreaSize, characterTriggerArea.BoxCollider.size);
        }

        [Test]
        public async Task UpdateTriggerAreaSizeCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var targetAreaSize = new Vector3(15, 6, 18);

            var pbComponent = new PBCameraModeArea();

            var component = new CharacterTriggerAreaComponent
            {
                AreaSize = targetAreaSize,
                IsDirty = true,
            };

            world.Add(entity, component, pbComponent);

            system.Update(0);

            Assert.AreEqual(targetAreaSize, characterTriggerArea.BoxCollider.size);

            // update component values
            targetAreaSize /= 3;
            component.AreaSize = targetAreaSize;
            component.IsDirty = true;
            world.Set(entity, component);

            Assert.AreNotEqual(targetAreaSize, characterTriggerArea.BoxCollider.size);

            system.Update(0);

            Assert.AreEqual(targetAreaSize, characterTriggerArea.BoxCollider.size);
        }

        [Test]
        public async Task UpdateTransformCorrectlyIgnoringScale()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbComponent = new PBCameraModeArea();

            var component = new CharacterTriggerAreaComponent
            {
                AreaSize = Vector3.one,
                IsDirty = true,
            };

            world.Add(entity, component, pbComponent);

            system.Update(0);

            Assert.AreEqual(entityTransformComponent.Transform.position, characterTriggerArea.transform.position);

            entityTransformComponent.SetTransform(Vector3.one * 10, Quaternion.identity, Vector3.one);
            world.Set(entity, entityTransformComponent);

            system.Update(0);
            Assert.AreEqual(entityTransformComponent.Transform.position, characterTriggerArea.transform.position);

            entityTransformComponent.SetTransform(Vector3.one * 38, Quaternion.Euler(33, 65, 59), Vector3.one * 66);
            world.Set(entity, entityTransformComponent);

            system.Update(0);

            Assert.AreEqual(entityTransformComponent.Transform.position, characterTriggerArea.transform.position);
            Assert.AreEqual(entityTransformComponent.Transform.rotation, characterTriggerArea.transform.rotation);
            Assert.AreNotEqual(entityTransformComponent.Transform.localScale, characterTriggerArea.BoxCollider.size);
            Assert.AreEqual(Vector3.one, characterTriggerArea.BoxCollider.size);
        }

        [Test]
        public async Task TriggerEnterExitEventsCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var enterTriggerCalled = false;
            var exitTriggerCalled = false;

            var pbComponent = new PBCameraModeArea();

            var component = new CharacterTriggerAreaComponent
            {
                AreaSize = Vector3.one * 4,
                OnEnteredTrigger = collider => enterTriggerCalled = true,
                OnExitedTrigger = collider => exitTriggerCalled = true,
                IsDirty = true,
            };

            world.Add(entity, component, pbComponent);

            system.Update(0);

            Assert.IsFalse(enterTriggerCalled);
            Assert.IsFalse(exitTriggerCalled);

            // Move character inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;

            await UniTask.Yield();

            Assert.IsTrue(enterTriggerCalled);
            Assert.IsFalse(exitTriggerCalled);
            enterTriggerCalled = false;

            // Move character outside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position + (Vector3.one * 50);

            await UniTask.Yield();

            Assert.IsTrue(exitTriggerCalled);
            Assert.IsFalse(enterTriggerCalled);
        }

        [Test]
        public async Task HandlePlayerLeaveSceneCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var enterTriggerCalled = false;
            var exitTriggerCalled = false;

            var pbComponent = new PBCameraModeArea();

            var component = new CharacterTriggerAreaComponent
            {
                AreaSize = Vector3.one * 4,
                OnEnteredTrigger = collider => enterTriggerCalled = true,
                OnExitedTrigger = collider => exitTriggerCalled = true,
                IsDirty = true,
            };

            world.Add(entity, component, pbComponent);

            system.Update(0);

            Assert.IsFalse(enterTriggerCalled);
            Assert.IsFalse(exitTriggerCalled);

            // Move character inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;

            await UniTask.Yield();

            Assert.IsTrue(enterTriggerCalled);
            Assert.IsFalse(exitTriggerCalled);
            enterTriggerCalled = false;

            // Simulate "getting outside current scene"
            sceneStateProvider.IsCurrent.Returns(false);

            system.Update(0);

            Assert.IsTrue(exitTriggerCalled);
            Assert.IsFalse(enterTriggerCalled);
            Assert.IsFalse(characterTriggerArea.BoxCollider.enabled);
        }

        [Test]
        public async Task HandleComponentRemoveCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var enterTriggerCalled = false;
            var exitTriggerCalled = false;

            var pbComponent = new PBCameraModeArea();

            var component = new CharacterTriggerAreaComponent
            {
                AreaSize = Vector3.one * 4,
                OnEnteredTrigger = collider => enterTriggerCalled = true,
                OnExitedTrigger = collider => exitTriggerCalled = true,
                IsDirty = true,
            };

            world.Add(entity, component, pbComponent);

            system.Update(0);

            Assert.IsFalse(enterTriggerCalled);
            Assert.IsFalse(exitTriggerCalled);

            // Move character inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;

            await UniTask.Yield();

            Assert.IsTrue(enterTriggerCalled);
            Assert.IsFalse(exitTriggerCalled);
            enterTriggerCalled = false;

            // Remove component
            world.Remove<PBCameraModeArea>(entity);

            system.Update(0);

            Assert.IsTrue(exitTriggerCalled);
            Assert.IsFalse(enterTriggerCalled);
            Assert.IsFalse(characterTriggerArea.BoxCollider.enabled);
            Assert.IsFalse(world.Has<CharacterTriggerAreaComponent>(entity));
        }

        [Test]
        public async Task HandleEntityDestructionCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var enterTriggerCalled = false;
            var exitTriggerCalled = false;

            var pbComponent = new PBCameraModeArea();

            var component = new CharacterTriggerAreaComponent
            {
                AreaSize = Vector3.one * 4,
                OnEnteredTrigger = collider => enterTriggerCalled = true,
                OnExitedTrigger = collider => exitTriggerCalled = true,
                IsDirty = true,
            };

            world.Add(entity, component, pbComponent);

            system.Update(0);

            Assert.IsFalse(enterTriggerCalled);
            Assert.IsFalse(exitTriggerCalled);

            // Move character inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;

            await UniTask.Yield();

            Assert.IsTrue(enterTriggerCalled);
            Assert.IsFalse(exitTriggerCalled);
            enterTriggerCalled = false;

            // Flag entity for deletion
            world.Add<DeleteEntityIntention>(entity);

            system.Update(0);

            Assert.IsTrue(exitTriggerCalled);
            Assert.IsFalse(enterTriggerCalled);
            Assert.IsFalse(characterTriggerArea.BoxCollider.enabled);
            Assert.IsFalse(world.Has<CharacterTriggerAreaComponent>(entity));
        }

        [Test]
        public async Task TriggerOnEnterIfPlayerAlreadyInside()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            // Move character inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;

            var enterTriggerCalled = false;
            var exitTriggerCalled = false;

            var pbComponent = new PBCameraModeArea();

            var component = new CharacterTriggerAreaComponent
            {
                AreaSize = Vector3.one * 4,
                OnEnteredTrigger = collider => enterTriggerCalled = true,
                OnExitedTrigger = collider => exitTriggerCalled = true,
                IsDirty = true,
            };

            world.Add(entity, component, pbComponent);

            Assert.IsFalse(exitTriggerCalled);
            Assert.IsFalse(enterTriggerCalled);

            system.Update(0);
            await UniTask.Yield();

            Assert.IsTrue(enterTriggerCalled);
            Assert.IsFalse(exitTriggerCalled);
        }

        [Test]
        public async Task WaitUntilAvatarBaseIsConfiguredBeforeActing()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            // Move character inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;

            // Use fresh non-initialized MainPlayerAvatarBase
            mainPlayerReferences.MainPlayerAvatarBase = new MainPlayerAvatarBase();

            var enterTriggerCalled = false;
            var exitTriggerCalled = false;

            var pbComponent = new PBCameraModeArea();

            var component = new CharacterTriggerAreaComponent
            {
                AreaSize = Vector3.one * 4,
                OnEnteredTrigger = collider => enterTriggerCalled = true,
                OnExitedTrigger = collider => exitTriggerCalled = true,
                IsDirty = true,
            };

            world.Add(entity, component, pbComponent);

            Assert.IsFalse(exitTriggerCalled);
            Assert.IsFalse(enterTriggerCalled);

            system.Update(0);
            await UniTask.Yield();

            Assert.IsFalse(exitTriggerCalled);
            Assert.IsFalse(enterTriggerCalled);

            mainPlayerReferences.MainPlayerAvatarBase.SetAvatarBase(fakeMainPlayerAvatarGO.GetComponent<AvatarBase>());

            system.Update(0);
            await UniTask.Yield();

            Assert.IsTrue(enterTriggerCalled);
            Assert.IsFalse(exitTriggerCalled);
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

            var enterTriggerCalls = 0;
            var exitTriggerCalls = 0;

            var pbComponent = new PBCameraModeArea();

            var component = new CharacterTriggerAreaComponent
            {
                AreaSize = Vector3.one * 4,
                OnEnteredTrigger = collider => enterTriggerCalls++,
                OnExitedTrigger = collider => exitTriggerCalls++,
                TargetOnlyMainPlayer = onlyMainPlayer,
                IsDirty = true,
            };

            world.Add(entity, component, pbComponent);

            system.Update(0);

            Assert.AreEqual(0, enterTriggerCalls);
            Assert.AreEqual(0, exitTriggerCalls);

            // Move both characters inside area
            fakeMainPlayerGO.transform.position = entityTransformComponent.Transform.position;
            fakeOtherPlayerGO.transform.position = entityTransformComponent.Transform.position;

            await UniTask.Yield();

            Assert.AreEqual(0, exitTriggerCalls);
            Assert.AreEqual(onlyMainPlayer ? 1 : 2, enterTriggerCalls);

            // Move both characters outside area
            fakeMainPlayerGO.transform.position += Vector3.one * 30;
            fakeOtherPlayerGO.transform.position += Vector3.one * 30;

            await UniTask.Yield();

            Assert.AreEqual(onlyMainPlayer ? 1 : 2, enterTriggerCalls);
            Assert.AreEqual(onlyMainPlayer ? 1 : 2, exitTriggerCalls);

            // Cleanup
            Object.DestroyImmediate(fakeOtherPlayerGO);
        }
    }
}
