using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Systems;
using DCL.Interaction.Utility;
using DCL.Profiles;
using ECS.Abstract;

namespace DCL.Interaction.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerOriginatedRaycastSystem))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class ProcessOtherAvatarsInteractionSystem : BaseUnityLoopSystem
    {
        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private readonly IEventSystem eventSystem;

        internal ProcessOtherAvatarsInteractionSystem(
            World world,
            IEntityCollidersGlobalCache entityCollidersGlobalCache,
            IEventSystem eventSystem) : base(world)
        {
            this.entityCollidersGlobalCache = entityCollidersGlobalCache;
            this.eventSystem = eventSystem;
        }

        protected override void Update(float t)
        {
            ProcessRaycastResultQuery(World);
        }

        [Query]
        private void ProcessRaycastResult(ref PlayerOriginRaycastResultForGlobalEntities raycastResultForGlobalEntities)
        {
            bool canHover = !eventSystem.IsPointerOverGameObject();
            GlobalColliderGlobalEntityInfo? entityInfo = raycastResultForGlobalEntities.GetEntityInfo();

            if (!raycastResultForGlobalEntities.IsValidHit || !canHover || entityInfo == null)
                return;

            EntityReference entityRef = entityInfo.Value.EntityReference;

            if (entityRef.IsAlive(World) && World.TryGet(entityRef, out Profile? profile))
            {
                // TODO (Santi): Continue here...
            }
        }
    }
}
