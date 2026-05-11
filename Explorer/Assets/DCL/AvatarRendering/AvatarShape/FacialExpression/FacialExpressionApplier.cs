using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;

namespace DCL.AvatarRendering.AvatarShape.FacialExpression
{
    public class FacialExpressionApplier : IFacialExpressionApplier
    {
        private readonly World world;
        private readonly Entity playerEntity;

        public FacialExpressionApplier(World world, Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public void Apply(byte eyebrowsIndex, byte eyesIndex, byte mouthIndex)
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