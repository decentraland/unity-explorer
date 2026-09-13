using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace StylizedGrass.Tests
{
    /// <summary>
    ///     Compiles StylizedGrass.shader variants the way the Linux player build does and inspects
    ///     the reflected bindings, so a feature the material authors (a texture, a parameter, a
    ///     keyword) is proven to reach the GPU rather than only to survive serialization.
    /// </summary>
    [TestFixture]
    public class StylizedGrassShaderShould
    {
        private const string SHADER_PATH = "Packages/decentraland.grassshader/Runtime/Shaders/StylizedGrass.shader";
        private const string FORWARD_PASS = "ForwardLit";
        private const string SHADOW_PASS = "ShadowCaster";
        private const string PER_MATERIAL = "UnityPerMaterial";

        private Shader shader;

        [SetUp]
        public void SetUp()
        {
            shader = AssetDatabase.LoadAssetAtPath<Shader>(SHADER_PATH);
            Assert.IsNotNull(shader, $"{SHADER_PATH} did not load");
        }

        [Test]
        public void CompileEveryPassForTheLinuxPlayer()
        {
            ShaderData.Subshader subshader = ShaderUtil.GetShaderData(shader).GetSubshader(0);

            for (var i = 0; i < subshader.PassCount; i++)
            {
                ShaderData.Pass pass = subshader.GetPass(i);
                AssertCompiles(pass.Name, ShaderType.Vertex);
                AssertCompiles(pass.Name, ShaderType.Fragment);
                AssertCompiles(pass.Name, ShaderType.Vertex, "_GPU_GRASS_BATCHING");
                AssertCompiles(pass.Name, ShaderType.Vertex, "_GPU_INSTANCER_BATCHER");
                AssertCompiles(pass.Name, ShaderType.Fragment, "_GPU_INSTANCER_BATCHER");
            }
        }

        [Test]
        public void DeclareTheRoadInstancerKeywordAndBuffers()
        {
            // GPUInstancingService admits a material to its indirect draws only when its shader
            // declares this keyword, then binds these two structured buffers by name through the
            // material property block. The Vulkan reflection exposes textures and constant buffers
            // only, so the buffer declarations are checked in the batcher branch of the source.
            CollectionAssert.Contains(shader.keywordSpace.keywordNames, "_GPU_INSTANCER_BATCHER");

            // The road-prop materials enable instancing variants, so the variant they draw with
            // pairs the batcher keyword with INSTANCING_ON: the vertex input takes SV_InstanceID
            // from the instancing macros and the Varyings carry the instancing fields too.
            AssertCompiles(FORWARD_PASS, ShaderType.Vertex, "INSTANCING_ON", "_GPU_INSTANCER_BATCHER");
            AssertCompiles(FORWARD_PASS, ShaderType.Fragment, "INSTANCING_ON", "_GPU_INSTANCER_BATCHER");
            AssertCompiles(SHADOW_PASS, ShaderType.Vertex, "INSTANCING_ON", "_GPU_INSTANCER_BATCHER");
            AssertCompiles(SHADOW_PASS, ShaderType.Fragment, "INSTANCING_ON", "_GPU_INSTANCER_BATCHER");

            string source = File.ReadAllText(FileUtil.GetPhysicalPath(SHADER_PATH));
            StringAssert.Contains("StructuredBuffer<PerInstanceBuffer> _PerInstanceBuffer;", source);
            StringAssert.Contains("StructuredBuffer<PerInstanceLookUpAndDither> _PerInstanceLookUpAndDitherBuffer;", source);
        }

        [Test]
        public void SampleTheAuthoredWindMapWhenBendingBlades()
        {
            ShaderData.VariantCompileInfo forward = Reflect(FORWARD_PASS);
            ShaderData.VariantCompileInfo shadow = Reflect(SHADOW_PASS, "_GPU_GRASS_BATCHING");

            CollectionAssert.Contains(TextureNames(forward), "_WindMap");
            CollectionAssert.Contains(TextureNames(shadow), "_WindMap");
        }

        [Test]
        public void SampleTheAuthoredTranslucencyMapWhenLighting()
        {
            ShaderData.VariantCompileInfo forward = Reflect(FORWARD_PASS, "_ADVANCED_LIGHTING");

            CollectionAssert.Contains(TextureNames(forward), "_TranslucencyMap");
        }

        [Test]
        public void CarryTheWindRandomnessAndTranslucencyParametersPerMaterial()
        {
            ShaderData.VariantCompileInfo forward = Reflect(FORWARD_PASS, "_ADVANCED_LIGHTING");

            CollectionAssert.IsSubsetOf(new[]
            {
                "_WindGustFreq", "_WindGustSpeed", "_WindGustStrength", "_WindGustTint", "_WindFlutter",
                "_WindRandStrength", "_WindRandSpeed", "_WindObjectRand", "_WindVertexRand", "_WindSwinging",
                "_TranslucencyDirect", "_TranslucencyIndirect", "_TranslucencyOffset",
            }, PerMaterialFields(forward));
        }

        [Test]
        public void ExposeBillboardAndHueVariationAsMaterialKeywords()
        {
            string[] keywords = shader.keywordSpace.keywordNames;

            CollectionAssert.Contains(keywords, "_BILLBOARD");
            CollectionAssert.Contains(keywords, "_HUE_VARIATION");
        }

        [Test]
        public void BillboardBladesTowardTheCameraInEveryGeometryPass()
        {
            ShaderData.Subshader subshader = ShaderUtil.GetShaderData(shader).GetSubshader(0);

            for (var i = 0; i < subshader.PassCount; i++)
            {
                ShaderData.VariantCompileInfo pass = Reflect(subshader.GetPass(i).Name, "_BILLBOARD");
                AssertCompiles(subshader.GetPass(i).Name, ShaderType.Fragment, "_BILLBOARD");
                CollectionAssert.IsSubsetOf(new[] { "_Billboard", "_BillboardingVerticalRotation" }, PerMaterialFields(pass));
            }

            ShaderData.VariantCompileInfo shadow = Reflect(SHADOW_PASS, "_BILLBOARD");
            CollectionAssert.Contains(PerMaterialFields(shadow), "_BillboardShadowFade");
        }

        [Test]
        public void VaryHueAndRemapNormalsFromTheAuthoredParameters()
        {
            ShaderData.VariantCompileInfo forward = Reflect(FORWARD_PASS, "_HUE_VARIATION");
            AssertCompiles(FORWARD_PASS, ShaderType.Fragment, "_HUE_VARIATION");

            CollectionAssert.IsSubsetOf(new[]
            {
                "_NormalFlattening", "_NormalSpherify", "_NormalSpherifyMask", "_SpherifyNormals",
                "_HueVariation", "_HueVariationColor", "_HueVariationHeight",
            }, PerMaterialFields(forward));
        }

        [Test]
        public void FadeBladesByViewAngleAlongsideDistance()
        {
            ShaderData.VariantCompileInfo forward = Reflect(FORWARD_PASS, "_FADING");
            AssertCompiles(FORWARD_PASS, ShaderType.Fragment, "_FADING");

            CollectionAssert.IsSubsetOf(new[] { "_AngleFading", "_FadeAngleThreshold" }, PerMaterialFields(forward));
        }

        // The Vulkan compiler reports resource bindings and constant buffers on the vertex-stage
        // compile of a pass; the fragment-stage compile only reports success and messages.
        private ShaderData.VariantCompileInfo Reflect(string passName, params string[] keywords) =>
            AssertCompiles(passName, ShaderType.Vertex, keywords);

        private ShaderData.VariantCompileInfo AssertCompiles(string passName, ShaderType stage, params string[] keywords)
        {
            ShaderData.Subshader subshader = ShaderUtil.GetShaderData(shader).GetSubshader(0);
            ShaderData.Pass pass = null;

            for (var i = 0; i < subshader.PassCount; i++)
            {
                if (subshader.GetPass(i).Name == passName)
                {
                    pass = subshader.GetPass(i);
                    break;
                }
            }

            Assert.IsNotNull(pass, $"pass {passName} not found");

            ShaderData.VariantCompileInfo info = pass.CompileVariant(stage, keywords, ShaderCompilerPlatform.Vulkan, BuildTarget.StandaloneLinux64);

            var report = new StringBuilder();

            foreach (ShaderMessage message in info.Messages)
                report.AppendLine($"{message.severity}: {message.message} ({message.file}:{message.line})");

            Assert.IsTrue(info.Success, $"{passName}/{stage} [{string.Join(" ", keywords)}] failed to compile:\n{report}");
            return info;
        }

        private static List<string> TextureNames(ShaderData.VariantCompileInfo info)
        {
            var names = new List<string>();

            foreach (ShaderData.TextureBindingInfo binding in info.TextureBindings)
                names.Add(binding.Name);

            return names;
        }

        private static List<string> PerMaterialFields(ShaderData.VariantCompileInfo info)
        {
            var names = new List<string>();

            foreach (ShaderData.ConstantBufferInfo buffer in info.ConstantBuffers)
            {
                if (buffer.Name != PER_MATERIAL)
                    continue;

                foreach (ShaderData.ConstantInfo field in buffer.Fields)
                    names.Add(field.Name);
            }

            return names;
        }
    }
}
