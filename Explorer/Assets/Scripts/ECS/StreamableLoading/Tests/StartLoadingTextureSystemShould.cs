using ECS.StreamableLoading.Components;
using ECS.StreamableLoading.Components.Common;
using ECS.StreamableLoading.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ECS.StreamableLoading.Tests
{
    [TestFixture]
    public class StartLoadingTextureSystemShould : StartLoadingSystemBaseShould<StartLoadingTextureSystem, Texture2D, GetTextureIntention>
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
