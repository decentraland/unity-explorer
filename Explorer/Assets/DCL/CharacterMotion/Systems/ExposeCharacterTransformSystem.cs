using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Character;
using DCL.Character.Components;
using DCL.Diagnostics;
using ECS.Abstract;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    [UpdateAfter(typeof(InterpolateCharacterSystem))]
    public partial class ExposeCharacterTransformSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;
        private readonly ExposedPlayerTransform exposedPlayerTransform;
        private readonly IECSToCRDTWriter ecsToCrdtWriter;

        internal ExposeCharacterTransformSystem(World world, Entity playerEntity, ExposedPlayerTransform exposedPlayerTransform, IECSToCRDTWriter ecsToCrdtWriter) : base(world)
        {
            this.playerEntity = playerEntity;
            this.ecsToCrdtWriter = ecsToCrdtWriter;
            this.exposedPlayerTransform = exposedPlayerTransform;
        }

        protected override void Update(float t)
        {
            CharacterTransform charTransform = World.Get<CharacterTransform>(playerEntity);
        }
    }
}
