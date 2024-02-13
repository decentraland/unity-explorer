using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using NUnit.Framework;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public abstract class TextureArrayShouldBase
    {
        private const int TEST_RESOLUTION = 256;

        private TextureArrayContainer textureArrayContainer;
        private Material testSourceMaterial;
        private Material testTargetMaterial;
        private Texture2D testTexture;

        protected abstract string targetShaderName { get; }

        [SetUp]
        public void SetUp()
        {
            var targetShader = Shader.Find(targetShaderName);
            textureArrayContainer = TextureArrayContainerFactory.Create(targetShader);

            testSourceMaterial = new Material(Shader.Find("DCL/Universal Render Pipeline/Lit"));

            foreach (TextureArrayMapping textureArrayMapping in textureArrayContainer.mappings)
            {
                testSourceMaterial.SetTexture(textureArrayMapping.OriginalTextureID,
                    new Texture2D(TEST_RESOLUTION, TEST_RESOLUTION, TextureArrayConstants.DEFAULT_TEXTURE_FORMAT, false, false));
            }

            testTargetMaterial = new Material(targetShader);
        }

        [Test]
        public void SetTexture()
        {
            TextureArraySlot?[] textureArraySlots = textureArrayContainer.SetTexturesFromOriginalMaterial(testSourceMaterial, testTargetMaterial);

            for (var i = 0; i < textureArraySlots.Length && i < textureArrayContainer.count; i++)
            {
                TextureArraySlot? slot = textureArraySlots[i];

                Assert.That(slot, Is.Not.Null);

                TextureArraySlot slotVal = slot.Value;

                Assert.AreEqual(slotVal.TextureArrayResolution, textureArrayContainer.mappings[i].Handler.resolutionDictionary[TEST_RESOLUTION]);
                Assert.AreEqual(slotVal.UsedSlotIndex, 0);

                Assert.AreEqual(slotVal.TextureArray,
                    textureArrayContainer.mappings[i].Handler.resolutionDictionary[TEST_RESOLUTION].arrays[0]);
            }
        }

        [Test]
        public void ReleaseAndReuseTexture()
        {
            TextureArraySlot?[] originalSlots = textureArrayContainer.SetTexturesFromOriginalMaterial(testSourceMaterial, testTargetMaterial);

            for (var i = 0; i < originalSlots.Length && i < textureArrayContainer.count; i++)
            {
                TextureArraySlot? slot = originalSlots[i];

                Assert.That(slot, Is.Not.Null);

                slot.Value.FreeSlot();

                Assert.AreEqual(textureArrayContainer.mappings[i].Handler.resolutionDictionary[TEST_RESOLUTION].freeSlots.Count, 1);
            }

            TextureArraySlot?[] replacedSlots = textureArrayContainer.SetTexturesFromOriginalMaterial(testSourceMaterial, testTargetMaterial);

            // Check the slots are the same as the original ones
            for (var i = 0; i < originalSlots.Length && i < textureArrayContainer.count; i++)
                Assert.AreEqual(originalSlots[i], replacedSlots[i]);
        }
    }
}
