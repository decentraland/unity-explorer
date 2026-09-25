using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Editor
{
    // The project's Assets/Rendering/ShaderVariants.shadervariants collection preserves the custom
    // "_GPU_INSTANCER_BATCHER" keyword (set at runtime via Material.EnableKeyword by
    // GPUInstancingMaterialsCache, never baked into any serialized material) only for the grass
    // shader. Rock_GPUIPro and GPUInstancerPro/Standard never got that variant captured, so their
    // GPU-instanced material clones fall back to the pink "missing variant" shader in a real player
    // build (the Editor never strips variants, so this never reproduces there).
    public static class FixGPUIShaderVariants
    {
        private const string COLLECTION_PATH = "Assets/Rendering/ShaderVariants.shadervariants";

        // Same keyword combinations already captured for the grass shader (decentraland.grassshader),
        // the one shader that already works - reused here since these are standard URP Lit keywords
        // shared across the affected shaders.
        private static readonly string[][] KeywordSets =
        {
            new string[] { },
            new[] { "FOG_EXP", "INSTANCING_ON", "_ADDITIONAL_LIGHTS", "_ADDITIONAL_LIGHT_SHADOWS", "_ADVANCED_LIGHTING", "_FADING", "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT", "_SPECULARHIGHLIGHTS_OFF" },
            new[] { "FOG_EXP", "INSTANCING_ON", "_ADDITIONAL_LIGHTS", "_ADDITIONAL_LIGHT_SHADOWS", "_ADVANCED_LIGHTING", "_GPU_INSTANCER_BATCHER", "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT" },
            new[] { "FOG_EXP", "INSTANCING_ON", "_ADDITIONAL_LIGHTS", "_ADDITIONAL_LIGHT_SHADOWS", "_ADVANCED_LIGHTING", "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT" },
            new[] { "FOG_EXP", "INSTANCING_ON", "_ADDITIONAL_LIGHTS", "_ADVANCED_LIGHTING", "_MAIN_LIGHT_SHADOWS_CASCADE" },
            new[] { "INSTANCING_ON" },
            new[] { "INSTANCING_ON", "_FADING" },
        };

        private static readonly string[] TargetShaderNames =
        {
            "Shader Graphs/Rock_GPUIPro",
            "GPUInstancerPro/Standard",
            "DCL/Tree",
            "DCL/TreeImpostor",
        };

        public static void Fix()
        {
            var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(COLLECTION_PATH);

            if (collection == null)
            {
                Debug.LogError($"[FixGPUIShaderVariants] Could not load {COLLECTION_PATH}");
                EditorApplication.Exit(1);
                return;
            }

            int added = 0;
            int skippedNoShader = 0;

            foreach (string shaderName in TargetShaderNames)
            {
                Shader shader = Shader.Find(shaderName);

                if (shader == null)
                {
                    Debug.LogWarning($"[FixGPUIShaderVariants] Shader not found: {shaderName}");
                    skippedNoShader++;
                    continue;
                }

                foreach (string[] keywords in KeywordSets)
                {
                    var variant = new ShaderVariantCollection.ShaderVariant
                    {
                        shader = shader,
                        passType = PassType.ScriptableRenderPipeline,
                        keywords = keywords,
                    };

                    if (!collection.Contains(variant))
                    {
                        collection.Add(variant);
                        added++;
                    }
                }
            }

            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();

            Debug.Log($"[FixGPUIShaderVariants] Added {added} variants, {skippedNoShader} shaders not found. Collection now has {collection.variantCount} variants.");
        }
    }
}
