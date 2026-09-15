using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine.TextCore.Text;

namespace ECS.StreamableLoading.Fonts.Tests
{
    [TestFixture]
    public class SceneFontRequestShould
    {
        private const string FAMILY_SRC = "Roboto";
        private const string FILE_SRC = "fonts/Lobster-Regular.ttf";

        private World world = null!;
        private ISceneData sceneData = null!;
        private SceneFontRequest request;
        private FontFamilyAssets? family;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            sceneData = Substitute.For<ISceneData>();

            sceneData.TryGetMediaUrl(FILE_SRC, out Arg.Any<URLAddress>())
                     .Returns(x =>
                      {
                          x[1] = URLAddress.FromString("https://peer.decentraland.org/content/contents/bafyfont");
                          return true;
                      });

            request = new SceneFontRequest();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
            family?.Destroy();
        }

        [Test]
        public void CreateAPromiseWhenTheSourceResolves()
        {
            bool updated = Update(FAMILY_SRC);

            Assert.That(updated, Is.True);
            Assert.That(request.Src, Is.EqualTo(FAMILY_SRC));
            Assert.That(request.Promise, Is.Not.Null);
            Entity promiseEntity = request.Promise!.Value.Entity;
            Assert.That(world.IsAlive(promiseEntity), Is.True);
            Assert.That(world.Get<GetFontIntention>(promiseEntity).Kind, Is.EqualTo(FontSourceKind.FontsourceFamily));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void HoldNothingForAnEmptySource(string? fontSrc)
        {
            bool updated = Update(fontSrc);

            Assert.That(updated, Is.False);
            Assert.That(request.Src, Is.Null);
            Assert.That(request.Promise, Is.Null);
        }

        [Test]
        public void TrimTheSource()
        {
            bool updated = Update($"  {FILE_SRC} ");

            Assert.That(updated, Is.True);
            Assert.That(request.Src, Is.EqualTo(FILE_SRC));
            Assert.That(request.Promise, Is.Not.Null);
        }

        [Test]
        public void KeepThePromiseForTheSamePaddedSource()
        {
            Update(FILE_SRC);
            Entity promiseEntity = request.Promise!.Value.Entity;

            bool updated = Update($" {FILE_SRC}  ");

            Assert.That(updated, Is.False);
            Assert.That(request.Promise!.Value.Entity, Is.EqualTo(promiseEntity));
        }

        [Test]
        public void HoldNoPromiseWhenTheSourceDoesNotResolve()
        {
            bool updated = Update("no such/file.ttf");

            Assert.That(updated, Is.True);
            Assert.That(request.Src, Is.EqualTo("no such/file.ttf"));
            Assert.That(request.Promise, Is.Null);
        }

        [Test]
        public void KeepThePromiseForTheSameSource()
        {
            Update(FAMILY_SRC);
            Entity promiseEntity = request.Promise!.Value.Entity;

            bool updated = Update(FAMILY_SRC);

            Assert.That(updated, Is.False);
            Assert.That(request.Promise!.Value.Entity, Is.EqualTo(promiseEntity));
        }

        [Test]
        public void ReplaceThePromiseWhenTheSourceChanges()
        {
            Update(FAMILY_SRC);
            Entity firstPromiseEntity = request.Promise!.Value.Entity;

            bool updated = Update(FILE_SRC);

            Assert.That(updated, Is.True);
            Assert.That(world.IsAlive(firstPromiseEntity), Is.False);
            Assert.That(request.Src, Is.EqualTo(FILE_SRC));
            Assert.That(world.Get<GetFontIntention>(request.Promise!.Value.Entity).Kind, Is.EqualTo(FontSourceKind.File));
        }

        [Test]
        public void ConsumeNothingWhileTheLoadIsPending()
        {
            Update(FAMILY_SRC);

            bool consumed = request.TryConsume(world, out FontFamilyAssets? _);

            Assert.That(consumed, Is.False);
        }

        [Test]
        public void ConsumeTheLoadedFamilyOnce()
        {
            Update(FAMILY_SRC);
            family = CreateFamily();
            var data = new FontData(family);
            ((IStreamableRefCountData)data).AddReference();
            world.Add(request.Promise!.Value.Entity, new StreamableLoadingResult<FontData>(data));

            bool consumed = request.TryConsume(world, out FontFamilyAssets? assets);
            bool consumedAgain = request.TryConsume(world, out FontFamilyAssets? _);

            Assert.That(consumed, Is.True);
            Assert.That(assets, Is.SameAs(family));
            Assert.That(request.Promise!.Value.IsConsumed, Is.True);
            Assert.That(consumedAgain, Is.False);
        }

        [Test]
        public void ConsumeAFailedLoadAsNoFamily()
        {
            Update(FAMILY_SRC);
            world.Add(request.Promise!.Value.Entity, new StreamableLoadingResult<FontData>(ReportData.UNSPECIFIED, new FontLoadException("test")));

            bool consumed = request.TryConsume(world, out FontFamilyAssets? assets);

            Assert.That(consumed, Is.True);
            Assert.That(assets, Is.Null);
        }

        [Test]
        public void CancelThePendingLoadOnRelease()
        {
            Update(FAMILY_SRC);
            Entity promiseEntity = request.Promise!.Value.Entity;
            CancellationToken ct = world.Get<GetFontIntention>(promiseEntity).CommonArguments.CancellationToken;

            request.Release(world);

            Assert.That(request.Src, Is.Null);
            Assert.That(request.Promise, Is.Null);
            Assert.That(world.IsAlive(promiseEntity), Is.False);
            Assert.That(ct.IsCancellationRequested, Is.True);
        }

        [Test]
        public void StartOverAfterRelease()
        {
            Update(FAMILY_SRC);
            request.Release(world);

            bool updated = Update(FAMILY_SRC);

            Assert.That(updated, Is.True);
            Assert.That(request.Promise, Is.Not.Null);
        }

        private bool Update(string? fontSrc) =>
            request.Update(world, sceneData, fontSrc, PartitionComponent.TOP_PRIORITY);

        private static FontFamilyAssets CreateFamily()
        {
            TMP_FontAsset textMeshProFont = TestFonts.CreateTextMeshProFont();
            FontAsset uiToolkitFont = TestFonts.CreateUIToolkitFont();

            return new FontFamilyAssets(textMeshProFont, uiToolkitFont, new List<TMP_FontAsset> { textMeshProFont }, new List<FontAsset> { uiToolkitFont });
        }
    }
}
