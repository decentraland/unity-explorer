using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.StreamableLoading.Tests;
using NUnit.Framework;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.StreamableLoading.Textures.Tests
{
    [TestFixture]
    public class LoadTextureSystemShould : LoadSystemBaseShould<LoadTextureSystem, Texture2D, GetTextureIntention>
    {
        private string successPath => $"file://{Application.dataPath + "/../TestResources/Images/alphaTexture.png"}";
        private string failPath => $"file://{Application.dataPath + "/../TestResources/Images/non_existing.png"}";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";

        protected override GetTextureIntention CreateSuccessIntention() =>
            new ()
            {
                CommonArguments = new CommonLoadingArguments(successPath, deferredLoadingState: DeferredLoadingState.Allowed),
                WrapMode = TextureWrapMode.MirrorOnce,
                FilterMode = FilterMode.Trilinear,
            };

        protected override GetTextureIntention CreateNotFoundIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(failPath, deferredLoadingState: DeferredLoadingState.Allowed) };

        protected override GetTextureIntention CreateWrongTypeIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(wrongTypePath, deferredLoadingState: DeferredLoadingState.Allowed) };

        protected override LoadTextureSystem CreateSystem() =>
            new (world, cache, new MutexSync(), new NullBudgetProvider());

        protected override void AssertSuccess(Texture2D asset)
        {
            Assert.AreEqual(TextureWrapMode.MirrorOnce, asset.wrapMode);
            Assert.AreEqual(FilterMode.Trilinear, asset.filterMode);
        }
    }
}
