using Arch.Core;
using ECS.StreamableLoading.Components;
using ECS.StreamableLoading.Components.Common;
using ECS.StreamableLoading.Systems;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Tests
{
    [TestFixture]
    public class ConcludeTextureLoadingSystemShould : ConcludeLoadingSystemBaseShould<ConcludeTextureLoadingSystem, Texture2D, GetTextureIntention>
    {
        private string successPath => $"file://{Application.dataPath + "/../TestResources/Images/alphaTexture.png"}";
        private string failPath => $"file://{Application.dataPath + "/../TestResources/Images/non_existing.png"}";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";

        protected override ConcludeTextureLoadingSystem CreateSystem() =>
            new (world);

        protected override Entity CreateSuccessIntention()
        {
            var lr = new LoadingRequest { WebRequest = UnityWebRequestTexture.GetTexture(successPath) };
            lr.WebRequest.SendWebRequest();

            return world.Create(new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(successPath),
                WrapMode = TextureWrapMode.MirrorOnce,
                FilterMode = FilterMode.Trilinear,
            }, lr);
        }

        protected override void AssertSuccess(Texture2D asset)
        {
            Assert.AreEqual(TextureWrapMode.MirrorOnce, asset.wrapMode);
            Assert.AreEqual(FilterMode.Trilinear, asset.filterMode);
        }

        protected override Entity CreateNotFoundIntention()
        {
            var lr = new LoadingRequest { WebRequest = UnityWebRequestTexture.GetTexture(failPath) };
            lr.WebRequest.SendWebRequest();
            return world.Create(new GetTextureIntention { CommonArguments = new CommonLoadingArguments(failPath) }, lr);
        }

        protected override Entity CreateWrongTypeIntention()
        {
            var lr = new LoadingRequest { WebRequest = UnityWebRequestTexture.GetTexture(wrongTypePath) };
            lr.WebRequest.SendWebRequest();
            return world.Create(new GetTextureIntention { CommonArguments = new CommonLoadingArguments(wrongTypePath) }, lr);
        }
    }
}
