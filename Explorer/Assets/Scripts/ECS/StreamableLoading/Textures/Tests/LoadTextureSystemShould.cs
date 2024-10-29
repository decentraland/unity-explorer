using DCL.WebRequests.ArgsFactory;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using ECS.TestSuite;
using NUnit.Framework;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using UnityEngine;

namespace ECS.StreamableLoading.Textures.Tests
{
    [TestFixture]
    public class LoadTextureSystemShould : LoadSystemBaseShould<LoadTextureSystem, Texture2DData, GetTextureIntention>
    {
        private string successPath => $"file://{Application.dataPath + "/../TestResources/Images/alphaTexture.png"}";
        private string failPath => $"file://{Application.dataPath + "/../TestResources/Images/non_existing.png"}";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";

        protected override GetTextureIntention CreateSuccessIntention() =>
            new (successPath, string.Empty, TextureWrapMode.MirrorOnce, FilterMode.Trilinear, TextureType.Albedo);

        protected override GetTextureIntention CreateNotFoundIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(failPath) };

        protected override GetTextureIntention CreateWrongTypeIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(wrongTypePath) };

        protected override LoadTextureSystem CreateSystem() =>
            new (world, cache, TestWebRequestController.INSTANCE, new GetTextureArgsFactory(ITexturesUnzip.NewTestInstance()));

        protected override void AssertSuccess(Texture2DData data)
        {
            Texture2D asset = data.Asset;

            Assert.AreEqual(TextureWrapMode.MirrorOnce, asset.wrapMode);
            Assert.AreEqual(FilterMode.Trilinear, asset.filterMode);
        }
    }
}
