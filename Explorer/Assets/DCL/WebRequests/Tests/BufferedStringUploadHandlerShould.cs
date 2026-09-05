using NUnit.Framework;
using System;
using UnityEngine.Networking;

namespace DCL.WebRequests.Tests
{
    public class BufferedStringUploadHandlerShould
    {
        private const string TEST_VALUE = nameof(BufferedStringUploadHandlerShould);

        [Test]
        public void CreateStringBeforeHandlerIsCreated()
        {
            using var buffer = new BufferedStringUploadHandler(50);
            buffer.WriteString(TEST_VALUE);

            Assert.That(buffer.ToString(), Is.EqualTo(TEST_VALUE));
        }

        [Test]
        public void CreateStringAfterHandlerIsCreated()
        {
            var buffer = new BufferedStringUploadHandler(50);
            buffer.WriteString(TEST_VALUE);

            using UploadHandlerRaw _ = buffer.CreateUploadHandler();

            Assert.That(buffer.ToString(), Is.EqualTo(TEST_VALUE));
        }

        [Test]
        public void ThrowIfBufferIsNotCreated()
        {
            using var buffer = default(BufferedStringUploadHandler);

            Assert.Throws<ObjectDisposedException>(() => buffer.ToString());
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void ThrowIfUploadHandlerIsDisposed()
        {
            var buffer = new BufferedStringUploadHandler(50);
            buffer.WriteString(TEST_VALUE);

            UploadHandlerRaw uploadHandler = buffer.CreateUploadHandler();
            uploadHandler.Dispose();

            Assert.Throws<ObjectDisposedException>(() => buffer.ToString());
        }
#endif
    }
}
