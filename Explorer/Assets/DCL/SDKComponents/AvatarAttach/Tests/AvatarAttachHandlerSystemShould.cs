using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.ECSComponents;
using DCL.SDKComponents.AvatarAttach.Systems;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.SDKComponents.AvatarAttach.Tests
{
    public class AvatarAttachHandlerSystemShould : UnitySystemTestBase<AvatarAttachHandlerSystem>
    {
        private Entity entity;
        private Transform entityTransform;
        private World globalWorld;
        private AvatarBase playerAvatarBase;

        [SetUp]
        public async void Setup()
        {
            // Create player entity in global world
            GameObject avatarBaseGameObject = await Addressables.LoadAssetAsync<GameObject>("AvatarBase_TestAsset");
            playerAvatarBase = Object.Instantiate(avatarBaseGameObject.GetComponent<AvatarBase>());
            playerAvatarBase.gameObject.transform.position = new Vector3(8, 8, 8);
            globalWorld = World.Create();

            globalWorld.Create(
                new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY),
                new PlayerComponent(Substitute.For<Transform>()),
                playerAvatarBase
            );

            // Setup system
            var worldProxy = new WorldProxy();
            worldProxy.SetWorld(globalWorld);
            system = new AvatarAttachHandlerSystem(world, worldProxy);

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            entityTransform = AddTransformToEntity(entity).Transform;
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(playerAvatarBase.gameObject);
        }

        [Test]
        public async Task SetupAndUpdateAvatarPositionAnchorPointCorrectly()
        {
            // Workaround for Unity's bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptPosition };

            // world.TryGet(entity, out TransformComponent entityTransform);
            Assert.IsNotNull(entityTransform);
            Assert.IsNotNull(playerAvatarBase);

            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransform.position);
            Assert.AreNotEqual(playerAvatarBase.transform.position, entityTransform.position);

            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransform.position);

            playerAvatarBase.transform.position += Vector3.one * 5;
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransform.position);

            playerAvatarBase.transform.position += Vector3.one * 6;
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransform.position);

            playerAvatarBase.transform.position += Vector3.one * 10;
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.transform.position, entityTransform.position);
        }

        [Test]
        public async Task SetupAndUpdateAvatarLeftHandAnchorPointCorrectly()
        {
            // Workaround for Unity's bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptLeftHand };

            // world.TryGet(entity, out TransformComponent entityTransform);
            Assert.IsNotNull(entityTransform);
            Assert.IsNotNull(playerAvatarBase);

            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransform.position);
            Assert.AreNotEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransform.position);

            system.Update(0);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransform.position);

            playerAvatarBase.LeftHandAnchorPoint.position += Vector3.one * 5;
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransform.position);

            playerAvatarBase.LeftHandAnchorPoint.position += Vector3.one * 6;
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransform.position);

            playerAvatarBase.LeftHandAnchorPoint.position += Vector3.one * 10;
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransform.position);
        }

        [Test]
        public async Task SetupAndUpdateAvatarRightHandAnchorPointCorrectly()
        {
            // Workaround for Unity's bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptRightHand };

            // world.TryGet(entity, out TransformComponent entityTransform);
            Assert.IsNotNull(entityTransform);
            Assert.IsNotNull(playerAvatarBase);

            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransform.position);
            Assert.AreNotEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransform.position);

            system.Update(0);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransform.position);

            playerAvatarBase.RightHandAnchorPoint.position += Vector3.one * 5;
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransform.position);

            playerAvatarBase.RightHandAnchorPoint.position += Vector3.one * 6;
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransform.position);

            playerAvatarBase.RightHandAnchorPoint.position += Vector3.one * 10;
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransform.position);
        }

        [Test]
        public async Task UpdateAnchorPointCorrectly() { }

        [Test]
        public async Task OverrideTransformValuesExceptScale() { }
    }
}
