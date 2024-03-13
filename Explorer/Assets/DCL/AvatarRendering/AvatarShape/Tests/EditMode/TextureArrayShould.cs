using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using NUnit.Framework;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class TextureArrayShould
    {
        private TextureArrayContainer textureArrayContainer;
        private Material testMaterial;
        private Texture2D testTexture;
        private int testResolution = 256;

        [SetUp]
        public void SetUp()
        {
            textureArrayContainer = new TextureArrayContainer(TextureFormat.BC7);
            testResolution = 256;
            testTexture = new Texture2D(testResolution, testResolution, TextureArrayConstants.DEFAULT_TEXTURE_FORMAT, false, false);
            testMaterial = new Material(Shader.Find("Standard"));
        }

        [Test]
        public void SetTexture()
        {
            TextureArraySlot textureArraySlot = textureArrayContainer.SetTexture(testMaterial, testTexture, ComputeShaderConstants.TextureArrayType.ALBEDO);

            var usedSlotIndex = 0;

            Assert.AreEqual(textureArraySlot.TextureArrayResolution, textureArrayContainer.textureArrayTypes[(int)ComputeShaderConstants.TextureArrayType.ALBEDO].resolutionDictionary[testResolution]);

            Assert.AreEqual(textureArraySlot.TextureArray,
                textureArrayContainer.textureArrayTypes[(int)ComputeShaderConstants.TextureArrayType.ALBEDO].resolutionDictionary[testResolution].arrays[usedSlotIndex]);

            Assert.AreEqual(textureArraySlot.UsedSlotIndex, usedSlotIndex);
        }

        [Test]
        public void ReleaseAndReuseTexture()
        {
            TextureArraySlot textureArraySlotOriginal = textureArrayContainer.SetTexture(testMaterial, testTexture, ComputeShaderConstants.TextureArrayType.ALBEDO);
            textureArraySlotOriginal.FreeSlot();
            Assert.AreEqual(textureArrayContainer.textureArrayTypes[(int)ComputeShaderConstants.TextureArrayType.ALBEDO].resolutionDictionary[testResolution].freeSlots.Count, 1);

            TextureArraySlot textureArraySlotReplacement = textureArrayContainer.SetTexture(testMaterial, testTexture, ComputeShaderConstants.TextureArrayType.ALBEDO);
            Assert.AreEqual(textureArraySlotOriginal, textureArraySlotReplacement);
        }
    }
}
