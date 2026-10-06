using DCL.Diagnostics.Tests;
using DCL.Ipfs;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;
using TMPro;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.Fonts.Tests
{
    [TestFixture]
    public class LoadFontSystemShould : UnitySystemTestBase<LoadFontSystem>
    {
        private const string FONT_SRC = "fonts/Lobster-Regular.ttf";
        private const string CONTENT_URL = "https://peer.decentraland.org/content/contents/bafyfont";

        private TMP_FontAsset referenceFont = null!;
        private FontsCache cache = null!;
        private MockedReportScope mockedReportScope = null!;

        [SetUp]
        public void SetUp()
        {
            mockedReportScope = new MockedReportScope();
            referenceFont = TestFonts.CreateTextMeshProFont();
            cache = new FontsCache();
            system = new LoadFontSystem(world, cache, new SceneFontAssetsFactory(referenceFont), tryUnlistedBundles: false);
            system.Initialize();
        }

        protected override void OnTearDown()
        {
            cache.Dispose();
            Object.DestroyImmediate(referenceFont);
            mockedReportScope.Dispose();
        }

        [Test]
        public async Task FailWhenTheSceneHasNoConvertedBundles()
        {
            // Arrange
            var intention = new GetFontIntention { Src = FONT_SRC, CommonArguments = new CommonLoadingArguments(CONTENT_URL) };

            // Act
            StreamableLoadingResult<FontData> result = await LoadAsync(intention);

            // Assert
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Exception, Is.TypeOf<FontLoadException>());
        }

        [Test]
        public async Task FailWhenTheManifestListsNoBundleForTheFont()
        {
            // Arrange
            var intention = new GetFontIntention
            {
                Src = FONT_SRC,
                CommonArguments = new CommonLoadingArguments(CONTENT_URL),
                Bundle = new ConvertedFontBundle("bafyfont", listed: false, AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01"), "scene"),
            };

            // Act
            StreamableLoadingResult<FontData> result = await LoadAsync(intention);

            // Assert
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Exception, Is.TypeOf<FontLoadException>());
        }

        private async Task<StreamableLoadingResult<FontData>> LoadAsync(GetFontIntention intention)
        {
            var promise = AssetPromise<FontData, GetFontIntention>.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            world.Get<StreamableLoadingState>(promise.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());

            system.Update(0);

            promise = await promise.ToUniTaskAsync(world, cancellationToken: intention.CommonArguments.CancellationToken);
            Assert.That(promise.TryGetResult(world, out StreamableLoadingResult<FontData> result), Is.True);
            return result;
        }
    }
}
