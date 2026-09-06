using NUnit.Framework;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.Tests
{
    /// <summary>
    ///     Compiles Decentraland/StylizedOcean through the editor shader compiler
    ///     (no GPU needed) and checks that every material feature the shipped water
    ///     materials author is actually consumed by the program, not only declared.
    /// </summary>
    [TestFixture]
    public class StylizedOceanShaderShould
    {
        private const string SHADER_NAME = "Decentraland/StylizedOcean";
        private const string CORE_INCLUDE = "StylizedOceanCore.hlsl";
        private const string OCEAN_MATERIAL = "Assets/DCL/Landscape/Assets/Materials/Ocean.mat";
        private const string POND_MATERIAL = "Assets/DCL/Landscape/Assets/Terrain/Pond.mat";
        private const string FORWARD_PASS = "ForwardLit";
        private const int TESSELLATED_SUBSHADER = 0;
        private const int FALLBACK_SUBSHADER = 1;

        private static readonly string[] NO_KEYWORDS = System.Array.Empty<string>();

        private Shader shader;
        private ShaderData shaderData;
        private string coreSource;

        [OneTimeSetUp]
        public void SetUp()
        {
            shader = Shader.Find(SHADER_NAME);
            Assert.IsNotNull(shader, $"{SHADER_NAME} is not importable");
            shaderData = ShaderUtil.GetShaderData(shader);

            string shaderPath = AssetDatabase.GetAssetPath(shader);
            string shaderDirectory = Path.GetDirectoryName(shaderPath) ?? string.Empty;
            coreSource = File.ReadAllText(Path.Combine(shaderDirectory, CORE_INCLUDE));
        }

        [Test]
        public void CompileWithoutErrors()
        {
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), $"{SHADER_NAME} reports compile errors");

            // The FallBack shader's own SubShaders are appended after the two authored here.
            Assert.GreaterOrEqual(shaderData.SubshaderCount, 2, "expected a tessellated SubShader and a vertex-only fallback");
        }

        [Test]
        public void TessellateTheSurfaceInTheFirstSubShader()
        {
            // Arrange
            ShaderData.Pass pass = FindPass(TESSELLATED_SUBSHADER, FORWARD_PASS);

            // Act
            ShaderData.VariantCompileInfo hull = Compile(pass, ShaderType.Hull);
            ShaderData.VariantCompileInfo domain = Compile(pass, ShaderType.Domain);

            // Assert
            Assert.IsTrue(pass.HasShaderStage(ShaderType.Hull), "hull stage missing");
            Assert.IsTrue(pass.HasShaderStage(ShaderType.Domain), "domain stage missing");
            AssertCompiled(hull, "hull");
            AssertCompiled(domain, "domain");
            AssertConsumed("_TessValue");
            AssertConsumed("_TessMin");
            AssertConsumed("_TessMax");
        }

        [Test]
        public void KeepAVertexOnlyFallbackSubShader()
        {
            // Arrange
            ShaderData.Pass pass = FindPass(FALLBACK_SUBSHADER, FORWARD_PASS);

            // Act
            ShaderData.VariantCompileInfo vertex = Compile(pass, ShaderType.Vertex);
            ShaderData.VariantCompileInfo fragment = Compile(pass, ShaderType.Fragment);

            // Assert
            Assert.IsFalse(pass.HasShaderStage(ShaderType.Hull), "fallback must not require tessellation");
            AssertCompiled(vertex, "fallback vertex");
            AssertCompiled(fragment, "fallback fragment");
        }

        [Test]
        public void SampleTheCausticsTexture()
        {
            // Arrange
            ShaderData.Pass pass = FindPass(TESSELLATED_SUBSHADER, FORWARD_PASS);

            // Act
            ShaderData.VariantCompileInfo fragment = Compile(pass, ShaderType.Fragment);

            // Assert
            AssertCompiled(fragment, "fragment");
            AssertBindsTexture(pass, "_CausticsTex");
            AssertConsumed("_CausticsOn");
            AssertConsumed("_CausticsBrightness");
            AssertConsumed("_CausticsDistortion");
            AssertConsumed("_CausticsTiling");
            AssertConsumed("_CausticsSpeed");
        }

        [Test]
        public void SampleTheIntersectionNoise()
        {
            // Arrange
            ShaderData.Pass pass = FindPass(TESSELLATED_SUBSHADER, FORWARD_PASS);

            // Act
            ShaderData.VariantCompileInfo fragment = Compile(pass, ShaderType.Fragment);

            // Assert
            AssertCompiled(fragment, "fragment");
            AssertBindsTexture(pass, "_IntersectionNoise");
            AssertConsumed("_IntersectionStyle");
            AssertConsumed("_IntersectionSource");
            AssertConsumed("_IntersectionSpeed");
            AssertConsumed("_IntersectionTiling");
            AssertConsumed("_IntersectionColor");
            AssertConsumed("_IntersectionLength");
            AssertConsumed("_IntersectionFalloff");
            AssertConsumed("_IntersectionClipping");
            AssertConsumed("_IntersectionRippleDist");
            AssertConsumed("_IntersectionRippleStrength");
            AssertConsumed("_CrossPan_IntersectionOn");
            AssertConsumed("_FoamOn");
        }

        [Test]
        public void ReadTheSparkleProperties()
        {
            AssertConsumed("_SparkleIntensity");
            AssertConsumed("_SparkleSize");
        }

        [Test]
        public void ReadTheSlopeFoamProperties()
        {
            AssertConsumed("_SlopeFoam");
            AssertConsumed("_SlopeAngleThreshold");
            AssertConsumed("_SlopeAngleFalloff");
            AssertConsumed("_SlopeSpeed");
            AssertConsumed("_SlopeStretching");
            AssertConsumed("_SlopeThreshold");
        }

        [Test]
        public void BindBothShippedWaterMaterials()
        {
            // Arrange
            Material ocean = AssetDatabase.LoadAssetAtPath<Material>(OCEAN_MATERIAL);
            Material pond = AssetDatabase.LoadAssetAtPath<Material>(POND_MATERIAL);

            // Assert
            Assert.IsNotNull(ocean);
            Assert.IsNotNull(pond);
            Assert.AreEqual(shader, ocean.shader, "Ocean.mat is not bound to the stylized ocean shader");
            Assert.AreEqual(shader, pond.shader, "Pond.mat is not bound to the stylized ocean shader");
            Assert.AreEqual(1f, pond.GetFloat("_CausticsOn"), "Pond.mat authors caustics on");
            Assert.AreEqual(0f, pond.GetFloat("_FoamOn"), "Pond.mat authors shoreline foam off");
            Assert.AreEqual(1f, ocean.GetFloat("_IntersectionStyle"), "Ocean.mat authors rippled intersection foam");
            Assert.Greater(ocean.GetFloat("_TessValue"), 1f, "Ocean.mat authors tessellation");
        }

        private ShaderData.Pass FindPass(int subshader, string passName)
        {
            ShaderData.Subshader sub = shaderData.GetSubshader(subshader);

            for (var i = 0; i < sub.PassCount; i++)
            {
                ShaderData.Pass pass = sub.GetPass(i);

                if (pass.Name == passName)
                    return pass;
            }

            Assert.Fail($"pass {passName} not found in SubShader {subshader}");
            return null;
        }

        private static ShaderData.VariantCompileInfo Compile(ShaderData.Pass pass, ShaderType stage) =>
            pass.CompileVariant(stage, NO_KEYWORDS, ShaderCompilerPlatform.Vulkan, BuildTarget.StandaloneLinux64);

        private static void AssertCompiled(ShaderData.VariantCompileInfo info, string label)
        {
            if (info.Success)
                return;

            var messages = new StringBuilder();

            foreach (ShaderMessage message in info.Messages)
                messages.AppendLine($"{message.severity}: {message.message} ({message.file}:{message.line})");

            Assert.Fail($"{label} stage failed to compile:\n{messages}");
        }

        // The OpenGL Core backend emits GLSL text (only when compiling for an
        // external tool), in which only the samplers the program actually reads
        // survive; a declared-but-unused texture is gone.
        private static void AssertBindsTexture(ShaderData.Pass pass, string textureName)
        {
            ShaderData.VariantCompileInfo info = pass.CompileVariant(ShaderType.Fragment, NO_KEYWORDS, ShaderCompilerPlatform.OpenGLCore, BuildTarget.StandaloneLinux64, true);
            AssertCompiled(info, "OpenGL Core fragment");

            string glsl = Encoding.ASCII.GetString(info.ShaderData);
            Assert.IsTrue(glsl.Contains(textureName), $"compiled fragment does not sample {textureName}; it was optimised away as unused");
        }

        // A property is consumed when the core include references it outside its
        // constant-buffer declaration.
        private void AssertConsumed(string property)
        {
            int references = Regex.Matches(coreSource, $@"\b{Regex.Escape(property)}\b").Count;
            Assert.Greater(references, 1, $"{property} is declared but never read by {CORE_INCLUDE}");
        }
    }
}
