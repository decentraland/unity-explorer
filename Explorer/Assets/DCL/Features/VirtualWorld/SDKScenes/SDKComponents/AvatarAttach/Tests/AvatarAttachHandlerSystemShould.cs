using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Typing;
using DCL.Multiplayer.Profiles.Tables;
using DCL.SDKComponents.AvatarAttach.Components;
using DCL.SDKComponents.AvatarAttach.Systems;
using DCL.SDKComponents.Utils;
using DCL.Utilities;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DCL.SDKComponents.AvatarAttach.Tests
{
    public class AvatarAttachHandlerSystemShould : UnitySystemTestBase<AvatarAttachHandlerSystem>
    {
        private Entity entity;
        private TransformComponent entityTransformComponent;
        private World globalWorld;
        private AvatarBase playerAvatarBase;
        private ISceneStateProvider sceneStateProvider;
        private AvatarAttachHandlerSetupSystem setupSystem;
        private IReadOnlyEntityParticipantTable entityParticipantTable;
        private ObjectProxy<IReadOnlyEntityParticipantTable> entityParticipantTableProxy;

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
                playerAvatarBase,
                new AvatarShapeComponent { ID = "" }
            );

            // Setup system
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            var mainPlayerAvatarBase = new ObjectProxy<AvatarBase>();
            mainPlayerAvatarBase.SetObject(playerAvatarBase);

            // Create a mock EntityParticipantTable
            entityParticipantTable = Substitute.For<IReadOnlyEntityParticipantTable>();
            entityParticipantTableProxy = new ObjectProxy<IReadOnlyEntityParticipantTable>();
            entityParticipantTableProxy.SetObject(entityParticipantTable);

            system = new AvatarAttachHandlerSystem(world,
                globalWorld,
                mainPlayerAvatarBase,
                sceneStateProvider,
                entityParticipantTableProxy);

            setupSystem = new AvatarAttachHandlerSetupSystem(world,
                globalWorld,
                mainPlayerAvatarBase,
                sceneStateProvider,
                entityParticipantTableProxy);

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            entityTransformComponent = AddTransformToEntity(entity);
        }

        protected override void OnTearDown()
        {
            Object.DestroyImmediate(playerAvatarBase.gameObject);
            Object.DestroyImmediate(entityTransformComponent.Transform.gameObject);
            setupSystem?.Dispose();
            globalWorld.Dispose();
            base.OnTearDown();
        }

        [Test]
        public async Task SetupAndUpdateAvatarPositionAnchorPointCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptPosition, AvatarId = "", IsDirty = true };
            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.transform.position, entityTransformComponent.Transform.position);

            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            playerAvatarBase.transform.position += Vector3.one * 5;
            playerAvatarBase.transform.rotation = Quaternion.Euler(30, 60, 90);
            system.Update(0);
            Assert.AreEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            playerAvatarBase.transform.position += Vector3.one * 6;
            playerAvatarBase.transform.rotation = Quaternion.Euler(50, 45, 66);
            system.Update(0);
            Assert.AreEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            playerAvatarBase.transform.position += Vector3.one * 10;
            playerAvatarBase.transform.rotation = Quaternion.Euler(99, 99, 99);
            system.Update(0);
            Assert.AreEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);
        }

        [Test]
        public async Task SetupAndUpdateAvatarLeftHandAnchorPointCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptLeftHand, AvatarId = "", IsDirty = true };
            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransformComponent.Transform.position);

            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());

            playerAvatarBase.LeftHandAnchorPoint.position += Vector3.one * 5;
            playerAvatarBase.LeftHandAnchorPoint.rotation = Quaternion.Euler(30, 60, 90);
            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());

            playerAvatarBase.LeftHandAnchorPoint.position += Vector3.one * 6;
            playerAvatarBase.LeftHandAnchorPoint.rotation = Quaternion.Euler(50, 45, 66);
            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());

            playerAvatarBase.LeftHandAnchorPoint.position += Vector3.one * 10;
            playerAvatarBase.LeftHandAnchorPoint.rotation = Quaternion.Euler(99, 99, 99);
            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());
        }

        [Test]
        public async Task SetupAndUpdateAvatarRightHandAnchorPointCorrectly()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptRightHand, AvatarId = "", IsDirty = true };
            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransformComponent.Transform.position);

            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());

            playerAvatarBase.RightHandAnchorPoint.position += Vector3.one * 5;
            playerAvatarBase.RightHandAnchorPoint.rotation = Quaternion.Euler(30, 60, 90);
            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());

            playerAvatarBase.RightHandAnchorPoint.position += Vector3.one * 6;
            playerAvatarBase.RightHandAnchorPoint.rotation = Quaternion.Euler(50, 45, 66);
            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.rotation.ToString(), entityTransformComponent.Transform.rotation.ToString());

            playerAvatarBase.RightHandAnchorPoint.position += Vector3.one * 10;
            playerAvatarBase.RightHandAnchorPoint.rotation = Quaternion.Euler(99, 99, 99);
            setupSystem.Update(0);
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
            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptLeftHand, AvatarId = "", IsDirty = true };
            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransformComponent.Transform.position);

            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.LeftHandAnchorPoint.position, entityTransformComponent.Transform.position);

            // Change attachment to right hand
            Assert.AreNotEqual(playerAvatarBase.LeftHandAnchorPoint.position, playerAvatarBase.RightHandAnchorPoint.position);

            pbAvatarAttachComponent.AnchorPointId = AvatarAnchorPointType.AaptRightHand;
            pbAvatarAttachComponent.IsDirty = true;
            world.Set(entity, pbAvatarAttachComponent);

            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(playerAvatarBase.RightHandAnchorPoint.position, entityTransformComponent.Transform.position);
        }

        [Test]
        public async Task VerifyAllAnchorPoints()
        {
            bool ApproximatelyEqual(Vector3 a, Vector3 b) =>
                Vector3.SqrMagnitude(a - b) < Mathf.Epsilon * Mathf.Epsilon;

            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AvatarId = "", IsDirty = true };
            world.Add(entity, pbAvatarAttachComponent);

            // Dictionary to map AvatarAnchorPointType to the corresponding Transform property
            var anchorPointMap = new Dictionary<AvatarAnchorPointType, Func<Transform>>
            {
                { AvatarAnchorPointType.AaptPosition, () => playerAvatarBase.transform },
                { AvatarAnchorPointType.AaptNameTag, () => playerAvatarBase.NameTagAnchorPoint },
                { AvatarAnchorPointType.AaptHead, () => playerAvatarBase.HeadAnchorPoint },
                { AvatarAnchorPointType.AaptNeck, () => playerAvatarBase.NeckAnchorPoint },
                { AvatarAnchorPointType.AaptSpine, () => playerAvatarBase.SpineAnchorPoint },
                { AvatarAnchorPointType.AaptSpine1, () => playerAvatarBase.Spine1AnchorPoint },
                { AvatarAnchorPointType.AaptSpine2, () => playerAvatarBase.Spine2AnchorPoint },
                { AvatarAnchorPointType.AaptHip, () => playerAvatarBase.HipAnchorPoint },
                { AvatarAnchorPointType.AaptLeftShoulder, () => playerAvatarBase.LeftShoulderAnchorPoint },
                { AvatarAnchorPointType.AaptLeftArm, () => playerAvatarBase.LeftArmAnchorPoint },
                { AvatarAnchorPointType.AaptLeftForearm, () => playerAvatarBase.LeftForearmAnchorPoint },
                { AvatarAnchorPointType.AaptLeftHand, () => playerAvatarBase.LeftHandAnchorPoint },
                { AvatarAnchorPointType.AaptLeftHandIndex, () => playerAvatarBase.LeftHandIndexAnchorPoint },
                { AvatarAnchorPointType.AaptRightShoulder, () => playerAvatarBase.RightShoulderAnchorPoint },
                { AvatarAnchorPointType.AaptRightArm, () => playerAvatarBase.RightArmAnchorPoint },
                { AvatarAnchorPointType.AaptRightForearm, () => playerAvatarBase.RightForearmAnchorPoint },
                { AvatarAnchorPointType.AaptRightHand, () => playerAvatarBase.RightHandAnchorPoint },
                { AvatarAnchorPointType.AaptRightHandIndex, () => playerAvatarBase.RightHandIndexAnchorPoint },
                { AvatarAnchorPointType.AaptLeftUpLeg, () => playerAvatarBase.LeftUpLegAnchorPoint },
                { AvatarAnchorPointType.AaptLeftLeg, () => playerAvatarBase.LeftLegAnchorPoint },
                { AvatarAnchorPointType.AaptLeftFoot, () => playerAvatarBase.LeftFootAnchorPoint },
                { AvatarAnchorPointType.AaptLeftToeBase, () => playerAvatarBase.LeftToeBaseAnchorPoint },
                { AvatarAnchorPointType.AaptRightUpLeg, () => playerAvatarBase.RightUpLegAnchorPoint },
                { AvatarAnchorPointType.AaptRightLeg, () => playerAvatarBase.RightLegAnchorPoint },
                { AvatarAnchorPointType.AaptRightFoot, () => playerAvatarBase.RightFootAnchorPoint },
                { AvatarAnchorPointType.AaptRightToeBase, () => playerAvatarBase.RightToeBaseAnchorPoint },
            };

            foreach (KeyValuePair<AvatarAnchorPointType, Func<Transform>> anchorPoint in anchorPointMap)
            {
                // Set the anchor point
                pbAvatarAttachComponent.AnchorPointId = anchorPoint.Key;
                pbAvatarAttachComponent.IsDirty = true;
                world.Set(entity, pbAvatarAttachComponent);

                // Update the system
                setupSystem.Update(0);
                system.Update(0);

                // After update, position should match the anchor point
                Vector3 position = anchorPoint.Value().position;

                if (anchorPoint.Key == AvatarAnchorPointType.AaptPosition)
                    position += Vector3.up * AvatarAttachUtils.OLD_CLIENT_PIVOT_CORRECTION;

                Assert.IsTrue(ApproximatelyEqual(position, entityTransformComponent.Transform.position),
                    $"Position should match {anchorPoint.Key} after update");
            }
        }

        [Test]
        public async Task OverrideTransformValuesExceptScale()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptPosition, AvatarId = "", IsDirty = true };
            world.Add(entity, pbAvatarAttachComponent);

            entityTransformComponent.SetTransform(Vector3.one * 5, Quaternion.Euler(90, 90, 90), Vector3.one * 3);
            world.Set(entity, entityTransformComponent);
            Assert.AreEqual(Vector3.one * 5, entityTransformComponent.Transform.position);

            playerAvatarBase.transform.position += Vector3.one * 5;
            playerAvatarBase.transform.rotation = Quaternion.Euler(30, 60, 90);
            Assert.AreNotEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);
            Assert.AreEqual(Vector3.one * 3, entityTransformComponent.Transform.localScale);

            entityTransformComponent.SetTransform(Vector3.one * 10, Quaternion.Euler(22, 6, 99), Vector3.one * 1.77f);
            world.Set(entity, entityTransformComponent);
            playerAvatarBase.transform.position += Vector3.one * 7;
            playerAvatarBase.transform.rotation = Quaternion.Euler(0, 50, 66);
            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);
            Assert.AreEqual(Vector3.one * 1.77f, entityTransformComponent.Transform.localScale);

            entityTransformComponent.SetTransform(Vector3.one * 10, Quaternion.Euler(22, 6, 99), Vector3.one * 5);
            world.Set(entity, entityTransformComponent);
            playerAvatarBase.transform.position += Vector3.one * 60;
            playerAvatarBase.transform.rotation = Quaternion.Euler(15, 37, 55);
            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);
            Assert.AreEqual(Vector3.one * 5, entityTransformComponent.Transform.localScale);
        }

        private Vector3 GetExpectedRootPosition(AvatarBase avatar = null) =>
            (avatar ?? playerAvatarBase).transform.position + (Vector3.up * AvatarAttachUtils.OLD_CLIENT_PIVOT_CORRECTION);

        [Test]
        public async Task StopUpdatingTransformOnComponentRemoval()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptPosition, AvatarId = "", IsDirty = true };
            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);

            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            playerAvatarBase.transform.position += Vector3.one * 5;
            playerAvatarBase.transform.rotation = Quaternion.Euler(30, 60, 90);
            world.Remove<PBAvatarAttach>(entity);
            setupSystem.Update(0);
            system.Update(0);
            Assert.AreNotEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);
        }

        [Test]
        public async Task StopUpdatingTransformOnEntityDeletion()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptPosition, AvatarId = "", IsDirty = true };
            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);

            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            world.Add<DeleteEntityIntention>(entity);
            setupSystem.Update(0);
            system.Update(0);

            playerAvatarBase.transform.position += Vector3.one * 5;
            playerAvatarBase.transform.rotation = Quaternion.Euler(30, 60, 90);
            setupSystem.Update(0);
            system.Update(0);
            Assert.AreNotEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);
        }

        [Test]
        public async Task UpdateTransformOnlyWhenPlayerIsInCurrentScene()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            var pbAvatarAttachComponent = new PBAvatarAttach { AnchorPointId = AvatarAnchorPointType.AaptPosition, AvatarId = "", IsDirty = true };
            world.Add(entity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, entityTransformComponent.Transform.position);
            Assert.AreNotEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);

            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            // Simulate leaving the scene
            sceneStateProvider.IsCurrent.Returns(false);
            playerAvatarBase.transform.position += Vector3.one * 5;
            playerAvatarBase.transform.rotation = Quaternion.Euler(30, 60, 90);
            setupSystem.Update(0);
            system.Update(0);
            Assert.AreNotEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            playerAvatarBase.transform.position += Vector3.one * 5;
            setupSystem.Update(0);
            system.Update(0);
            Assert.AreNotEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreNotEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);

            // Simulate re-entering the scene
            sceneStateProvider.IsCurrent.Returns(true);
            playerAvatarBase.transform.position += Vector3.one * 5;
            setupSystem.Update(0);
            system.Update(0);
            Assert.AreEqual(GetExpectedRootPosition(), entityTransformComponent.Transform.position);
            Assert.AreEqual(playerAvatarBase.transform.rotation, entityTransformComponent.Transform.rotation);
        }

        [Test]
        public async Task AttachToOtherPlayerAvatar()
        {
            await UniTask.WaitUntil(() => system != null);

            GameObject avatarBaseGameObject = await Addressables.LoadAssetAsync<GameObject>("AvatarBase_TestAsset");
            var otherPlayerAvatarBase = Object.Instantiate(avatarBaseGameObject.GetComponent<AvatarBase>());
            otherPlayerAvatarBase.gameObject.transform.position = new Vector3(10, 10, 10);

            int otherPlayerId = SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM;

            Entity otherPlayerEntity = globalWorld.Create(
                new CRDTEntity(otherPlayerId),
                new PlayerComponent(Substitute.For<Transform>()),
                otherPlayerAvatarBase,
                new AvatarShapeComponent { ID = otherPlayerId.ToString() }
            );

            var attachToOtherEntity = world.Create(PartitionComponent.TOP_PRIORITY);
            var attachToOtherTransform = AddTransformToEntity(attachToOtherEntity);

            var pbAvatarAttachComponent = new PBAvatarAttach
            {
                AvatarId = otherPlayerId.ToString(),
                AnchorPointId = AvatarAnchorPointType.AaptPosition,
                IsDirty = true
            };
            world.Add(attachToOtherEntity, pbAvatarAttachComponent);

            Assert.AreEqual(Vector3.zero, attachToOtherTransform.Transform.position);

            setupSystem.Update(0);
            system.Update(0);

            Assert.AreEqual(GetExpectedRootPosition(otherPlayerAvatarBase), attachToOtherTransform.Transform.position);
            Assert.AreEqual(otherPlayerAvatarBase.transform.rotation, attachToOtherTransform.Transform.rotation);

            // Move the other player and verify the attachment follows
            otherPlayerAvatarBase.transform.position += Vector3.one * 5;
            otherPlayerAvatarBase.transform.rotation = Quaternion.Euler(30, 60, 90);

            system.Update(0);

            Assert.AreEqual(GetExpectedRootPosition(otherPlayerAvatarBase), attachToOtherTransform.Transform.position);
            Assert.AreEqual(otherPlayerAvatarBase.transform.rotation, attachToOtherTransform.Transform.rotation);

            Object.DestroyImmediate(otherPlayerAvatarBase.gameObject);
            Object.DestroyImmediate(attachToOtherTransform.Transform.gameObject);
            globalWorld.Destroy(otherPlayerEntity);
        }

        [Test]
        public async Task HandlePlayerDisconnection()
        {
            await UniTask.WaitUntil(() => system != null);

            GameObject avatarBaseGameObject = await Addressables.LoadAssetAsync<GameObject>("AvatarBase_TestAsset");
            var disconnectingPlayerAvatarBase = Object.Instantiate(avatarBaseGameObject.GetComponent<AvatarBase>());
            disconnectingPlayerAvatarBase.gameObject.transform.position = new Vector3(15, 15, 15);

            int disconnectingPlayerId = SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 1;
            string disconnectingPlayerIdString = disconnectingPlayerId.ToString();

            Entity disconnectingPlayerEntity = globalWorld.Create(
                new CRDTEntity(disconnectingPlayerId),
                new PlayerComponent(Substitute.For<Transform>()),
                disconnectingPlayerAvatarBase,
                new AvatarShapeComponent { ID = disconnectingPlayerIdString }
            );

            var attachToDisconnectingEntity = world.Create(PartitionComponent.TOP_PRIORITY);
            var attachToDisconnectingTransform = AddTransformToEntity(attachToDisconnectingEntity);

            var pbAvatarAttachComponent = new PBAvatarAttach
            {
                AvatarId = disconnectingPlayerIdString,
                AnchorPointId = AvatarAnchorPointType.AaptPosition,
                IsDirty = true
            };
            world.Add(attachToDisconnectingEntity, pbAvatarAttachComponent);

            setupSystem.Update(0);
            system.Update(0);

            Assert.AreEqual(GetExpectedRootPosition(disconnectingPlayerAvatarBase), attachToDisconnectingTransform.Transform.position);

            Vector3 lastKnownPosition = attachToDisconnectingTransform.Transform.position;

            // Simulate player disconnection
            Object.DestroyImmediate(disconnectingPlayerAvatarBase.gameObject);
            globalWorld.Destroy(disconnectingPlayerEntity);

            LogAssert.Expect(LogType.Log, $"Failed to find avatar with ID {disconnectingPlayerIdString}");

            system.Update(0);

            // Entity should be moved to "mordor" position when player is gone
            Assert.AreEqual(MordorConstants.AVATAR_ATTACH_MORDOR_POSITION, attachToDisconnectingTransform.Transform.position);
            Assert.AreNotEqual(lastKnownPosition, attachToDisconnectingTransform.Transform.position);

            Object.DestroyImmediate(attachToDisconnectingTransform.Transform.gameObject);
        }

        [Test]
        public async Task HandleConcurrentAttachments()
        {
            await UniTask.WaitUntil(() => system != null);

            GameObject avatarBaseGameObject = await Addressables.LoadAssetAsync<GameObject>("AvatarBase_TestAsset");
            var targetPlayerAvatarBase = Object.Instantiate(avatarBaseGameObject.GetComponent<AvatarBase>());
            targetPlayerAvatarBase.gameObject.transform.position = new Vector3(20, 20, 20);

            int targetPlayerId = SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 2;

            Entity targetPlayerEntity = globalWorld.Create(
                new CRDTEntity(targetPlayerId),
                new PlayerComponent(Substitute.For<Transform>()),
                targetPlayerAvatarBase,
                new AvatarShapeComponent { ID = targetPlayerId.ToString() }
            );

            // Create multiple entities to attach to the same player
            const int attachmentCount = 5;
            var attachEntities = new Entity[attachmentCount];
            var attachTransforms = new TransformComponent[attachmentCount];

            for (int i = 0; i < attachmentCount; i++)
            {
                attachEntities[i] = world.Create(PartitionComponent.TOP_PRIORITY);
                attachTransforms[i] = AddTransformToEntity(attachEntities[i]);

                // Use different anchor points for each attachment
                var anchorPoint = (AvatarAnchorPointType)(i % 3 == 0
                    ? AvatarAnchorPointType.AaptPosition
                    : (i % 3 == 1 ? AvatarAnchorPointType.AaptLeftHand : AvatarAnchorPointType.AaptRightHand));

                var pbAvatarAttachComponent = new PBAvatarAttach
                {
                    AvatarId = targetPlayerId.ToString(),
                    AnchorPointId = anchorPoint,
                    IsDirty = true
                };
                world.Add(attachEntities[i], pbAvatarAttachComponent);
            }

            setupSystem.Update(0);
            system.Update(0);

            // Verify each attachment is at the correct position
            for (int i = 0; i < attachmentCount; i++)
            {
                Transform expectedAnchorPoint;
                Vector3 expectedPosition;

                if (i % 3 == 0)
                {
                    expectedAnchorPoint = targetPlayerAvatarBase.transform;
                    expectedPosition = GetExpectedRootPosition(targetPlayerAvatarBase);
                }
                else if (i % 3 == 1)
                {
                    expectedAnchorPoint = targetPlayerAvatarBase.LeftHandAnchorPoint;
                    expectedPosition = expectedAnchorPoint.position;
                }
                else
                {
                    expectedAnchorPoint = targetPlayerAvatarBase.RightHandAnchorPoint;
                    expectedPosition = expectedAnchorPoint.position;
                }

                Assert.AreEqual(expectedPosition, attachTransforms[i].Transform.position);
                Assert.AreEqual(expectedAnchorPoint.rotation.ToString(), attachTransforms[i].Transform.rotation.ToString());
            }

            // Move the target player and verify all attachments follow
            targetPlayerAvatarBase.transform.position += Vector3.one * 10;
            targetPlayerAvatarBase.transform.rotation = Quaternion.Euler(45, 45, 45);

            system.Update(0);

            for (int i = 0; i < attachmentCount; i++)
            {
                Transform expectedAnchorPoint;
                Vector3 expectedPosition;

                if (i % 3 == 0)
                {
                    expectedAnchorPoint = targetPlayerAvatarBase.transform;
                    expectedPosition = GetExpectedRootPosition(targetPlayerAvatarBase);
                }
                else if (i % 3 == 1)
                {
                    expectedAnchorPoint = targetPlayerAvatarBase.LeftHandAnchorPoint;
                    expectedPosition = expectedAnchorPoint.position;
                }
                else
                {
                    expectedAnchorPoint = targetPlayerAvatarBase.RightHandAnchorPoint;
                    expectedPosition = expectedAnchorPoint.position;
                }

                Assert.AreEqual(expectedPosition, attachTransforms[i].Transform.position);
                Assert.AreEqual(expectedAnchorPoint.rotation.ToString(), attachTransforms[i].Transform.rotation.ToString());
            }

            Object.DestroyImmediate(targetPlayerAvatarBase.gameObject);
            globalWorld.Destroy(targetPlayerEntity);

            for (int i = 0; i < attachmentCount; i++)
            {
                Object.DestroyImmediate(attachTransforms[i].Transform.gameObject);
            }
        }

        [Test]
        public async Task UseEntityParticipantTableForLookup()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            // Create a target avatar
            GameObject avatarBaseGameObject = await Addressables.LoadAssetAsync<GameObject>("AvatarBase_TestAsset");
            var targetAvatarBase = Object.Instantiate(avatarBaseGameObject.GetComponent<AvatarBase>());
            targetAvatarBase.gameObject.transform.position = new Vector3(20, 20, 20);

            string targetAvatarId = "test-avatar-id";
            Entity targetEntity = globalWorld.Create(
                targetAvatarBase,
                new AvatarShapeComponent { ID = targetAvatarId }
            );

            // Set up the EntityParticipantTable mock to return the target entity
            var entry = new IReadOnlyEntityParticipantTable.Entry(targetAvatarId, targetEntity, RoomSource.GATEKEEPER);
            entityParticipantTable.TryGet(targetAvatarId, out _).Returns(x => {
                x[1] = entry;
                return true;
            });

            // Create an avatar attach component that references the target avatar
            var pbAvatarAttachComponent = new PBAvatarAttach {
                AnchorPointId = AvatarAnchorPointType.AaptPosition,
                AvatarId = targetAvatarId,
                IsDirty = true
            };
            world.Add(entity, pbAvatarAttachComponent);

            // Run the systems
            setupSystem.Update(0);
            system.Update(0);

            // Verify that the EntityParticipantTable was used for lookup
            // Both the setup system and the handler system call FindAvatarUtils.AvatarWithID
            entityParticipantTable.Received(2).TryGet(targetAvatarId, out _);

            // Clean up
            Object.DestroyImmediate(targetAvatarBase.gameObject);
        }

        [Test]
        public async Task FallbackToQueryWhenAvatarNotFoundInEntityParticipantTable()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => system != null);

            // Create a target avatar
            GameObject avatarBaseGameObject = await Addressables.LoadAssetAsync<GameObject>("AvatarBase_TestAsset");
            var targetAvatarBase = Object.Instantiate(avatarBaseGameObject.GetComponent<AvatarBase>());
            targetAvatarBase.gameObject.transform.position = new Vector3(20, 20, 20);

            string targetAvatarId = "test-avatar-id";
            Entity targetEntity = globalWorld.Create(
                targetAvatarBase,
                new AvatarShapeComponent { ID = targetAvatarId }
            );

            // Set up the EntityParticipantTable mock to return false for TryGet
            // This simulates the avatar not being found in the table
            entityParticipantTable.TryGet(targetAvatarId, out _).Returns(false);

            // Create an avatar attach component that references the target avatar
            var pbAvatarAttachComponent = new PBAvatarAttach {
                AnchorPointId = AvatarAnchorPointType.AaptPosition,
                AvatarId = targetAvatarId,
                IsDirty = true
            };
            world.Add(entity, pbAvatarAttachComponent);

            // Run the systems
            setupSystem.Update(0);
            system.Update(0);

            // Verify that the EntityParticipantTable was queried
            entityParticipantTable.Received(2).TryGet(targetAvatarId, out _);

            // Verify that the avatar was found via the fallback query mechanism
            Assert.IsTrue(world.Has<AvatarAttachComponent>(entity),
                "The AvatarAttachComponent should have been added, indicating the avatar was found via fallback query");

            // Clean up
            Object.DestroyImmediate(targetAvatarBase.gameObject);
        }
    }
}
