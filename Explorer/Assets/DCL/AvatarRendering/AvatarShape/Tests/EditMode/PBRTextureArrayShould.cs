using NUnit.Framework;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    [TestFixture]
    public class PBRTextureArrayShould : TextureArrayShouldBase
    {
        protected override string targetShaderName => "DCL/Avatar_CelShading";
    }
}
