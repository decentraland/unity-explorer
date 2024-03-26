using System.Collections.Generic;
using System.Net.Mime;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using NUnit.Framework;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public abstract class TextureArrayShouldBase
    {
        private const int TEST_RESOLUTION = TextureArrayConstants.MAIN_TEXTURE_RESOLUTION;

        private static readonly int[] DEFAULT_RESOLUTIONS = { TEST_RESOLUTION };

        private TextureArrayContainer textureArrayContainer;
        private Material testSourceMaterial;
        private Material testTargetMaterial;
        private Texture2D testTexture;

        protected abstract string targetShaderName { get; }

        [SetUp]
        public void SetUp()
        {
            var targetShader = Shader.Find(targetShaderName);
            Texture texture = new Texture2D(TEST_RESOLUTION, TEST_RESOLUTION, TextureArrayConstants.DEFAULT_BASEMAP_TEXTURE_FORMAT, false, false);

            var defaultTextures = new Dictionary<TextureArrayKey, Texture>
            {
                [new TextureArrayKey(TextureArrayConstants.MAINTEX_ARR_TEX_SHADER, TEST_RESOLUTION)] = texture,
                [new TextureArrayKey(TextureArrayConstants.BASE_MAP_TEX_ARR, TEST_RESOLUTION)] = texture,
                [new TextureArrayKey(TextureArrayConstants.NORMAL_MAP_TEX_ARR, TEST_RESOLUTION)] = texture,
                [new TextureArrayKey(TextureArrayConstants.EMISSIVE_MAP_TEX_ARR, TEST_RESOLUTION)] = texture
            };
            var factory = new TextureArrayContainerFactory();
            factory.SetDefaultTextures(defaultTextures);
            textureArrayContainer = factory.Create(targetShader, DEFAULT_RESOLUTIONS);
            testSourceMaterial = new Material(Shader.Find("DCL/Universal Render Pipeline/Lit"));

            foreach (TextureArrayMapping textureArrayMapping in textureArrayContainer.mappings)
                testSourceMaterial.SetTexture(textureArrayMapping.OriginalTextureID, texture);

            testTargetMaterial = new Material(targetShader);
        }

        [Test]
        public void SetDefaultTexture()
        {
            // We recreate the material with no texture so the default one is applied
            testSourceMaterial = new Material(Shader.Find("DCL/Universal Render Pipeline/Lit"));
            var textureArraySlots = textureArrayContainer.SetTexturesFromOriginalMaterial(testSourceMaterial, testTargetMaterial);

            for (int i = 0; i < textureArraySlots.Length && i < textureArrayContainer.count; i++)
            {
                var slot = textureArraySlots[i];

                //We dont want to have a reference for the slot since we dont want to release it after
                Assert.That(slot, Is.Null);
            }

            foreach (var textureArrayMapping in textureArrayContainer.mappings)
            {
                Assert.AreEqual(TextureArrayHandler.DEFAULT_SLOT_INDEX, testTargetMaterial.GetInteger(textureArrayMapping.Handler.arrayID));
                Assert.AreEqual(textureArrayMapping.Handler.GetDefaultTextureArray(TEST_RESOLUTION), testTargetMaterial.GetTexture(textureArrayMapping.Handler.textureID));
            }
        }

        [Test]
        public void SetTexture()
        {
            var textureArraySlots = textureArrayContainer.SetTexturesFromOriginalMaterial(testSourceMaterial, testTargetMaterial);

            for (var i = 0; i < textureArraySlots.Length && i < textureArrayContainer.count; i++)
            {
                TextureArraySlot? slot = textureArraySlots[i];

                Assert.That(slot, Is.Not.Null);

                TextureArraySlot slotVal = slot.Value;

                Assert.AreEqual(slotVal.UsedSlotIndex, 1);
                Assert.AreEqual(slotVal.TextureArray, textureArrayContainer.mappings[i].Handler.GetOrCreateSlotHandler(TEST_RESOLUTION).arrays[0]);
            }
        }

        [Test]
        public void ReleaseAndReuseTexture()
        {
            var originalSlots = textureArrayContainer.SetTexturesFromOriginalMaterial(testSourceMaterial, testTargetMaterial);

            for (var i = 0; i < originalSlots.Length && i < textureArrayContainer.count; i++)
            {
                TextureArraySlot? slot = originalSlots[i];

                Assert.That(slot, Is.Not.Null);

                slot.Value.FreeSlot();

                Assert.AreEqual(textureArrayContainer.mappings[i].Handler.GetOrCreateSlotHandler(TEST_RESOLUTION).freeSlots.Count, 1);
            }

            var replacedSlots = textureArrayContainer.SetTexturesFromOriginalMaterial(testSourceMaterial, testTargetMaterial);

            // Check the slots are the same as the original ones
            for (var i = 0; i < originalSlots.Length && i < textureArrayContainer.count; i++)
                Assert.AreEqual(originalSlots[i], replacedSlots[i]);
        }

    }

}
