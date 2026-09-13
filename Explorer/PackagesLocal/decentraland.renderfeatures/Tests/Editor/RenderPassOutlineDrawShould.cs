using DCL.Rendering.RenderGraphs.RenderFeatures.AvatarOutline;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.Tests
{
    [TestFixture]
    public class RenderPassOutlineDrawShould
    {
        private const string AVATAR_SHADER = "DCL/DCL_Toon";
        private const string OUTLINE_SHADER = "Hidden/DCL/RenderFeatures/AvatarOutline";

        private static readonly int LAST_AVATAR_VERT_COUNT_ID = Shader.PropertyToID("_lastAvatarVertCount");
        private static readonly int LAST_WEARABLE_VERT_COUNT_ID = Shader.PropertyToID("_lastWearableVertCount");

        private Material avatarMaterial;
        private Material plainMaterial;

        [SetUp]
        public void SetUp()
        {
            Shader avatarShader = Shader.Find(AVATAR_SHADER);
            Assert.IsNotNull(avatarShader, $"{AVATAR_SHADER} is not importable");
            avatarMaterial = new Material(avatarShader);
            plainMaterial = new Material(Shader.Find(OUTLINE_SHADER));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(avatarMaterial);
            Object.DestroyImmediate(plainMaterial);
        }

        [Test]
        public void ResolveTheSkinnedVertexBaseFromTheAvatarMaterial()
        {
            // Arrange
            avatarMaterial.SetInteger(LAST_AVATAR_VERT_COUNT_ID, 4096);
            avatarMaterial.SetInteger(LAST_WEARABLE_VERT_COUNT_ID, 300);

            // Act
            float skinnedBase = RenderPass_OutlineDraw.SkinnedVertexBase(avatarMaterial);

            // Assert
            Assert.AreEqual(4396f, skinnedBase);
        }

        [Test]
        public void DrawTheMeshAttributesForMaterialsWithoutAvatarSkinning()
        {
            // Act
            float skinnedBase = RenderPass_OutlineDraw.SkinnedVertexBase(plainMaterial);
            float missingBase = RenderPass_OutlineDraw.SkinnedVertexBase(null);

            // Assert
            Assert.Less(skinnedBase, 0f);
            Assert.Less(missingBase, 0f);
        }
    }
}
