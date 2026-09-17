using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using ECS.TestSuite;
using NUnit.Framework;
using System;
using System.Collections;
using System.IO;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.Fonts.Tests
{
    [TestFixture]
    public class LoadFontSystemShould : LoadSystemBaseShould<LoadFontSystem, FontData, GetFontIntention>
    {
        private TMP_FontAsset? referenceFont;

        private string successPath => $"file://{TestFonts.PATH}";
        private string failPath => $"file://{Application.dataPath + "/DCL/SDKComponents/Fonts/non_existing.ttf"}";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";

        [UnityTest]
        public IEnumerator KeepTheSourceFileUntilNativeAssetsAreDestroyed() => UniTask.ToCoroutine(async () =>
        {
            var store = new FontFileStore(Path.Combine(Application.temporaryCachePath, "SceneFontsTests", Guid.NewGuid().ToString("N")));
            using FontFileStore.Lease file = await store.StoreAsync(File.ReadAllBytes(TestFonts.PATH), CancellationToken.None);
            await UniTask.SwitchToMainThread();
            FontFamilyAssets assets = new RuntimeFontAssetFactory(referenceFont!).Create("File lifetime", file.Path)!;
            var data = new FontData(assets, file);

            data.Dispose();

            Assert.That(File.Exists(file.Path), Is.True);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await UniTask.WaitUntil(() => !File.Exists(file.Path), cancellationToken: timeout.Token);
            Assert.That(assets.TextMeshProFont == null, Is.True);
            Assert.That(assets.UIToolkitFont == null, Is.True);
        });

        [TearDown]
        public void DestroyReferenceFont()
        {
            if (referenceFont != null)
                Object.DestroyImmediate(referenceFont);
        }

        protected override GetFontIntention CreateSuccessIntention() =>
            new ()
            {
                Src = "LiberationSans-Regular.ttf",
                CommonArguments = new CommonLoadingArguments(successPath),
            };

        protected override GetFontIntention CreateNotFoundIntention() =>
            new ()
            {
                Src = "non_existing.ttf",
                CommonArguments = new CommonLoadingArguments(failPath),
            };

        protected override GetFontIntention CreateWrongTypeIntention() =>
            new ()
            {
                Src = "arraybuffer.test",
                CommonArguments = new CommonLoadingArguments(wrongTypePath),
            };

        protected override LoadFontSystem CreateSystem()
        {
            TMP_FontAsset font = TestFonts.CreateTextMeshProFont();
            referenceFont = font;

            return new LoadFontSystem(world, cache, TestWebRequestController.INSTANCE, new RuntimeFontAssetFactory(font),
                new FontFileStore(Path.Combine(Application.temporaryCachePath, "SceneFontsTests")));
        }

        protected override void AssertSuccess(FontData data)
        {
            FontFamilyAssets assets = data.Asset;

            Assert.That(assets.TextMeshProFont, Is.Not.Null);
            Assert.That(assets.TextMeshProFont.faceInfo.familyName, Is.EqualTo("Liberation Sans"));
            Assert.That(assets.UIToolkitFont, Is.Not.Null);
            Assert.That(assets.UIToolkitFont.faceInfo.familyName, Is.EqualTo("Liberation Sans"));
        }
    }
}
