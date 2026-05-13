using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;

namespace DCL.AvatarRendering.AvatarShape.FacialExpression
{
    public static class FacialExpressionApplier
    {
        public static void Apply(World world, Entity playerEntity, byte eyebrowsIndex, byte eyesIndex, byte mouthIndex)
        {
            world.AddOrGet(playerEntity, new TriggerFacialExpressionIntent
            {
                EyebrowsIndex = eyebrowsIndex,
                EyesIndex = eyesIndex,
                MouthIndex = mouthIndex,
            });
        }
    }
}