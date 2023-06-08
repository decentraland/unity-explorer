using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using NUnit.Framework;
using UnityEngine;

namespace ECS.StreamableLoading.Textures.Tests
{
    [TestFixture]
    public class StartLoadingTextureSystemShould : StartLoadingSystemBaseShould<StartLoadingTextureSystem, GetTextureIntention>
    {
        private string path => $"file://{Application.dataPath + "/../TestResources/Images/alphaTexture.png"}";

        protected override GetTextureIntention CreateIntention() =>
            new ()
            {
                CommonArguments = new CommonLoadingArguments
                    { Attempts = 1, Timeout = 60, URL = path },
                IsReadable = false,
            };

        protected override StartLoadingTextureSystem CreateSystem() =>
            new (world);
    }
}
