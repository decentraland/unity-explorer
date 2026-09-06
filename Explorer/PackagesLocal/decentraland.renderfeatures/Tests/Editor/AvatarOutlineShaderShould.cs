using NUnit.Framework;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.Tests
{
    /// <summary>
    ///     Avatars are skinned by a compute shader into the global avatar vertex
    ///     buffer and their renderers keep the bind-pose mesh, so an outline drawn
    ///     from the mesh attributes alone would trace the T-pose. The outline vertex
    ///     stage has to read the skinned vertices the way the avatar shaders do.
    /// </summary>
    [TestFixture]
    public class AvatarOutlineShaderShould
    {
        private const string SHADER_NAME = "Hidden/DCL/RenderFeatures/AvatarOutline";
        private const string SKINNED_VERTEX_BUFFER = "_GlobalAvatarBuffer";
        private const string SKINNED_VERTEX_BASE = "_DCL_OutlineSkinnedBase";

        private static readonly string[] NO_KEYWORDS = System.Array.Empty<string>();

        [Test]
        public void ReadTheComputeSkinnedVertices()
        {
            // Arrange
            Shader shader = Shader.Find(SHADER_NAME);
            Assert.IsNotNull(shader, $"{SHADER_NAME} is not importable");
            ShaderData.Pass pass = ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(0);

            // Act
            ShaderData.VariantCompileInfo vertex = pass.CompileVariant(ShaderType.Vertex, NO_KEYWORDS, ShaderCompilerPlatform.OpenGLCore, BuildTarget.StandaloneLinux64, true);

            // Assert
            Assert.IsTrue(vertex.Success, "outline vertex stage failed to compile");
            string glsl = Encoding.ASCII.GetString(vertex.ShaderData);
            Assert.IsTrue(glsl.Contains(SKINNED_VERTEX_BUFFER), $"outline vertex stage never reads {SKINNED_VERTEX_BUFFER}, so it traces the bind pose");
            Assert.IsTrue(glsl.Contains(SKINNED_VERTEX_BASE), $"outline vertex stage ignores {SKINNED_VERTEX_BASE}");
        }
    }
}
