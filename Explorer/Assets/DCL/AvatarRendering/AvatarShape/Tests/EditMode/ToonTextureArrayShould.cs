using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using NUnit.Framework;

namespace DCL.AvatarRendering.AvatarShape.Tests
{

    public class ToonTextureArrayShould : TextureArrayShouldBase
    {
        protected override string targetShaderName => "DCL/DCL_Toon";
    }
}
