using CRDT.Attribution;
using CRDT.Protocol;
using DCL.ECS7;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace CRDT.CRDTTests
{
    [TestFixture]
    public class CrdtWriterLogShould
    {
        private const string SCENE = "bafkscene";
        private const string SERVER = ICrdtWriterLog.AUTHORITATIVE_SERVER_ADDRESS;
        private const string PEER = "0xdeadbeef";

        private const int TRANSFORM = ComponentID.TRANSFORM;

        private CrdtWriterLog log = null!;
        private List<CrdtWrite> writes = null!;
        private List<CrdtWriterSummary> writers = null!;

        [SetUp]
        public void SetUp()
        {
            log = new CrdtWriterLog();
            writes = new List<CrdtWrite>();
            writers = new List<CrdtWriterSummary>();
        }

        [Test]
        public void RecordNothingUntilEnabled()
        {
            // Act
            log.RecordInbound(SCENE, SERVER, true, Batch(PutNetwork(entity: 512, component: TRANSFORM, timestamp: 1)));
            log.EntityWrites(SCENE, 512, writes);

            // Assert
            Assert.That(log.IsEnabled, Is.False);
            Assert.That(writes, Is.Empty);
        }

        [Test]
        public void AttributeAWriteToThePeerThatSentIt()
        {
            // Arrange
            log.Enable();

            // Act
            log.RecordInbound(SCENE, SERVER, true, Batch(PutNetwork(entity: 512, component: TRANSFORM, timestamp: 7)));
            log.EntityWrites(SCENE, 512, writes);

            // Assert
            Assert.That(writes.Count, Is.EqualTo(1));
            Assert.That(writes[0].Writer, Is.EqualTo(SERVER));
            Assert.That(writes[0].IsAuthoritativeServer, Is.True);
            Assert.That(writes[0].ComponentId, Is.EqualTo(TRANSFORM));
            Assert.That(writes[0].CrdtTimestamp, Is.EqualTo(7));
            Assert.That(writes[0].MessageType, Is.EqualTo(CRDTMessageType.PUT_COMPONENT_NETWORK));
        }

        /// <summary>
        ///     The question an authoritative game asks: the last write to a component came from a client, not the server.
        /// </summary>
        [Test]
        public void ReportTheLatestWriterOfAComponent()
        {
            // Arrange
            log.Enable();
            log.RecordInbound(SCENE, SERVER, true, Batch(PutNetwork(entity: 512, component: TRANSFORM, timestamp: 1)));

            // Act
            log.RecordInbound(SCENE, PEER, false, Batch(PutNetwork(entity: 512, component: TRANSFORM, timestamp: 2)));
            log.EntityWrites(SCENE, 512, writes);

            // Assert
            Assert.That(writes.Count, Is.EqualTo(1));
            Assert.That(writes[0].Writer, Is.EqualTo(PEER));
            Assert.That(writes[0].IsAuthoritativeServer, Is.False);
            Assert.That(writes[0].IsTrustedSource, Is.False);
        }

        [Test]
        public void KeepWritesOfDifferentEntitiesApart()
        {
            // Arrange
            log.Enable();

            // Act
            log.RecordInbound(SCENE, SERVER, true, Batch(PutNetwork(entity: 512, component: TRANSFORM, timestamp: 1)));
            log.RecordInbound(SCENE, PEER, false, Batch(PutNetwork(entity: 513, component: TRANSFORM, timestamp: 1)));
            log.EntityWrites(SCENE, 513, writes);

            // Assert
            Assert.That(writes.Count, Is.EqualTo(1));
            Assert.That(writes[0].EntityId, Is.EqualTo(513));
            Assert.That(writes[0].Writer, Is.EqualTo(PEER));
        }

        [Test]
        public void ReadThroughTheAddressPrefixOfAStateResponse()
        {
            // Arrange
            log.Enable();
            byte[] batch = StateResponse(PEER, PutNetwork(entity: 512, component: TRANSFORM, timestamp: 3));

            // Act
            log.RecordInbound(SCENE, PEER, false, batch);
            log.EntityWrites(SCENE, 512, writes);

            // Assert
            Assert.That(writes.Count, Is.EqualTo(1));
            Assert.That(writes[0].CrdtTimestamp, Is.EqualTo(3));
        }

        /// <summary>
        ///     A state dump replays writes its sender did not necessarily author — a client hydrating mid-game gets
        ///     the server's writes handed to it by whichever peer answered. Crediting them to that peer would make
        ///     every authorship test wrong, so the rows are marked and never read as authoritative.
        /// </summary>
        [Test]
        public void NotCreditAuthorshipToThePeerThatRelayedAStateDump()
        {
            // Arrange
            log.Enable();

            // Act — the peer answers our state request with state the server originally wrote
            log.RecordInbound(SCENE, PEER, false, StateResponse(PEER, PutNetwork(entity: 512, component: TRANSFORM, timestamp: 3)));
            log.EntityWrites(SCENE, 512, writes);

            // Assert
            Assert.That(writes[0].ViaStateSync, Is.True);
            Assert.That(writes[0].IsAuthoritativeServer, Is.False);
        }

        /// <summary>
        ///     Even when the authoritative server is the peer answering the request, a replayed row is not a live
        ///     assertion by it, so IsAuthoritativeServer stays false and only the flag distinguishes the two.
        /// </summary>
        [Test]
        public void NotReadAStateDumpFromTheServerAsALiveServerWrite()
        {
            // Arrange
            log.Enable();

            // Act
            log.RecordInbound(SCENE, SERVER, true, StateResponse(SERVER, PutNetwork(entity: 512, component: TRANSFORM, timestamp: 3)));
            log.EntityWrites(SCENE, 512, writes);

            // Assert
            Assert.That(writes[0].Writer, Is.EqualTo(SERVER));
            Assert.That(writes[0].ViaStateSync, Is.True);
            Assert.That(writes[0].IsAuthoritativeServer, Is.False);
        }

        [Test]
        public void CountRelayedRowsApartFromLiveWrites()
        {
            // Arrange
            log.Enable();

            // Act
            log.RecordInbound(SCENE, PEER, false, Batch(PutNetwork(entity: 512, component: TRANSFORM, timestamp: 1)));
            log.RecordInbound(SCENE, PEER, false, StateResponse(PEER, PutNetwork(entity: 513, component: TRANSFORM, timestamp: 1)));
            log.SceneWriters(SCENE, writers);

            // Assert
            Assert.That(writers.Count, Is.EqualTo(1));
            Assert.That(writers[0].Writes, Is.EqualTo(1));
            Assert.That(writers[0].StateSyncWrites, Is.EqualTo(1));
        }

        /// <summary>
        ///     A component row must never name an address the scene-level summary omits, or the two tools would
        ///     contradict each other. When the writer budget is full the write is refused outright and counted.
        /// </summary>
        [Test]
        public void NeverNameAWriterTheSceneSummaryOmits()
        {
            // Arrange — more distinct addresses than a scene may summarise
            log.Enable();
            const int OVERSHOOT = 200;

            for (var i = 0; i < OVERSHOOT; i++)
                log.RecordInbound(SCENE, $"0x{i:x8}", false, Batch(PutNetwork(entity: 512 + i, component: TRANSFORM, timestamp: 1)));

            log.SceneWriters(SCENE, writers);

            // Assert — the budget held, and the overflow was counted rather than dropped silently
            Assert.That(writers.Count, Is.LessThan(OVERSHOOT));
            Assert.That(log.DroppedWrites(SCENE), Is.GreaterThan(0));

            // Assert — every recorded component row resolves to a summarised address
            var summarised = new HashSet<string>();

            foreach (CrdtWriterSummary writer in writers)
                summarised.Add(writer.Address);

            for (var i = 0; i < OVERSHOOT; i++)
            {
                writes.Clear();
                log.EntityWrites(SCENE, 512 + i, writes);

                foreach (CrdtWrite write in writes)
                    Assert.That(summarised, Does.Contain(write.Writer));
            }
        }

        /// <summary>
        ///     A write the CRDT filter withholds from the scene never reached the scene's state, so attributing it
        ///     would describe a change the agent cannot observe.
        /// </summary>
        [Test]
        public void NotAttributeAWriteTheSceneNeverReceives()
        {
            // Arrange
            log.Enable();

            // Act
            log.RecordInbound(SCENE, PEER, isTrustedSource: false,
                Batch(PutNetwork(entity: 512, component: ComponentID.VIDEO_PLAYER, timestamp: 1)));

            log.EntityWrites(SCENE, 512, writes);

            // Assert
            Assert.That(writes, Is.Empty);
        }

        [Test]
        public void SummariseEveryWriterOfAScene()
        {
            // Arrange
            log.Enable();

            // Act
            log.RecordInbound(SCENE, SERVER, true, Batch(PutNetwork(entity: 512, component: TRANSFORM, timestamp: 1)));
            log.RecordInbound(SCENE, SERVER, true, Batch(PutNetwork(entity: 513, component: TRANSFORM, timestamp: 1)));
            log.RecordInbound(SCENE, PEER, false, Batch(PutNetwork(entity: 514, component: TRANSFORM, timestamp: 1)));
            log.SceneWriters(SCENE, writers);

            // Assert
            Assert.That(writers.Count, Is.EqualTo(2));
            Assert.That(writers.Find(writer => writer.Address == SERVER).Writes, Is.EqualTo(2));
            Assert.That(writers.Find(writer => writer.Address == PEER).Writes, Is.EqualTo(1));
            Assert.That(writers.Find(writer => writer.Address == SERVER).IsAuthoritativeServer, Is.True);
        }

        [Test]
        public void ReportNothingForAnUnknownScene()
        {
            // Arrange
            log.Enable();
            log.RecordInbound(SCENE, SERVER, true, Batch(PutNetwork(entity: 512, component: TRANSFORM, timestamp: 1)));

            // Act
            log.EntityWrites("other-scene", 512, writes);
            log.SceneWriters("other-scene", writers);

            // Assert
            Assert.That(writes, Is.Empty);
            Assert.That(writers, Is.Empty);
        }

        [Test]
        public void SurviveATruncatedBatch()
        {
            // Arrange
            log.Enable();
            byte[] truncated = Batch(PutNetwork(entity: 512, component: TRANSFORM, timestamp: 1))[..^6];

            // Act & Assert
            Assert.DoesNotThrow(() => log.RecordInbound(SCENE, SERVER, true, truncated));
        }

        /// <summary>A CRDT batch as the SDK sends it: the message type byte followed by the messages.</summary>
        private static byte[] Batch(params byte[][] messages)
        {
            var stream = new MemoryStream();
            stream.WriteByte((byte)SdkCommsMessageType.CRDT);

            foreach (byte[] message in messages)
                stream.Write(message, 0, message.Length);

            return stream.ToArray();
        }

        /// <summary>A RES_CRDT_STATE payload: the type byte, the subject's address, then the messages.</summary>
        private static byte[] StateResponse(string address, params byte[][] messages)
        {
            var stream = new MemoryStream();
            stream.WriteByte((byte)SdkCommsMessageType.ResCRDTState);
            stream.WriteByte((byte)address.Length);

            foreach (char character in address)
                stream.WriteByte((byte)character);

            foreach (byte[] message in messages)
                stream.Write(message, 0, message.Length);

            return stream.ToArray();
        }

        /// <summary>[length][type][entity][component][timestamp][network id][data length][data]</summary>
        private static byte[] PutNetwork(int entity, int component, int timestamp, int dataLength = 4)
        {
            var writer = new BinaryWriter(new MemoryStream());
            writer.Write(CRDTConstants.MESSAGE_HEADER_LENGTH + 20 + dataLength);
            writer.Write((int)CRDTMessageType.PUT_COMPONENT_NETWORK);
            writer.Write(entity);
            writer.Write(component);
            writer.Write(timestamp);
            writer.Write(0); // network id
            writer.Write(dataLength);

            for (var i = 0; i < dataLength; i++)
                writer.Write((byte)0);

            return ((MemoryStream)writer.BaseStream).ToArray();
        }
    }
}
