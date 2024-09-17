using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Character.Components;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Multiplayer.SDK.Systems.SceneWorld;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.SDK.Tests
{
    public class PropagateTransformTest
    {
        [Test]
        public void PropagateTransform()
        {
            const int CRDT_ID = 100;

            var globalWorld = World.Create();
            var sceneWorld = World.Create();

            var writer = Substitute.For<IECSToCRDTWriter>();
            var sceneData = Substitute.For<ISceneData>();
            Vector3 sceneBasePos = new Vector3(15, 0, -56);
            var sceneGeometry = new ParcelMathHelper.SceneGeometry(sceneBasePos, new ParcelMathHelper.SceneCircumscribedPlanes(), 20);
            sceneData.Geometry.Returns(sceneGeometry);

            var propagationSystem = new PlayerTransformPropagationSystem(globalWorld);
            var writeSystem = new WritePlayerTransformSystem(sceneWorld, writer, sceneData);

            var sceneFacade = Substitute.For<ISceneFacade>();
            var sceneEcsExecutor = new SceneEcsExecutor(sceneWorld);
            sceneFacade.EcsExecutor.Returns(sceneEcsExecutor);

            var sceneWorldEntity = sceneWorld.Create();
            var crdtEntity = new CRDTEntity(CRDT_ID);
            var playerCRDTEntity = new PlayerCRDTEntity(
                crdtEntity,
                sceneFacade,
                sceneWorldEntity
            );
            var playerSceneCRDTEntity = new PlayerSceneCRDTEntity(crdtEntity);

            Transform fakeCharaTransform = new GameObject("fake character").transform;
            fakeCharaTransform.position = Vector3.one * 6;
            fakeCharaTransform.rotation = Quaternion.Euler(15, 6, 89);
            var charaTransform = new CharacterTransform(fakeCharaTransform);
            globalWorld.Create(
                charaTransform,
                playerCRDTEntity
            );
            sceneWorld.Add(sceneWorldEntity, playerSceneCRDTEntity);

            propagationSystem.Update(0);
            writeSystem.Update(0);

            writer.Received(1)
                           .PutMessage(
                                Arg.Any<Action<SDKTransform, (IExposedTransform, Vector3)>>(),
                                playerCRDTEntity.CRDTEntity,
                                Arg.Is<(IExposedTransform exposedTransform, Vector3 scenePosition)>(data =>
                                    data.exposedTransform.Position.Value.Equals(fakeCharaTransform.position)
                                    && data.exposedTransform.Rotation.Value.Equals(fakeCharaTransform.rotation)
                                    && data.scenePosition.Equals(sceneData.Geometry.BaseParcelPosition)));
            writer.ClearReceivedCalls();

            // Cleanup
            globalWorld.Dispose();
            sceneWorld.Dispose();
            propagationSystem.Dispose();
            writeSystem.Dispose();
            GameObject.DestroyImmediate(fakeCharaTransform.gameObject);
        }
    }
}
