using Cysharp.Threading.Tasks;
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
        [TestCase(new byte[] { 0x4F, 0x54, 0x54, 0x4F, 0, 0, 0, 0, 0, 0, 0, 0 }, false)]
        [TestCase(new byte[] { 0x74, 0x72, 0x75, 0x65, 0, 0, 0, 0, 0, 0, 0, 0 }, false)]
        [TestCase(new byte[] { 0x74, 0x74, 0x63, 0x66, 0, 0, 0, 0, 0, 0, 0, 0 }, false)]
        [TestCase(new byte[] { 0x00, 0x01, 0x00 }, false)]
        [TestCase(new byte[] { 0x77, 0x4F, 0x46, 0x46, 0, 0, 0, 0, 0, 0, 0, 0 }, false)]
        [TestCase(new byte[] { 0x77, 0x4F, 0x46, 0x32, 0, 0, 0, 0, 0, 0, 0, 0 }, false)]
        public void AcceptOnlyTrueTypeHeaders(byte[] bytes, bool expected)
        {
            bool looksLikeFont = FontFileStore.LooksLikeTrueTypeFont(bytes);

            Assert.That(looksLikeFont, Is.EqualTo(expected));
        }

        [Test]
        public void RejectAnOpenTypeFont()
        {
            string path = Path.Combine(Application.dataPath, "TextMesh Pro/Fonts & Materials/Asian fallbacks/NotoSansJP-SemiBold.otf");

            Assert.That(FontFileStore.LooksLikeTrueTypeFont(File.ReadAllBytes(path)), Is.False);
        }

        [Test]
        public void RejectAnHtmlErrorPage()
        {
            bool looksLikeFont = FontFileStore.LooksLikeTrueTypeFont(Encoding.ASCII.GetBytes("<!DOCTYPE html><html>"));

            Assert.That(looksLikeFont, Is.False);
        }

        [Test]
        public async Task StoreTheBytesUnderTheirHash()
        {
            using FontFileStore.Lease lease = await store.StoreAsync(BYTES, CancellationToken.None);
            string path = lease.Path;

            Assert.That(Path.GetDirectoryName(Path.GetFullPath(path)), Is.EqualTo(Path.GetFullPath(directory)));
            Assert.That(Path.GetExtension(path), Is.EqualTo(".ttf"));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(BYTES));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        }

        [Test]
        public async Task ReuseTheFileOfTheSameBytes()
        {
            using FontFileStore.Lease first = await store.StoreAsync(BYTES, CancellationToken.None);

            using FontFileStore.Lease second = await store.StoreAsync(BYTES, CancellationToken.None);

            Assert.That(second.Path, Is.EqualTo(first.Path));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        }

        [Test]
        public async Task KeepDifferentBytesInDifferentFiles()
        {
            using FontFileStore.Lease first = await store.StoreAsync(BYTES, CancellationToken.None);

            using FontFileStore.Lease second = await store.StoreAsync(OTHER_BYTES, CancellationToken.None);

            Assert.That(second.Path, Is.Not.EqualTo(first.Path));
            Assert.That(File.ReadAllBytes(second.Path), Is.EqualTo(OTHER_BYTES));
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
        public async Task KeepTheFileUntilItsLastOwnerReleasesIt()
        {
            using FontFileStore.Lease first = await store.StoreAsync(BYTES, CancellationToken.None);
            using FontFileStore.Lease second = await store.StoreAsync(BYTES, CancellationToken.None);

            first.Dispose();
            first.Dispose();

            Assert.That(File.Exists(second.Path), Is.True);
            second.Dispose();
            Assert.That(File.Exists(second.Path), Is.False);
        }

        [Test]
        public async Task ShareOwnershipAcrossConcurrentWrites()
        {
            FontFileStore.Lease[] leases = await Task.WhenAll(
                store.StoreAsync(BYTES, CancellationToken.None).AsTask(),
                store.StoreAsync(BYTES, CancellationToken.None).AsTask());

            try
            {
                Assert.That(leases[0].Path, Is.EqualTo(leases[1].Path));
                leases[0].Dispose();
                Assert.That(File.ReadAllBytes(leases[1].Path), Is.EqualTo(BYTES));
            }
            finally
            {
                foreach (FontFileStore.Lease lease in leases)
                    lease.Dispose();
            }

            Assert.That(File.Exists(leases[0].Path), Is.False);
        }

        [Test]
        public async Task RemoveEveryFileOnClear()
        {
            using FontFileStore.Lease lease = await store.StoreAsync(BYTES, CancellationToken.None);
            lease.Dispose();
            File.WriteAllBytes(Path.Combine(directory, "abandoned.tmp"), BYTES);
            store.Clear();

            Assert.That(Directory.Exists(directory), Is.False);
        }

        [Test]
        public async Task StoreAgainAfterClear()
        {
            using FontFileStore.Lease first = await store.StoreAsync(BYTES, CancellationToken.None);
            first.Dispose();
            store.Clear();

            using FontFileStore.Lease second = await store.StoreAsync(BYTES, CancellationToken.None);

            Assert.That(second.Path, Is.EqualTo(first.Path));
            Assert.That(File.ReadAllBytes(second.Path), Is.EqualTo(BYTES));
        }
    }
}
