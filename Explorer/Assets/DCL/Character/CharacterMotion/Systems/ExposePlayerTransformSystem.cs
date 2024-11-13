using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character;
using DCL.Character.Components;
using DCL.Diagnostics;
using ECS.Abstract;
using Utility;

namespace DCL.CharacterMotion.Systems
{
    /// <summary>
    ///     Writes current player position into the shared data.
    ///     In the future it must be extended to include other players' positions to propagate onto the fixed range of entities
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    [UpdateAfter(typeof(ChangeCharacterPositionGroup))]
    public partial class ExposePlayerTransformSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;
        private readonly ExposedTransform exposedTransform;

        internal ExposePlayerTransformSystem(World world, Entity playerEntity, ExposedTransform exposedTransform) : base(world)
        {
            this.playerEntity = playerEntity;
            this.exposedTransform = exposedTransform;
        }

        protected override void Update(float t)
        {
            CharacterTransform charTransform = World.Get<CharacterTransform>(playerEntity);

            exposedTransform.Position.Value = charTransform.Position;
            exposedTransform.Rotation.Value = charTransform.Rotation;
        }
    }
}
