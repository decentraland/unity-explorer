using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading;
using TMPro;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.Fonts.Tests
{
    [TestFixture]
    public class SceneFontRequestShould
    {
        private const string OTHER_FILE_SRC = "fonts/Roboto.ttf";
        private const string FILE_SRC = "fonts/Lobster-Regular.ttf";

        private World world = null!;
        private ISceneData sceneData = null!;
        private SceneFontRequest request;
        private TMP_FontAsset? referenceFont;
        private FontData? fontData;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            sceneData = Substitute.For<ISceneData>();

            sceneData.TryGetContentUrl(FILE_SRC, out Arg.Any<URLAddress>())
                     .Returns(x =>
                      {
                          x[1] = URLAddress.FromString("https://peer.decentraland.org/content/contents/bafyfont");
                          return true;
                      });

            sceneData.TryGetContentUrl(OTHER_FILE_SRC, out Arg.Any<URLAddress>())
                     .Returns(x =>
                      {
                          x[1] = URLAddress.FromString("https://peer.decentraland.org/content/contents/otherfont");
                          return true;
                      });

            request = new SceneFontRequest();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();

            if (fontData != null)
                TestFonts.DestroyBundledFont(fontData);

            if (referenceFont != null)
                Object.DestroyImmediate(referenceFont);

            fontData = null;
            referenceFont = null;
        }

        [Test]
        public void CreateAPromiseWhenTheSourceResolves()
        {
            // Act
            bool updated = Update(OTHER_FILE_SRC);

            // Assert
            Assert.That(updated, Is.True);
            Assert.That(request.Src, Is.EqualTo(OTHER_FILE_SRC));
            Assert.That(request.Promise, Is.Not.Null);
            Entity promiseEntity = request.Promise!.Value.Entity;
            Assert.That(world.IsAlive(promiseEntity), Is.True);
            Assert.That(world.Get<GetFontIntention>(promiseEntity).Src, Is.EqualTo(OTHER_FILE_SRC));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void HoldNothingForAnEmptySource(string? fontSrc)
        {
            // Act
            bool updated = Update(fontSrc);

            // Assert
            Assert.That(updated, Is.False);
            Assert.That(request.Src, Is.Null);
            Assert.That(request.Promise, Is.Null);
        }

        [Test]
        public void TrimTheSource()
        {
            // Act
            bool updated = Update($"  {FILE_SRC} ");

            // Assert
            Assert.That(updated, Is.True);
            Assert.That(request.Src, Is.EqualTo(FILE_SRC));
            Assert.That(request.Promise, Is.Not.Null);
        }

        [Test]
        public void KeepThePromiseForTheSamePaddedSource()
        {
            // Arrange
            Update(FILE_SRC);
            Entity promiseEntity = request.Promise!.Value.Entity;

            // Act
            bool updated = Update($" {FILE_SRC}  ");

            // Assert
            Assert.That(updated, Is.False);
            Assert.That(request.Promise!.Value.Entity, Is.EqualTo(promiseEntity));
        }

        [Test]
        public void HoldNoPromiseWhenTheSourceDoesNotResolve()
        {
            // Act
            bool updated = Update("no such/file.ttf");

            // Assert
            Assert.That(updated, Is.True);
            Assert.That(request.Src, Is.EqualTo("no such/file.ttf"));
            Assert.That(request.Promise, Is.Null);
        }

        [Test]
        public void KeepThePromiseForTheSameSource()
        {
            // Arrange
            Update(OTHER_FILE_SRC);
            Entity promiseEntity = request.Promise!.Value.Entity;

            // Act
            bool updated = Update(OTHER_FILE_SRC);

            // Assert
            Assert.That(updated, Is.False);
            Assert.That(request.Promise!.Value.Entity, Is.EqualTo(promiseEntity));
        }

        [Test]
        public void ReplaceThePromiseWhenTheSourceChanges()
        {
            // Arrange
            Update(OTHER_FILE_SRC);
            Entity firstPromiseEntity = request.Promise!.Value.Entity;

            // Act
            bool updated = Update(FILE_SRC);

            // Assert
            Assert.That(updated, Is.True);
            Assert.That(world.IsAlive(firstPromiseEntity), Is.False);
            Assert.That(request.Src, Is.EqualTo(FILE_SRC));
            Assert.That(world.Get<GetFontIntention>(request.Promise!.Value.Entity).Src, Is.EqualTo(FILE_SRC));
        }

        [Test]
        public void ConsumeNothingWhileTheLoadIsPending()
        {
            // Arrange
            Update(OTHER_FILE_SRC);

            // Act
            bool consumed = request.TryConsume(world, out SceneFontAssets? _);

            // Assert
            Assert.That(consumed, Is.False);
        }

        [Test]
        public void ConsumeTheLoadedFontOnce()
        {
            // Arrange
            Update(OTHER_FILE_SRC);
            referenceFont = TestFonts.CreateTextMeshProFont();
            fontData = TestFonts.CreateBundledFont(referenceFont);
            ((IStreamableRefCountData)fontData).AddReference();
            world.Add(request.Promise!.Value.Entity, new StreamableLoadingResult<FontData>(fontData));

            // Act
            bool consumed = request.TryConsume(world, out SceneFontAssets? assets);
            bool consumedAgain = request.TryConsume(world, out SceneFontAssets? _);

            // Assert
            Assert.That(consumed, Is.True);
            Assert.That(assets, Is.SameAs(fontData.Asset));
            Assert.That(request.Promise!.Value.IsConsumed, Is.True);
            Assert.That(consumedAgain, Is.False);
        }

        [Test]
        public void ConsumeAFailedLoadAsNoFont()
        {
            // Arrange
            Update(OTHER_FILE_SRC);
            world.Add(request.Promise!.Value.Entity, new StreamableLoadingResult<FontData>(ReportData.UNSPECIFIED, new FontLoadException("test")));

            // Act
            bool consumed = request.TryConsume(world, out SceneFontAssets? assets);

            // Assert
            Assert.That(consumed, Is.True);
            Assert.That(assets, Is.Null);
        }

        [Test]
        public void CancelThePendingLoadOnRelease()
        {
            // Arrange
            Update(OTHER_FILE_SRC);
            Entity promiseEntity = request.Promise!.Value.Entity;
            CancellationToken ct = world.Get<GetFontIntention>(promiseEntity).CommonArguments.CancellationToken;

            // Act
            request.Release(world);

            // Assert
            Assert.That(request.Src, Is.Null);
            Assert.That(request.Promise, Is.Null);
            Assert.That(world.IsAlive(promiseEntity), Is.False);
            Assert.That(ct.IsCancellationRequested, Is.True);
        }

        [Test]
        public void StartOverAfterRelease()
        {
            // Arrange
            Update(OTHER_FILE_SRC);
            request.Release(world);

            // Act
            bool updated = Update(OTHER_FILE_SRC);

            // Assert
            Assert.That(updated, Is.True);
            Assert.That(request.Promise, Is.Not.Null);
        }

        [Test]
        public void DereferenceTheConsumedFontOnRelease()
        {
            // Arrange
            Update(OTHER_FILE_SRC);
            referenceFont = TestFonts.CreateTextMeshProFont();
            fontData = TestFonts.CreateBundledFont(referenceFont);
            ((IStreamableRefCountData)fontData).AddReference();
            world.Add(request.Promise!.Value.Entity, new StreamableLoadingResult<FontData>(fontData));
            request.TryConsume(world, out SceneFontAssets? _);

            // Act
            request.Release(world);

            // Assert
            Assert.That(fontData.CanBeDisposed(), Is.True);
        }

        [Test]
        public void DereferenceALoadedButUnconsumedFontOnRelease()
        {
            // Arrange
            Update(OTHER_FILE_SRC);
            referenceFont = TestFonts.CreateTextMeshProFont();
            fontData = TestFonts.CreateBundledFont(referenceFont);
            ((IStreamableRefCountData)fontData).AddReference();
            world.Add(request.Promise!.Value.Entity, new StreamableLoadingResult<FontData>(fontData));

            // Act
            request.Release(world);

            // Assert
            Assert.That(fontData.CanBeDisposed(), Is.True);
        }

        private bool Update(string? fontSrc) =>
            request.Update(world, sceneData, fontSrc, PartitionComponent.TOP_PRIORITY);
    }
}
