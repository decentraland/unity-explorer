using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using DCL.Passport;
using DCL.Profiles;
using DCL.Web3;
using ECS.Abstract;
using MVC;
using System.Threading;
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
        private const string HOVER_TOOLTIP = "View Profile";

        private readonly IEventSystem eventSystem;
        private readonly DCLInput dclInput;
        private readonly IMVCManagerMenusAccessFacade menusAccessFacade;
        private readonly HoverFeedbackComponent.Tooltip viewProfileTooltip;
        private Profile? currentProfileHovered;
        private readonly IMVCManager mvcManager;
        private Vector2? currentPositionHovered;
        private UniTaskCompletionSource contextMenuTask = new ();

        private ProcessOtherAvatarsInteractionSystem(
            World world,
            IEventSystem eventSystem,
            DCLInput dclInput,
            IMVCManagerMenusAccessFacade menusAccessFacade,
            IMVCManager mvcManager) : base(world)
        {
            this.eventSystem = eventSystem;
            this.dclInput = dclInput;
            this.menusAccessFacade = menusAccessFacade;
            this.mvcManager = mvcManager;
            viewProfileTooltip = new HoverFeedbackComponent.Tooltip(HOVER_TOOLTIP, dclInput.Player.Pointer);

            dclInput.Player.Pointer!.performed += OpenContextMenu;
        }

        protected override void Update(float t)
        {
            ProcessRaycastResultQuery(World);
        }

        protected override void OnDispose()
        {
            dclInput.Player.RightPointer!.performed -= OpenContextMenu;
            contextMenuTask.TrySetResult();
        }

        [Query]
        private void ProcessRaycastResult(ref PlayerOriginRaycastResultForGlobalEntities raycastResultForGlobalEntities,
            ref HoverFeedbackComponent hoverFeedbackComponent, ref HoverStateComponent hoverStateComponent)
        {
            currentProfileHovered = null;
            currentPositionHovered = null;
            hoverFeedbackComponent.Remove(viewProfileTooltip);

            bool canHover = !eventSystem.IsPointerOverGameObject();
            GlobalColliderGlobalEntityInfo? entityInfo = raycastResultForGlobalEntities.GetEntityInfo();

            if (!raycastResultForGlobalEntities.IsValidHit || !canHover || entityInfo == null)
                return;

            Entity entityRef = entityInfo.Value.EntityReference;

            if (!World.IsAlive(entityRef)
                || !World!.TryGet(entityRef, out Profile? profile)
                || World.Has<BlockedPlayerComponent>(entityRef)
                || World.Has<IgnoreInteractionComponent>(entityRef))
                return;

            currentPositionHovered = Mouse.current.position.ReadValue();
            currentProfileHovered = profile;
            hoverStateComponent.AssignCollider(raycastResultForGlobalEntities.Collider, true);
            hoverFeedbackComponent.Add(viewProfileTooltip);
        }

        private void OpenContextMenu(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (context.control!.IsPressed() || currentProfileHovered == null)
                return;

            string userId = currentProfileHovered.UserId;

            if (string.IsNullOrEmpty(userId))
                return;

            //Commented for now, we will restore it later
            //contextMenuTask.TrySetResult();
            //contextMenuTask = new UniTaskCompletionSource();

            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(userId))).Forget();

            //menusAccessFacade.ShowUserProfileContextMenuFromWalletIdAsync(new Web3Address(userId), currentPositionHovered!.Value, new Vector2(10, 0), CancellationToken.None, contextMenuTask.Task, anchorPoint: MenuAnchorPoint.CENTER_RIGHT);
        }
    }
}
