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

        public static Color4 GetEyeColor(this PBAvatarShape self)
        {
            Color3 rgb = self.EyeColor ?? NEUTRAL_COLOR;

            return new Color4
            {
                A = self.GetAlpha(),
                R = rgb.R,
                G = rgb.G,
                B = rgb.B,
            };
        }

        public static Color4 GetHairColor(this PBAvatarShape self)
        {
            Color3 rgb = self.HairColor ?? NEUTRAL_COLOR;

            return new Color4
            {
                A = self.GetAlpha(),
                R = rgb.R,
                G = rgb.G,
                B = rgb.B,
            };
        }

        public static Color4 GetSkinColor(this PBAvatarShape self)
        {
            Color3 rgb = self.SkinColor ?? NEUTRAL_COLOR;

            return new Color4
            {
                A = self.GetAlpha(),
                R = rgb.R,
                G = rgb.G,
                B = rgb.B,
            };
        }

        private static float GetAlpha(this PBAvatarShape self)
        {
            var alpha = 1f;

            if (self.HasIsBodyInvisible)
                alpha = self.IsBodyInvisible ? 0f : 1f;

            return alpha;
        }
    }
}
