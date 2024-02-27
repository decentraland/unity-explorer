using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.ECSComponents;
using DCL.SDKComponents.AvatarAttach.Systems;
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

namespace DCL.SDKComponents.AvatarAttach.Tests
{
    public class AvatarAttachHandlerSystemShould : UnitySystemTestBase<AvatarAttachHandlerSystem>
    {
        private Entity entity;
        private TransformComponent entityTransformComponent;
        private World globalWorld;
        private AvatarBase playerAvatarBase;
        private ISceneStateProvider sceneStateProvider;

        [SetUp]
        public async void Setup()
        {
            // Create player entity in global world
            GameObject avatarBaseGameObject = await Addressables.LoadAssetAsync<GameObject>("AvatarBase_TestAsset");
            playerAvatarBase = Object.Instantiate(avatarBaseGameObject.GetComponent<AvatarBase>());
            playerAvatarBase.gameObject.transform.position = new Vector3(8, 8, 8);
            globalWorld = World.Create();

            Entity playerEntity = globalWorld.Create(
                new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY),
                new PlayerComponent(Substitute.For<Transform>()),
                playerAvatarBase
            );

            // Setup system
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            var mainPlayerAvatarBase = new ObjectProxy<AvatarBase>();
            mainPlayerAvatarBase.SetObject(playerAvatarBase);
            system = new AvatarAttachHandlerSystem(world, mainPlayerAvatarBase, sceneStateProvider);

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            entityTransformComponent = AddTransformToEntity(entity);
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(playerAvatarBase.gameObject);
            Object.DestroyImmediate(entityTransformComponent.Transform.gameObject);
        }

        [Test]
        public async Task SetupAndUpdateAvatarPositionAnchorPointCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptPosition };
            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);

            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            playerAvatarBase.transform.position += Vector3.one * 5;
            playerAvatarBase.transform.rotation = Quaternion.Euler(30, 60, 90);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            playerAvatarBase.transform.position += Vector3.one * 6;
            playerAvatarBase.transform.rotation = Quaternion.Euler(50, 45, 66);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            playerAvatarBase.transform.position += Vector3.one * 10;
            playerAvatarBase.transform.rotation = Quaternion.Euler(99, 99, 99);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);
        }

        [Test]
        public async Task SetupAndUpdateAvatarLeftHandAnchorPointCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptLeftHand };
            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransformComponent.Transform.position);

            system.Update(0);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());

            playerAvatarBase.LeftHandAnchorPoint.position += Vector3.one * 5;
            playerAvatarBase.LeftHandAnchorPoint.rotation = Quaternion.Euler(30, 60, 90);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());

            playerAvatarBase.LeftHandAnchorPoint.position += Vector3.one * 6;
            playerAvatarBase.LeftHandAnchorPoint.rotation = Quaternion.Euler(50, 45, 66);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());

            playerAvatarBase.LeftHandAnchorPoint.position += Vector3.one * 10;
            playerAvatarBase.LeftHandAnchorPoint.rotation = Quaternion.Euler(99, 99, 99);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());
        }

        [Test]
        public async Task SetupAndUpdateAvatarRightHandAnchorPointCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptRightHand };
            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransformComponent.Transform.position);

            system.Update(0);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());

            playerAvatarBase.RightHandAnchorPoint.position += Vector3.one * 5;
            playerAvatarBase.RightHandAnchorPoint.rotation = Quaternion.Euler(30, 60, 90);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());

            playerAvatarBase.RightHandAnchorPoint.position += Vector3.one * 6;
            playerAvatarBase.RightHandAnchorPoint.rotation = Quaternion.Euler(50, 45, 66);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());

            playerAvatarBase.RightHandAnchorPoint.position += Vector3.one * 10;
            playerAvatarBase.RightHandAnchorPoint.rotation = Quaternion.Euler(99, 99, 99);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());
        }

        [Test]
        public async Task UpdateAnchorPointCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            // Attach to left hand
            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptLeftHand };
            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransformComponent.Transform.position);

            system.Update(0);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransformComponent.Transform.position);

            // Change attachment to right hand
            Assert.AreNotEqual(playerAvatarBase.LeftHandAnchorPoint.position, playerAvatarBase.RightHandAnchorPoint.position);

            pbAvatarAttachComponent.AnchorPointId = AvatarAnchorPointType.AaptRightHand;
            pbAvatarAttachComponent.IsDirty = true;
            world.Set(entity, pbAvatarAttachComponent);

            system.Update(0);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransformComponent.Transform.position);
        }

        [Test]
        public async Task OverrideTransformValuesExceptScale()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptPosition };
            world.Add(entity, pbAvatarAttachComponent);

            entityTransformComponent.SetTransform(Vector3.one * 5, Quaternion.Euler(90, 90, 90), Vector3.one * 3);
            world.Set(entity, entityTransformComponent);
            Assert.AreEqual(Vector3.one * 5, entityTransformComponent.Transform.position);

            playerAvatarBase.transform.position += Vector3.one * 5;
            playerAvatarBase.transform.rotation = Quaternion.Euler(30, 60, 90);
            Assert.AreNotEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);
            Assert.AreEqual(Vector3.one * 3, entityTransformComponent.Transform.localScale);

            entityTransformComponent.SetTransform(Vector3.one * 10, Quaternion.Euler(22, 6, 99), Vector3.one * 1.77f);
            world.Set(entity, entityTransformComponent);
            playerAvatarBase.transform.position += Vector3.one * 7;
            playerAvatarBase.transform.rotation = Quaternion.Euler(0, 50, 66);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);
            Assert.AreEqual(Vector3.one * 1.77f, entityTransformComponent.Transform.localScale);

            entityTransformComponent.SetTransform(Vector3.one * 10, Quaternion.Euler(22, 6, 99), Vector3.one * 5);
            world.Set(entity, entityTransformComponent);
            playerAvatarBase.transform.position += Vector3.one * 60;
            playerAvatarBase.transform.rotation = Quaternion.Euler(15, 37, 55);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);
            Assert.AreEqual(Vector3.one * 5, entityTransformComponent.Transform.localScale);
        }

        [Test]
        public async Task StopUpdatingTransformOnComponentRemoval()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptPosition };
            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);

            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            playerAvatarBase.transform.position += Vector3.one * 5;
            playerAvatarBase.transform.rotation = Quaternion.Euler(30, 60, 90);
            world.Remove<PBAvatarAttach>(entity);
            system.Update(0);
            Assert.AreNotEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);
        }

        [Test]
        public async Task StopUpdatingTransformOnEntityDeletion()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptPosition };
            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);

            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            world.Add<DeleteEntityIntention>(entity);
            system.Update(0);

            playerAvatarBase.transform.position += Vector3.one * 5;
            playerAvatarBase.transform.rotation = Quaternion.Euler(30, 60, 90);
            system.Update(0);
            Assert.AreNotEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);
        }

        [Test]
        public async Task UpdateTransformOnlyWhenPlayerIsInCurrentScene()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptPosition };
            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);

            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            // Simulate leaving the scene
            sceneStateProvider.IsCurrent.Returns(false);
            playerAvatarBase.transform.position += Vector3.one * 5;
            playerAvatarBase.transform.rotation = Quaternion.Euler(30, 60, 90);
            system.Update(0);
            Assert.AreNotEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            playerAvatarBase.transform.position += Vector3.one * 5;
            system.Update(0);
            Assert.AreNotEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            // Simulate re-entering the scene
            sceneStateProvider.IsCurrent.Returns(true);
            playerAvatarBase.transform.position += Vector3.one * 5;
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);
        }
    }
}
