using DCL.ECSComponents;
using Decentraland.Common;

namespace DCL.AvatarRendering.AvatarShape.Helpers
{
    public static class AvatarShapeDefaults
    {
        private static readonly Color3 NEUTRAL_COLOR = new()
        {
            R = 0.6f, G = 0.462f, B = 0.356f,
        };

        private static readonly Color3 HAIR_DEFAULT_COLOR = new()
        {
            R = 0.283f, G = 0.142f, B = 0f,
        };

        public static Color3 GetEyeColor(this PBAvatarShape self) =>
            self.EyeColor ?? new Color3(NEUTRAL_COLOR);

        public static Color3 GetHairColor(this PBAvatarShape self) =>
            self.HairColor ?? new Color3(HAIR_DEFAULT_COLOR);

        public static Color4 GetSkinColor(this PBAvatarShape self)
        {
            Color3 rgb = self.SkinColor ?? NEUTRAL_COLOR;

            return new Color4
            {
                A = self.HasSkinColorAlpha ? self.SkinColorAlpha : 1f,
                R = rgb.R,
                G = rgb.G,
                B = rgb.B,
            };
        }
    }
}
