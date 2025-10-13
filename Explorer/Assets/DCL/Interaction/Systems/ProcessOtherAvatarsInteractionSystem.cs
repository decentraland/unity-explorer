using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using DCL.Profiles;
using DCL.SocialEmotes.UI;
using DCL.Web3;
using ECS.Abstract;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;
using SocialEmoteInteractionsManager = DCL.SocialEmotes.SocialEmoteInteractionsManager;

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
        private readonly SocialEmoteOutcomeMenuController socialEmoteOutcomeMenuController;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        private ProcessOtherAvatarsInteractionSystem(
            World world,
            IEventSystem eventSystem,
            IMVCManagerMenusAccessFacade menusAccessFacade,
            IMVCManager mvcManager,
            EmotesBus emotesBus,
            SocialEmoteOutcomeMenuController socialEmoteOutcomeMenuController) : base(world)
        {
            this.eventSystem = eventSystem;
            dclInput = DCLInput.Instance;
            this.menusAccessFacade = menusAccessFacade;
            this.mvcManager = mvcManager;
            this.emotesBus = emotesBus;
            this.socialEmoteOutcomeMenuController = socialEmoteOutcomeMenuController;

            dclInput.Player.Pointer!.performed += OpenContextMenu;
        }

        protected override void Update(float t)
        {
            ProcessRaycastResultQuery(World);
        }

        protected override void OnDispose()
        {
            cts.SafeCancelAndDispose();
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
            {
                if(socialEmoteOutcomeMenuController.State != ControllerState.ViewHiding && socialEmoteOutcomeMenuController.State != ControllerState.ViewHidden)
                    socialEmoteOutcomeMenuController.HideViewAsync(cts.Token).Forget();

                return;
            }

            Entity entityRef = entityInfo.Value.EntityReference;

            if (!World.IsAlive(entityRef)
                || !World!.TryGet(entityRef, out Profile? profile)
                || World.Has<BlockedPlayerComponent>(entityRef)
                || World.Has<IgnoreInteractionComponent>(entityRef))
            {
                if(socialEmoteOutcomeMenuController.State != ControllerState.ViewHiding && socialEmoteOutcomeMenuController.State != ControllerState.ViewHidden)
                    socialEmoteOutcomeMenuController.HideViewAsync(cts.Token).Forget();

                return;
            }

            currentPositionHovered = Mouse.current.position.ReadValue();
            currentProfileHovered = profile;
            hoverStateComponent.AssignCollider(raycastResultForGlobalEntities.Collider, true);

            SocialEmoteInteractionsManager.SocialEmoteInteractionReadOnly? socialEmoteInteraction = SocialEmoteInteractionsManager.Instance.GetInteractionState(profile.UserId);

            if (socialEmoteInteraction.HasValue && !socialEmoteInteraction.Value.AreInteracting)
            {
                Vector3 otherPosition = World.Get<CharacterTransform>(entityRef).Position;
                Vector3 playerPosition = Vector3.zero;
                World.Query(in new QueryDescription().WithAll<CharacterTransform, PlayerComponent>(),
                (ref CharacterTransform characterTransform) => playerPosition = characterTransform.Position);

                const float MAX_SQR_DISTANCE_TO_INTERACT = 2.0f * 2.0f; // TODO: Move to a proper place
                float sqrDistanceToAvatar = (otherPosition - playerPosition).sqrMagnitude;

                if (socialEmoteOutcomeMenuController.State == ControllerState.ViewHidden)
                {
                    // From hidden to showing
                    mvcManager.ShowAsync(SocialEmoteOutcomeMenuController.IssueCommand(new SocialEmoteOutcomeMenuController.SocialEmoteOutcomeMenuParams()
                    {
                        InteractingUserWalletAddress = currentProfileHovered.UserId,
                        Username = profile.ValidatedName,
                        UsernameColor = profile.UserNameColor,
                        IsCloseEnoughToAvatar = sqrDistanceToAvatar < MAX_SQR_DISTANCE_TO_INTERACT
                    }), cts.Token).Forget();
                }
                else
                {
                    // Is visible, updates data
                    socialEmoteOutcomeMenuController.SetParams(new SocialEmoteOutcomeMenuController.SocialEmoteOutcomeMenuParams()
                    {
                        InteractingUserWalletAddress = currentProfileHovered.UserId,
                        Username = profile.ValidatedName,
                        UsernameColor = profile.UserNameColor,
                        IsCloseEnoughToAvatar = sqrDistanceToAvatar < MAX_SQR_DISTANCE_TO_INTERACT
                    });
                }
            }
            else
            {
                viewProfileTooltip = new HoverFeedbackComponent.Tooltip(HOVER_TOOLTIP, dclInput.Player.Pointer);
                hoverFeedbackComponent.Add(viewProfileTooltip);
            }

        }

        private void OpenContextMenu(InputAction.CallbackContext context)
        {
            if (context.control!.IsPressed() || currentProfileHovered == null)
                return;

            string userId = currentProfileHovered.UserId;

            if (string.IsNullOrEmpty(userId))
                return;

            SocialEmoteInteractionsManager.SocialEmoteInteractionReadOnly? socialEmoteInteraction = SocialEmoteInteractionsManager.Instance.GetInteractionState(userId);

            if (!socialEmoteInteraction.HasValue || socialEmoteInteraction.Value.AreInteracting)
            {
                // A context menu will be available if no social emote interaction is in process
                contextMenuTask.TrySetResult();
                contextMenuTask = new UniTaskCompletionSource();
                menusAccessFacade.ShowUserProfileContextMenuFromWalletIdAsync(new Web3Address(userId), currentPositionHovered!.Value, new Vector2(10, 0), CancellationToken.None, contextMenuTask.Task, anchorPoint: MenuAnchorPoint.CENTER_RIGHT, enableSocialEmotes: true);
            }
        }
    }
}
