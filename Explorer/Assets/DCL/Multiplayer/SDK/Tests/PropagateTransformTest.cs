#nullable enable

using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Multiplayer.SDK.Systems.SceneWorld;
using Google.Protobuf;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Multiplayer.SDK.Tests
{
    public class PropagateTransformTest
    {
        [Test]
        public void PropagateTransform()
        {
            const int CRDT_ID = 100;

            var world = World.Create();
            var ecsWorld = World.Create();

            var writer = new FakeECSToCRDTWriter();
            var sceneData = new ISceneData.Fake();

            var propagationSystem = new PlayerTransformPropagationSystem(world);
            var writeSystem = new WritePlayerTransformSystem(ecsWorld, writer, sceneData);

            var sceneWorldEntity = ecsWorld.Create();

            var playerCRDTEntity = new PlayerCRDTEntity(
                new CRDTEntity(CRDT_ID),
                new ISceneFacade.Fake(
                    new SceneShortInfo(Vector2Int.zero, string.Empty),
                    new SceneStateProvider(),
                    new SceneEcsExecutor(ecsWorld),
                    false
                ),
                sceneWorldEntity
            );

            world.Create(
                new IAvatarView.Fake(Vector3.zero, Quaternion.identity) as IAvatarView,
                playerCRDTEntity
            );

            ecsWorld.Add(sceneWorldEntity, playerCRDTEntity);

            propagationSystem.Update(0);
            writeSystem.Update(0);


            Assert.AreEqual(1, writer.Messages.Count);

            var message = writer.Messages.First();

            Assert.AreEqual(typeof(SDKTransform), message.MessageType);
            Assert.AreEqual(typeof(SDKTransform), message.Data.GetType());
            Assert.AreEqual(CRDT_ID, message.Entity.Id);
        }

        private class FakeECSToCRDTWriter : IECSToCRDTWriter
        {
            public readonly struct Message
            {
                public readonly CRDTEntity Entity;
                public readonly Type MessageType;
                public readonly object Data;

                public Message(CRDTEntity entity, Type messageType, object data)
                {
                    Entity = entity;
                    MessageType = messageType;
                    Data = data;
                }
            }

            public readonly List<Message> Messages = new ();

            public TMessage PutMessage<TMessage, TData>(Action<TMessage, TData> prepareMessage, CRDTEntity entity, TData data) where TMessage: class, IMessage
            {
                Messages.Add(new Message(entity, typeof(TMessage), data));
                return null;
            }

            public void PutMessage<TMessage>(TMessage message, CRDTEntity entity) where TMessage: class, IMessage
            {
                throw new NotImplementedException();
            }

            public TMessage AppendMessage<TMessage, TData>(Action<TMessage, TData> prepareMessage, CRDTEntity entity, int timestamp, TData data) where TMessage: class, IMessage =>
                throw new NotImplementedException();

            public void DeleteMessage<T>(CRDTEntity crdtID) where T: class, IMessage
            {
                throw new NotImplementedException();
            }
        }
    }
}
