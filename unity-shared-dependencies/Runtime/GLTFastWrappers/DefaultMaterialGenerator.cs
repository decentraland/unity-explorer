using GLTFast;
using GLTFast.Materials;
using System;
using UnityEngine;

namespace DCL.GLTFast.Wrappers
{
    /// <summary>
    /// With this class we can override the material generation from GLTFast,
    /// in this case we are using the ShaderGraphMaterialGenerator that comes from GLTFast
    /// </summary>
    internal class DefaultMaterialGenerator : ShaderGraphMaterialGenerator
    {
        private const float CUSTOM_EMISSIVE_FACTOR = 5f;

        public override Material GenerateMaterial(int materialIndex, global::GLTFast.Schema.Material gltfMaterial, IGltfReadable gltf, bool pointsSupport = false)
        {
            Material generatedMaterial = base.GenerateMaterial(materialIndex, gltfMaterial, gltf);

            SetMaterialName(generatedMaterial, materialIndex, gltfMaterial);

            if (gltfMaterial.Emissive != Color.black) { generatedMaterial.SetColor(EmissiveFactorProperty, gltfMaterial.Emissive * CUSTOM_EMISSIVE_FACTOR); }

            return generatedMaterial;

            // This step is important if we want to keep the functionality of skin and hair colouring
            void SetMaterialName(Material material, int materialIndex, global::GLTFast.Schema.Material gltfMaterial)
            {
                material.name = "material";

                if (gltfMaterial.name.Contains("skin", StringComparison.InvariantCultureIgnoreCase))
                    material.name += "_skin";

                if (gltfMaterial.name.Contains("hair", StringComparison.InvariantCultureIgnoreCase))
                    material.name += "_hair";

                material.name += $"_{materialIndex}";
            }
        }
    }
}
