using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.CharacterCamera.Components;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using DCL.FeatureFlags;
using DCL.Passport;
using DCL.Profiles;
using DCL.Utilities;
using DCL.Web3;
using ECS.Abstract;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.Interaction.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProcessPointerEventsSystem))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class ProcessOtherAvatarsInteractionSystem : BaseUnityLoopSystem
    {
        private const string VIEW_PROFILE_TOOLTIP = "View Profile";
        private const string OPTIONS_TOOLTIP = "Options";

        private readonly IEventSystem eventSystem;
        private readonly DCLInput dclInput;
        private readonly IMVCManagerMenusAccessFacade menusAccessFacade;
        private readonly IMVCManager mvcManager;
        private readonly ObjectProxy<Entity> cameraEntityProxy;
        private readonly bool useContextMenu;

        private HoverFeedbackComponent.Tooltip viewProfileTooltip;
        private Profile? currentProfileHovered;
        private Vector2? currentPositionHovered;
        private UniTaskCompletionSource contextMenuTask = new ();
        private bool wasCursorLockedWhenMenuOpened;

        internal ProcessOtherAvatarsInteractionSystem(
            World world,
            IEventSystem eventSystem,
            IMVCManagerMenusAccessFacade menusAccessFacade,
            IMVCManager mvcManager,
            ObjectProxy<Entity> cameraEntityProxy) : base(world)
        {
            this.eventSystem = eventSystem;
            dclInput = DCLInput.Instance;
            this.menusAccessFacade = menusAccessFacade;
            this.mvcManager = mvcManager;
            this.cameraEntityProxy = cameraEntityProxy;

            useContextMenu = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.AVATAR_CONTEXT_MENU);

            if (useContextMenu)
            {
                viewProfileTooltip = new HoverFeedbackComponent.Tooltip(OPTIONS_TOOLTIP, dclInput.Player.RightPointer);
                dclInput.Player.RightPointer!.performed += OpenOptionsContextMenu;
                dclInput.Player.Movement.performed += OnPlayerMoved;
                dclInput.Player.Jump.performed += OnPlayerMoved;
            }
            else
            {
                viewProfileTooltip = new HoverFeedbackComponent.Tooltip(VIEW_PROFILE_TOOLTIP, dclInput.Player.Pointer);
                dclInput.Player.Pointer!.performed += OpenPassport;
            }
        }

        protected override void Update(float t)
        {
            ProcessRaycastResultQuery(World);
        }

        protected override void OnDispose()
        {
            if (useContextMenu)
            {
                dclInput.Player.RightPointer!.performed -= OpenOptionsContextMenu;
                dclInput.Player.Movement.performed -= OnPlayerMoved;
                dclInput.Player.Jump.performed -= OnPlayerMoved;
            }
            else
            {
                dclInput.Player.Pointer!.performed -= OpenPassport;
            }

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
                || World.Has<HiddenPlayerComponent>(entityRef)
                || World.Has<IgnoreInteractionComponent>(entityRef))
                return;

            currentPositionHovered = Mouse.current.position.ReadValue();
            currentProfileHovered = profile;
            hoverStateComponent.AssignCollider(raycastResultForGlobalEntities.Collider, true, true);
            hoverFeedbackComponent.Add(viewProfileTooltip);
        }

        private void OpenPassport(InputAction.CallbackContext context)
        {
            if (context.control!.IsPressed() || currentProfileHovered == null)
                return;

            string userId = currentProfileHovered.UserId;

            if (string.IsNullOrEmpty(userId))
                return;

            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportParams(userId))).Forget();
        }

        private void OpenOptionsContextMenu(InputAction.CallbackContext context)
        {
            if (!context.control.IsPressed() || currentProfileHovered == null)
                return;

            string userId = currentProfileHovered.UserId;

            if (string.IsNullOrEmpty(userId))
                return;

            wasCursorLockedWhenMenuOpened = World.Get<CursorComponent>(cameraEntityProxy.Object).CursorState == CursorState.Locked;

            if (wasCursorLockedWhenMenuOpened)
                World.Add(cameraEntityProxy.Object, new PointerLockIntention(true, true));

            contextMenuTask.TrySetResult();
            contextMenuTask = new UniTaskCompletionSource();
            menusAccessFacade.ShowUserProfileContextMenuFromWalletIdAsync(
                new Web3Address(userId),
                currentPositionHovered!.Value,
                new Vector2(50, 0),
                CancellationToken.None,
                contextMenuTask.Task,
                anchorPoint: MenuAnchorPoint.CENTER_RIGHT,
                isOpenedOnWorldAvatar: true,
                onHide: OnContextMenuClosed);
        }

        private void OnContextMenuClosed()
        {
            if (wasCursorLockedWhenMenuOpened)
            {
                ref CursorComponent cursor = ref World.Get<CursorComponent>(cameraEntityProxy.Object);
                cursor.CursorState = CursorState.Locked;
            }
        }

        private void OnPlayerMoved(InputAction.CallbackContext obj)
        {
            contextMenuTask.TrySetResult();
        }
    }
}
