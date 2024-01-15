using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character;
using DCL.Character.Components;
using DCL.Diagnostics;
using ECS.Abstract;

namespace DCL.CharacterMotion.Systems
{
    /// <summary>
    ///     Writes current player position into the shared data.
    ///     In the future it must be extended to include other players' positions to propagate onto the fixed range of entities
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    [UpdateAfter(typeof(InterpolateCharacterSystem))]
    public partial class ExposePlayerTransformSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;
        private readonly ExposedPlayerTransform exposedPlayerTransform;

        internal ExposePlayerTransformSystem(World world, Entity playerEntity, ExposedPlayerTransform exposedPlayerTransform) : base(world)
        {
            this.playerEntity = playerEntity;
            this.exposedPlayerTransform = exposedPlayerTransform;
        }

        protected override void Update(float t)
        {
            CharacterTransform charTransform = World.Get<CharacterTransform>(playerEntity);

            exposedPlayerTransform.Position.Value = charTransform.Position;
            exposedPlayerTransform.Rotation.Value = charTransform.Rotation;
        }
    }
}
