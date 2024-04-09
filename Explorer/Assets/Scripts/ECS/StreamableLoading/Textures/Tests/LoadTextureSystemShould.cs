using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.StreamableLoading.Textures.Tests
{

    public class LoadTextureSystemShould : LoadSystemBaseShould<LoadTextureSystem, Texture2D, GetTextureIntention>
    {
        private string successPath => $"file://{Application.dataPath + "/../TestResources/Images/alphaTexture.png"}";
        private string failPath => $"file://{Application.dataPath + "/../TestResources/Images/non_existing.png"}";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";

        protected override GetTextureIntention CreateSuccessIntention() =>
            new ()
            {
                CommonArguments = new CommonLoadingArguments(successPath),
                WrapMode = TextureWrapMode.MirrorOnce,
                FilterMode = FilterMode.Trilinear,
            };

        protected override GetTextureIntention CreateNotFoundIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(failPath) };

        protected override GetTextureIntention CreateWrongTypeIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(wrongTypePath) };

        protected override LoadTextureSystem CreateSystem() =>
            new (world, cache, TestSuite.TestWebRequestController.INSTANCE, new MutexSync());

        protected override void AssertSuccess(Texture2D asset)
        {
            Assert.AreEqual(TextureWrapMode.MirrorOnce, asset.wrapMode);
            Assert.AreEqual(FilterMode.Trilinear, asset.filterMode);
        }
    }
}
