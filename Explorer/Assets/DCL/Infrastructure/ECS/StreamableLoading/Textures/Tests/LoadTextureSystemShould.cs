using DCL.WebRequests;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;

namespace ECS.StreamableLoading.Textures.Tests
{
    [TestFixture(WebRequestsMode.UNITY)]
    public class LoadTextureSystemShould : LoadSystemBaseShould<LoadTextureSystem, Texture2DData, GetTextureIntention>
    {
        public LoadTextureSystemShould(WebRequestsMode webRequestsMode) : base(webRequestsMode) { }

        private Uri successPath => new ($"file://{Application.dataPath + "/../TestResources/Images/alphaTexture.png"}");
        private Uri failPath => new ($"file://{Application.dataPath + "/../TestResources/Images/non_existing.png"}");
        private Uri wrongTypePath => new ($"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}");

        protected override GetTextureIntention CreateSuccessIntention() =>
            new (successPath.OriginalString, string.Empty, TextureWrapMode.MirrorOnce, FilterMode.Trilinear, TextureType.Albedo);

        protected override GetTextureIntention CreateNotFoundIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(failPath) };

        protected override GetTextureIntention CreateWrongTypeIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(wrongTypePath) };

        protected override LoadTextureSystem CreateSystem(IWebRequestController webRequestController) =>
            new (world, cache, webRequestController, IDiskCache<Texture2DData>.Null.INSTANCE, Substitute.For<IAvatarTextureUrlProvider>());

        protected override void AssertSuccess(Texture2DData data)
        {
            Texture2D asset = data.Asset;

            Assert.AreEqual(TextureWrapMode.MirrorOnce, asset.wrapMode);
            Assert.AreEqual(FilterMode.Trilinear, asset.filterMode);
        }
    }
}
