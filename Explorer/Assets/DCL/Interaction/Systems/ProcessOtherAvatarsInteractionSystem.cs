using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.SocialEmotes;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using DCL.Profiles;
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
        private const string HOVER_TOOLTIP = "Options...";

        private readonly IEventSystem eventSystem;
        private readonly DCLInput dclInput;
        private readonly IMVCManagerMenusAccessFacade menusAccessFacade;
        private HoverFeedbackComponent.Tooltip viewProfileTooltip;
        private Profile? currentProfileHovered;
        private readonly IMVCManager mvcManager;
        private Vector2? currentPositionHovered;
        private UniTaskCompletionSource contextMenuTask = new ();
        private EmotesBus emotesBus;

        private ProcessOtherAvatarsInteractionSystem(
            World world,
            IEventSystem eventSystem,
            IMVCManagerMenusAccessFacade menusAccessFacade,
            IMVCManager mvcManager) : base(world)
        {
            this.eventSystem = eventSystem;
            dclInput = DCLInput.Instance;
            this.menusAccessFacade = menusAccessFacade;
            this.mvcManager = mvcManager;
//            viewProfileTooltip = new HoverFeedbackComponent.Tooltip(HOVER_TOOLTIP, dclInput.Player.Pointer);

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

            SocialEmoteInteractionsManager.SocialEmoteInteractionReadOnly? socialEmoteInteraction = SocialEmoteInteractionsManager.Instance.GetInteractionState(profile.WalletId);

            if (socialEmoteInteraction.HasValue && !socialEmoteInteraction.Value.AreInteracting)
                viewProfileTooltip = new HoverFeedbackComponent.Tooltip("INTERACT!", dclInput.Player.Pointer);
            else
                viewProfileTooltip = new HoverFeedbackComponent.Tooltip(HOVER_TOOLTIP, dclInput.Player.Pointer);

            hoverFeedbackComponent.Add(viewProfileTooltip);
        }

        private void OpenContextMenu(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (context.control!.IsPressed() || currentProfileHovered == null)
                return;

            string userId = currentProfileHovered.UserId;

            if (string.IsNullOrEmpty(userId))
                return;

            SocialEmoteInteractionsManager.SocialEmoteInteractionReadOnly? socialEmoteInteraction = SocialEmoteInteractionsManager.Instance.GetInteractionState(userId);

            if (socialEmoteInteraction.HasValue && !socialEmoteInteraction.Value.AreInteracting)
            {
                // The hovered avatar is playing a social emote, in the starting step
                emotesBus.PlaySocialEmoteReaction(userId, socialEmoteInteraction.Value.Emote, 0);
            }
            else
            {
                // A context menu will be available if no social emote interaction is in process
                contextMenuTask.TrySetResult();
                contextMenuTask = new UniTaskCompletionSource();
                menusAccessFacade.ShowUserProfileContextMenuFromWalletIdAsync(new Web3Address(userId), currentPositionHovered!.Value, new Vector2(10, 0), CancellationToken.None, contextMenuTask.Task, anchorPoint: MenuAnchorPoint.CENTER_RIGHT, enableSocialEmotes: true);
            }


        }
    }
}
