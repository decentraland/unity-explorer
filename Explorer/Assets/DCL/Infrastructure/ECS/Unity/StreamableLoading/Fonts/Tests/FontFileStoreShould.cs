using NUnit.Framework;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ECS.StreamableLoading.Fonts.Tests
{
    [TestFixture]
    public class FontFileStoreShould
    {
        private static readonly byte[] BYTES = { 1, 2, 3 };
        private static readonly byte[] OTHER_BYTES = { 4, 5, 6 };

        private string directory = null!;
        private FontFileStore store = null!;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Application.temporaryCachePath, nameof(FontFileStoreShould), Guid.NewGuid().ToString("N"));
            store = new FontFileStore(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        [TestCase(new byte[] { 0x00, 0x01, 0x00, 0x00, 0, 0, 0, 0, 0, 0, 0, 0 }, true)]
        [TestCase(new byte[] { 0x4F, 0x54, 0x54, 0x4F, 0, 0, 0, 0, 0, 0, 0, 0 }, true)]
        [TestCase(new byte[] { 0x74, 0x72, 0x75, 0x65, 0, 0, 0, 0, 0, 0, 0, 0 }, true)]
        [TestCase(new byte[] { 0x74, 0x74, 0x63, 0x66, 0, 0, 0, 0, 0, 0, 0, 0 }, true)]
        [TestCase(new byte[] { 0x00, 0x01, 0x00 }, false)]
        [TestCase(new byte[] { 0x77, 0x4F, 0x46, 0x46, 0, 0, 0, 0, 0, 0, 0, 0 }, false)]
        public void RecognizeSfntHeaders(byte[] bytes, bool expected)
        {
            bool looksLikeFont = FontFileStore.LooksLikeFontFile(bytes);

            Assert.That(looksLikeFont, Is.EqualTo(expected));
        }

        [Test]
        public void RejectAnHtmlErrorPage()
        {
            bool looksLikeFont = FontFileStore.LooksLikeFontFile(Encoding.ASCII.GetBytes("<!DOCTYPE html><html>"));

            Assert.That(looksLikeFont, Is.False);
        }

        [Test]
        public async Task StoreTheBytesUnderTheirHash()
        {
            string path = await store.StoreAsync(BYTES, CancellationToken.None);

            Assert.That(Path.GetDirectoryName(path), Is.EqualTo(directory));
            Assert.That(Path.GetExtension(path), Is.EqualTo(".ttf"));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(BYTES));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        }

        [Test]
        public async Task ReuseTheFileOfTheSameBytes()
        {
            string first = await store.StoreAsync(BYTES, CancellationToken.None);

            string second = await store.StoreAsync(BYTES, CancellationToken.None);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        }

        [Test]
        public async Task KeepDifferentBytesInDifferentFiles()
        {
            string first = await store.StoreAsync(BYTES, CancellationToken.None);

            string second = await store.StoreAsync(OTHER_BYTES, CancellationToken.None);

            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(File.ReadAllBytes(second), Is.EqualTo(OTHER_BYTES));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(2));
        }

        [Test]
        public void WriteNothingWhenCancelled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.CatchAsync<OperationCanceledException>(async () => await store.StoreAsync(BYTES, cts.Token));
            Assert.That(Directory.Exists(directory), Is.False);
        }

        [Test]
        public async Task RemoveEveryFileOnClear()
        {
            await store.StoreAsync(BYTES, CancellationToken.None);

            store.Clear();

            Assert.That(Directory.Exists(directory), Is.False);
        }

        [Test]
        public async Task StoreAgainAfterClear()
        {
            string first = await store.StoreAsync(BYTES, CancellationToken.None);
            store.Clear();

            string second = await store.StoreAsync(BYTES, CancellationToken.None);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(File.ReadAllBytes(second), Is.EqualTo(BYTES));
        }
    }
}
