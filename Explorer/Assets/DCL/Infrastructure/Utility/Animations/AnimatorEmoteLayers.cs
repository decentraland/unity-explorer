using DCL.ECSComponents;

namespace Utility.Animations
{
    public static class AnimatorEmoteLayers
    {
        public const string BASE_LAYER = "Base Layer";
        public const string UPPER_BODY_LAYER = "Upper Body Layer";

        public static readonly string[] ALL_LAYERS =
        {
            BASE_LAYER,
            UPPER_BODY_LAYER,
        };

        public static readonly string[] NON_BASE_LAYERS =
        {
            UPPER_BODY_LAYER,
        };

        public static string GetFromEmoteMask(AvatarEmoteMask mask) =>
                    mask switch
                    {
                        AvatarEmoteMask.AemFullBody => BASE_LAYER,
                        AvatarEmoteMask.AemUpperBody => UPPER_BODY_LAYER,
                        _ => BASE_LAYER,
                    };
    }
}
