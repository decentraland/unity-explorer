using NUnit.Framework;
using System.Text;

namespace ECS.StreamableLoading.Fonts.Tests
{
    [TestFixture]
    public class FontDownloadHandlerShould
    {
        [Test]
        public void AcceptChunksUpToTheLimit()
        {
            using var handler = new TestHandler(4);

            Assert.That(handler.Receive(new byte[] { 1, 2, 99 }, 2), Is.True);
            Assert.That(handler.Receive(new byte[] { 3, 4 }, 2), Is.True);
            Assert.That(handler.data, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            Assert.That(handler.LimitExceeded, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RejectOverflowWithoutTrustingContentLength(bool sendSmallerHeader)
        {
            using var handler = new TestHandler(4);

            if (sendSmallerHeader)
                handler.Header(1);

            Assert.That(handler.Receive(new byte[] { 1, 2, 3 }, 3), Is.True);
            Assert.That(handler.Receive(new byte[] { 4, 5 }, 2), Is.False);
            Assert.That(handler.LimitExceeded, Is.True);
            Assert.That(handler.data, Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(handler.Receive(new byte[] { 4 }, 1), Is.False);
        }

        [Test]
        public void RejectAnOversizedHeaderBeforeBufferingTheBody()
        {
            using var handler = new TestHandler(4);
            handler.Header(ulong.MaxValue);

            Assert.That(handler.LimitExceeded, Is.True);
            Assert.That(handler.Receive(new byte[] { 1 }, 1), Is.False);
            Assert.That(handler.data, Is.Empty);
        }

        [Test]
        public void DecodeUtf8CatalogText()
        {
            using var handler = new TestHandler(128);
            byte[] bytes = Encoding.UTF8.GetBytes("{\"family\":\"café\"}");
            handler.Receive(bytes, bytes.Length);

            Assert.That(handler.text, Is.EqualTo("{\"family\":\"café\"}"));
        }

        private sealed class TestHandler : FontDownloadHandler
        {
            public TestHandler(int maxBytes) : base(maxBytes) { }

            public bool Receive(byte[] bytes, int count) =>
                ReceiveData(bytes, count);

            public void Header(ulong count) =>
                ReceiveContentLengthHeader(count);
        }
    }
}
