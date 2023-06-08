using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using NUnit.Framework;
using UnityEngine;

namespace ECS.StreamableLoading.Tests
{
    [TestFixture]
    public class RepeatTextureLoadingSystemShould : RepeatLoadingSystemBaseShould<RepeatTextureLoadingSystem, Texture2D, GetTextureIntention>
    {
        private string path => $"file://{Application.dataPath + "/../TestResources/Images/alphaTexture.png"}";

        protected override GetTextureIntention CreateIntention() =>
            new ()
            {
                CommonArguments = new CommonLoadingArguments
                    { Attempts = 1, Timeout = 60, URL = path },
                IsReadable = false,
            };

        protected override RepeatTextureLoadingSystem CreateSystem() =>
            new (world);
    }
}
