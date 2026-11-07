using Decentraland.Pulse;
using NUnit.Framework;

namespace DCL.Multiplayer.Connections.Pulse.Tests
{
    [TestFixture]
    public class ClientMessagePoolShould
    {
        private ClientMessagePool pool = null!;

        [SetUp]
        public void SetUp()
        {
            pool = new ClientMessagePool();
        }

        [TestCase(ClientMessage.MessageOneofCase.Handshake)]
        [TestCase(ClientMessage.MessageOneofCase.Input)]
        [TestCase(ClientMessage.MessageOneofCase.Resync)]
        [TestCase(ClientMessage.MessageOneofCase.ProfileAnnouncement)]
        [TestCase(ClientMessage.MessageOneofCase.EmoteStart)]
        [TestCase(ClientMessage.MessageOneofCase.EmoteStop)]
        [TestCase(ClientMessage.MessageOneofCase.Teleport)]
        public void ReturnAMessageOfTheRequestedKind(ClientMessage.MessageOneofCase kind)
        {
            ClientMessage message = pool.Get(kind);

            Assert.AreEqual(kind, message.MessageCase);

            pool.Release(message);
        }

        [Test]
        public void ClearAReleasedResyncPayload()
        {
            ClientMessage message = pool.Get(ClientMessage.MessageOneofCase.Resync);
            message.Resync.SubjectId = 7;
            message.Resync.KnownSeq = 3;
            pool.Release(message);

            Assert.AreEqual(0u, message.Resync.SubjectId);
            Assert.AreEqual(0u, message.Resync.KnownSeq);
        }
    }
}
