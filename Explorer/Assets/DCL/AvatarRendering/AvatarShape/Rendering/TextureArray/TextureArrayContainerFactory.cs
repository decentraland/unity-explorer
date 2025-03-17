using System.Collections.Generic;
using UnityEngine;
using static DCL.AvatarRendering.AvatarShape.Rendering.TextureArray.TextureArrayConstants;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public class TextureArrayContainerFactory
    {
        private readonly IReadOnlyDictionary<TextureArrayKey, Texture> defaultTextures;
        private readonly bool enableRawGltfWearables;

        public TextureArrayContainerFactory(IReadOnlyDictionary<TextureArrayKey, Texture> defaultTextures, bool enableRawGltfWearables = false)
        {
            this.defaultTextures = defaultTextures;
            this.enableRawGltfWearables = enableRawGltfWearables;
        }

        private TextureArrayContainer CreateSceneLOD(TextureFormat textureFormat, IReadOnlyList<TextureArrayResolutionDescriptor> defaultResolutionsDescriptors,
            int arraySizeForMissingResolutions, int capacityForMissingResolutions)
        {
            return new TextureArrayContainer( new TextureArrayMapping[]
            {
                new (new TextureArrayHandler(defaultResolutionsDescriptors, BASE_MAP_TEX_ARR_INDEX, BASE_MAP_TEX_ARR,
                        textureFormat, new Dictionary<TextureArrayKey, Texture>(), arraySizeForMissingResolutions, capacityForMissingResolutions),
                    MAINTEX_ORIGINAL_TEXTURE, MAIN_TEXTURE_RESOLUTION)
            });
        }

        private TextureArrayContainer CreatePBR(IReadOnlyList<int> defaultResolutions)
        {
            return new TextureArrayContainer(
                new TextureArrayMapping[]
                {
                    new (new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, BASE_MAP_TEX_ARR_INDEX, BASE_MAP_TEX_ARR, defaultResolutions, DEFAULT_BASEMAP_TEXTURE_FORMAT, defaultTextures), BASE_MAP_ORIGINAL_TEXTURE, MAIN_TEXTURE_RESOLUTION),
                });
        }

        private TextureArrayContainer CreateToon(IReadOnlyList<int> defaultResolutions)
        {
            var textureArrayMapping = new List<TextureArrayMapping>
            {
                // Asset Bundle Wearables
                new (new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, MAINTEX_ARR_SHADER_INDEX, MAINTEX_ARR_TEX_SHADER, defaultResolutions, DEFAULT_BASEMAP_TEXTURE_FORMAT, defaultTextures),
                    MAINTEX_ORIGINAL_TEXTURE, MAIN_TEXTURE_RESOLUTION),
                new (new TextureArrayHandler(NORMAL_TEXTURE_ARRAY_SIZE, NORMAL_MAP_TEX_ARR_INDEX, NORMAL_MAP_TEX_ARR, defaultResolutions, DEFAULT_NORMALMAP_TEXTURE_FORMAT, defaultTextures),
                    BUMP_MAP_ORIGINAL_TEXTURE_ID, NORMAL_TEXTURE_RESOLUTION),
                new (new TextureArrayHandler(EMISSION_TEXTURE_ARRAY_SIZE, EMISSIVE_MAP_TEX_ARR_INDEX, EMISSIVE_MAP_TEX_ARR, defaultResolutions, DEFAULT_EMISSIVEMAP_TEXTURE_FORMAT, defaultTextures),
                    EMISSION_MAP_ORIGINAL_TEXTURE_ID, EMISSION_TEXTURE_RESOLUTION),
            };

            if (enableRawGltfWearables)
            {
                // Raw GLTF Wearables
                textureArrayMapping.AddRange(new[]
                {
                    new TextureArrayMapping(new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, MAINTEX_ARR_SHADER_INDEX, MAINTEX_ARR_TEX_SHADER, defaultResolutions, DEFAULT_RAW_WEARABLE_TEXTURE_FORMAT),
                        MAINTEX_ORIGINAL_TEXTURE, MAIN_TEXTURE_RESOLUTION),
                    new TextureArrayMapping(new TextureArrayHandler(NORMAL_TEXTURE_ARRAY_SIZE, NORMAL_MAP_TEX_ARR_INDEX, NORMAL_MAP_TEX_ARR, defaultResolutions, DEFAULT_RAW_WEARABLE_TEXTURE_FORMAT),
                        BUMP_MAP_ORIGINAL_TEXTURE_ID, NORMAL_TEXTURE_RESOLUTION),
                    new TextureArrayMapping(new TextureArrayHandler(EMISSION_TEXTURE_ARRAY_SIZE, EMISSIVE_MAP_TEX_ARR_INDEX, EMISSIVE_MAP_TEX_ARR, defaultResolutions, DEFAULT_RAW_WEARABLE_TEXTURE_FORMAT),
                        EMISSION_MAP_ORIGINAL_TEXTURE_ID, EMISSION_TEXTURE_RESOLUTION)
                });
            }

            return new TextureArrayContainer(textureArrayMapping);
        }

        private TextureArrayContainer CreateFacial(IReadOnlyList<int> defaultResolutions)
        {
            var textureArrayMapping = new List<TextureArrayMapping>
            {
                // Asset Bundle Facial Feature Wearables
                new (new TextureArrayHandler(FACIAL_FEATURES_TEXTURE_ARRAY_SIZE, MAINTEX_ARR_SHADER_INDEX, MAINTEX_ARR_TEX_SHADER, defaultResolutions, DEFAULT_BASEMAP_TEXTURE_FORMAT, defaultTextures),
                    MAINTEX_ORIGINAL_TEXTURE, FACIAL_FEATURES_TEXTURE_RESOLUTION),
                new (new TextureArrayHandler(FACIAL_FEATURES_TEXTURE_ARRAY_SIZE, MASK_ARR_SHADER_ID, MASK_ARR_TEX_SHADER_ID, defaultResolutions, DEFAULT_BASEMAP_TEXTURE_FORMAT, defaultTextures),
                    MASK_ORIGINAL_TEXTURE_ID, FACIAL_FEATURES_TEXTURE_RESOLUTION)
            };

            if (enableRawGltfWearables)
            {
                // Raw Facial Feature Wearables
                textureArrayMapping.AddRange(new[]
                {
                    new TextureArrayMapping(new TextureArrayHandler(FACIAL_FEATURES_TEXTURE_ARRAY_SIZE, MAINTEX_ARR_SHADER_INDEX, MAINTEX_ARR_TEX_SHADER, defaultResolutions, DEFAULT_RAW_WEARABLE_TEXTURE_FORMAT),
                        MAINTEX_ORIGINAL_TEXTURE, FACIAL_FEATURES_TEXTURE_RESOLUTION),
                    new TextureArrayMapping(new TextureArrayHandler(FACIAL_FEATURES_TEXTURE_ARRAY_SIZE, MASK_ARR_SHADER_ID, MASK_ARR_TEX_SHADER_ID, defaultResolutions, DEFAULT_RAW_WEARABLE_TEXTURE_FORMAT),
                        MASK_ORIGINAL_TEXTURE_ID, FACIAL_FEATURES_TEXTURE_RESOLUTION)
                });
            }

            return new TextureArrayContainer(textureArrayMapping);
        }

        public TextureArrayContainer Create(Shader shader, IReadOnlyList<int> defaultResolutions)
        {
            return shader.name switch
                   {
                TOON_SHADER => CreateToon(defaultResolutions),
                FACIAL_SHADER => CreateFacial(defaultResolutions),
                _ => CreatePBR(defaultResolutions)
            };
        }

        public TextureArrayContainer CreateSceneLOD(string shaderName, IReadOnlyList<TextureArrayResolutionDescriptor> defaultResolutions, TextureFormat format,
            int arraySizeForMissingResolutions, int capacityForMissingResolutions)
        {
            return shaderName switch
            {
                SCENE_TEX_ARRAY_SHADER => CreateSceneLOD(format, defaultResolutions, arraySizeForMissingResolutions, capacityForMissingResolutions),
                _ => CreatePBR(new []
                {
                    256
                })
            };
        }
    }
}
