using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.AvatarRendering.AvatarShape.Tests.Instantiate
{
    public class AvatarInstantiatorAssetsShould
    {
        private const int TEST_RESOLUTION = 256;

        private static readonly int[] DEFAULT_RESOLUTIONS = { TEST_RESOLUTION };

        public static TextureArrayContainer NewTextureArrayContainer(Shader shader)
        {
            Texture texture_BC7 = new Texture2D(TEST_RESOLUTION, TEST_RESOLUTION,
                TextureArrayConstants.DEFAULT_BASEMAP_TEXTURE_FORMAT, false, false);
            Texture texture_BC5 = new Texture2D(TEST_RESOLUTION, TEST_RESOLUTION,
                TextureArrayConstants.DEFAULT_NORMALMAP_TEXTURE_FORMAT, false, false);

            var defaultTextures = new Dictionary<TextureArrayKey, Texture>
            {
                [new TextureArrayKey(TextureArrayConstants.MAINTEX_ARR_TEX_SHADER, TEST_RESOLUTION)] = texture_BC7,
                [new TextureArrayKey(TextureArrayConstants.NORMAL_MAP_TEX_ARR, TEST_RESOLUTION)] = texture_BC5,
                [new TextureArrayKey(TextureArrayConstants.EMISSIVE_MAP_TEX_ARR, TEST_RESOLUTION)] = texture_BC7
            };

            var textureArrayContainerFactory = new TextureArrayContainerFactory(defaultTextures);
            return textureArrayContainerFactory.Create(shader, DEFAULT_RESOLUTIONS);
        }

        [Test]
        public async Task CreateTextureArray()
        {
            Material? celShadingMaterial = await Addressables.LoadAssetAsync<Material>("Avatar_Toon_TestAsset");
            var _ = NewTextureArrayContainer(celShadingMaterial.shader!);
        }
    }
}
