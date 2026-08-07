using CRDT;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.OutgoingMessages;
using DCL.ECS7;
using DCL.ECSComponents;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Collections.Generic;

namespace CrdtEcsBridge.RestrictedActions.Tests
{
    /// <summary>
    ///     Covers the protocol plumbing of <see cref="PBExplorerUiEventsResult" />: it leaves the renderer as a
    ///     grow-only append message addressed to the scene root under its generated component id, its oneof
    ///     payload survives serialization, and a message instance taken back from the pool carries no residue
    ///     of the event it transported before.
    /// </summary>
    public class ExplorerUiEventsResultShould
    {
        private OutgoingCRDTMessagesProvider provider;
        private List<AppendedMessage> appendedMessages;

        [SetUp]
        public void SetUp()
        {
            appendedMessages = new List<AppendedMessage>();

            var crdtProtocol = Substitute.For<ICRDTProtocol>();

            crdtProtocol.CreateAppendMessage(Arg.Any<CRDTEntity>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<IMemoryOwner<byte>>())
                        .Returns(callInfo =>
                         {
                             appendedMessages.Add(new AppendedMessage(
                                 callInfo.ArgAt<CRDTEntity>(0),
                                 callInfo.ArgAt<int>(1),
                                 callInfo.ArgAt<int>(2),
                                 PBExplorerUiEventsResult.Parser.ParseFrom(callInfo.ArgAt<IMemoryOwner<byte>>(3).Memory.ToArray())));

                             return default(ProcessedCRDTMessage);
                         });

            var componentsRegistry = new SDKComponentsRegistry();

            // Registered the way ComponentsContainer registers it, so a divergence there surfaces here.
            componentsRegistry.Add(SDKComponentBuilder<PBExplorerUiEventsResult>.Create(ComponentID.EXPLORER_UI_EVENTS_RESULT).AsProtobufResult());

            provider = new OutgoingCRDTMessagesProvider(componentsRegistry, crdtProtocol, CRDTPooledMemoryAllocator.Create());
        }

        [TearDown]
        public void TearDown()
        {
            provider.Dispose();
        }

        [Test]
        public void ReachTheSceneRootAsAnAppendMessage()
        {
            Append(ExplorerUi.EuMap, 42, static result => result.Opened = new PBExplorerUiEventsResult.Types.UiOpened());

            Flush();

            Assert.That(appendedMessages, Has.Count.EqualTo(1));
            Assert.That(appendedMessages[0].Entity.Id, Is.EqualTo(SpecialEntitiesID.SCENE_ROOT_ENTITY));
            Assert.That(appendedMessages[0].ComponentId, Is.EqualTo(ComponentID.EXPLORER_UI_EVENTS_RESULT));
            Assert.That(appendedMessages[0].Timestamp, Is.EqualTo(42));
            Assert.That(appendedMessages[0].Payload.Ui, Is.EqualTo(ExplorerUi.EuMap));
            Assert.That(appendedMessages[0].Payload.Timestamp, Is.EqualTo(42u));
            Assert.That(appendedMessages[0].Payload.EventCase, Is.EqualTo(PBExplorerUiEventsResult.EventOneofCase.Opened));
        }

        [Test]
        public void KeepEveryEventOfTheSameTick()
        {
            Append(ExplorerUi.EuBackpack, 7, static result => result.Opened = new PBExplorerUiEventsResult.Types.UiOpened());
            Append(ExplorerUi.EuBackpack, 7, static result => result.Closed = new PBExplorerUiEventsResult.Types.UiClosed());

            Flush();

            // Grow-only semantics: unlike a put, a second append to the same entity and component must not
            // overwrite the first one.
            Assert.That(appendedMessages, Has.Count.EqualTo(2));
            Assert.That(appendedMessages[0].Payload.EventCase, Is.EqualTo(PBExplorerUiEventsResult.EventOneofCase.Opened));
            Assert.That(appendedMessages[1].Payload.EventCase, Is.EqualTo(PBExplorerUiEventsResult.EventOneofCase.Closed));
        }

        [Test]
        public void ClearTheEventVariantOfAPooledMessage()
        {
            Append(ExplorerUi.EuMap, 1, static result => result.Opened = new PBExplorerUiEventsResult.Types.UiOpened());
            Flush();

            Append(ExplorerUi.EuSettings, 0, null);
            Flush();

            Assert.That(appendedMessages, Has.Count.EqualTo(2));
            Assert.That(appendedMessages[1].Payload.EventCase, Is.EqualTo(PBExplorerUiEventsResult.EventOneofCase.None));
            Assert.That(appendedMessages[1].Payload.Ui, Is.EqualTo(ExplorerUi.EuSettings));
            Assert.That(appendedMessages[1].Payload.Timestamp, Is.EqualTo(0u));
        }

        private void Append(ExplorerUi ui, int tick, Action<PBExplorerUiEventsResult> setEvent)
        {
            provider.AppendMessage<PBExplorerUiEventsResult, (ExplorerUi ui, int tick, Action<PBExplorerUiEventsResult> setEvent)>(
                static (result, data) =>
                {
                    result.Ui = data.ui;
                    result.Timestamp = (uint)data.tick;
                    data.setEvent?.Invoke(result);
                }, SpecialEntitiesID.SCENE_ROOT_ENTITY, tick, (ui, tick, setEvent));
        }

        private void Flush()
        {
            using OutgoingCRDTMessagesSyncBlock syncBlock = provider.GetSerializationSyncBlock(null);
        }

        private readonly struct AppendedMessage
        {
            public readonly CRDTEntity Entity;
            public readonly int ComponentId;
            public readonly int Timestamp;
            public readonly PBExplorerUiEventsResult Payload;

            public AppendedMessage(CRDTEntity entity, int componentId, int timestamp, PBExplorerUiEventsResult payload)
            {
                Entity = entity;
                ComponentId = componentId;
                Timestamp = timestamp;
                Payload = payload;
            }
        }
    }
}
