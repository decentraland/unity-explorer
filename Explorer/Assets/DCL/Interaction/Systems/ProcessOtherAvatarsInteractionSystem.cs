using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using DCL.Profiles;
using ECS.Abstract;
using UnityEngine;
using UnityEngine.InputSystem;
using InputAction = DCL.ECSComponents.InputAction;

namespace DCL.Interaction.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProcessPointerEventsSystem))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class ProcessOtherAvatarsInteractionSystem : BaseUnityLoopSystem
    {
        private const string VIEW_PROFILE_TOOLTIP = "View Profile";

        private readonly IEventSystem eventSystem;
        private readonly DCLInput dclInput;
        private readonly HoverFeedbackComponent.Tooltip viewProfileTooltip = new (VIEW_PROFILE_TOOLTIP, InputAction.IaPointer);
        private Profile? currentProfileHovered;

        private ProcessOtherAvatarsInteractionSystem(World world, IEventSystem eventSystem, DCLInput dclInput) : base(world)
        {
            this.eventSystem = eventSystem;
            this.dclInput = dclInput;

            dclInput.Player.Pointer.performed += OpenPassport;
        }

        protected override void Update(float t)
        {
            ProcessRaycastResultQuery(World);
        }

        public override void Dispose()
        {
            dclInput.Player.Pointer.performed -= OpenPassport;
            base.Dispose();
        }

        [Query]
        private void ProcessRaycastResult(ref PlayerOriginRaycastResultForGlobalEntities raycastResultForGlobalEntities, ref HoverFeedbackComponent hoverFeedbackComponent, ref HoverStateComponent hoverStateComponent)
        {
            currentProfileHovered = null;
            hoverFeedbackComponent.Tooltips.Remove(viewProfileTooltip);

            bool canHover = !eventSystem.IsPointerOverGameObject();
            GlobalColliderGlobalEntityInfo? entityInfo = raycastResultForGlobalEntities.GetEntityInfo();

            if (!raycastResultForGlobalEntities.IsValidHit || !canHover || entityInfo == null)
                return;

            EntityReference entityRef = entityInfo.Value.EntityReference;

            if (!entityRef.IsAlive(World) || !World.TryGet(entityRef, out Profile? profile))
                return;

            currentProfileHovered = profile;

            hoverStateComponent.LastHitCollider = raycastResultForGlobalEntities.GetCollider();
            hoverStateComponent.HasCollider = true;
            hoverStateComponent.IsAtDistance = true;

            hoverFeedbackComponent.Tooltips.Add(viewProfileTooltip);
        }

        private void OpenPassport(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (context.control.IsPressed() || currentProfileHovered == null)
                return;

            Debug.LogError($"[SANTI LOG] Profile clicked: {currentProfileHovered.UserId}");
        }
    }
}
