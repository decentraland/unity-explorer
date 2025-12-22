using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes;
using DCL.Character.CharacterCamera.Components;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using DCL.Profiles;
using DCL.SocialEmotes.UI;
using DCL.UI;
using DCL.UI.Controls.Configs;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Identities;
using ECS.Abstract;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;
using Random = UnityEngine.Random;
using SocialEmoteInteractionsManager = DCL.SocialEmotes.SocialEmoteInteractionsManager;

namespace DCL.Interaction.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProcessPointerEventsSystem))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class ProcessOtherAvatarsInteractionSystem : BaseUnityLoopSystem
    {
        private const string OPTIONS_TOOLTIP = "Interact";

        private readonly IEventSystem eventSystem;
        private readonly DCLInput dclInput;
        private readonly IMVCManagerMenusAccessFacade menusAccessFacade;
        private HoverFeedbackComponent.Tooltip viewProfileTooltip;
        private HoverFeedbackComponent.Tooltip socialEmoteInteractionTooltip;
        private Profile? currentProfileHovered;
        private Entity currentEntityHovered;
        private Vector2? currentPositionHovered;
        private UniTaskCompletionSource contextMenuTask = new ();
        private readonly IWeb3IdentityCache identityCache;
        private readonly CancellationTokenSource cts = new ();
        private readonly ObjectProxy<Entity> cameraEntityProxy;
        private readonly Entity playerEntity;
        private readonly SocialEmoteOutcomesContextMenuSettings contextMenuSettings;
        private readonly SocialEmotesSettings socialEmotesSettings;

        private readonly GenericContextMenu contextMenuConfiguration;

        private bool wasCursorLockedWhenMenuOpened;

        private ProcessOtherAvatarsInteractionSystem(
            World world,
            IEventSystem eventSystem,
            IMVCManagerMenusAccessFacade menusAccessFacade,
            IWeb3IdentityCache identityCache,
            ObjectProxy<Entity> cameraEntityProxy,
            Entity playerEntity,
            SocialEmoteOutcomesContextMenuSettings contextMenuSettings,
            SocialEmotesSettings socialEmotesSettings) : base(world)
        {
            this.eventSystem = eventSystem;
            dclInput = DCLInput.Instance;
            this.menusAccessFacade = menusAccessFacade;
            this.identityCache = identityCache;
            this.cameraEntityProxy = cameraEntityProxy;
            this.playerEntity = playerEntity;
            this.contextMenuSettings = contextMenuSettings;
            this.socialEmotesSettings = socialEmotesSettings;

            dclInput.Player.Movement.performed += OnPlayerMoved;
            dclInput.Player.Jump.performed += OnPlayerMoved;
            dclInput.Player.Pointer!.performed += OpenEmoteOutcomeContextMenu;
            dclInput.Player.RightPointer!.performed += OpenOptionsContextMenu;

            contextMenuConfiguration = new GenericContextMenu(contextMenuSettings.Width,
                contextMenuSettings.Offset,
                contextMenuSettings.VerticalLayoutPadding,
                contextMenuSettings.ElementsSpacing,
                ContextMenuOpenDirection.CENTER_RIGHT);
        }

        protected override void Update(float t)
        {
            ProcessRaycastResultQuery(World);
        }

        protected override void OnDispose()
        {
            cts.SafeCancelAndDispose();
            dclInput.Player.Pointer!.performed -= OpenEmoteOutcomeContextMenu;
            dclInput.Player.RightPointer!.performed -= OpenOptionsContextMenu;
            dclInput.Player.Movement.performed -= OnPlayerMoved;
            dclInput.Player.Jump.performed -= OnPlayerMoved;
            contextMenuTask.TrySetResult();
        }

        [Query]
        private void ProcessRaycastResult(ref PlayerOriginRaycastResultForGlobalEntities raycastResultForGlobalEntities,
            ref HoverFeedbackComponent hoverFeedbackComponent, ref HoverStateComponent hoverStateComponent)
        {
            currentProfileHovered = null;
            currentPositionHovered = null;
            hoverFeedbackComponent.Remove(viewProfileTooltip);
            hoverFeedbackComponent.Remove(socialEmoteInteractionTooltip);

            bool canHover = !eventSystem.IsPointerOverGameObject();
            GlobalColliderGlobalEntityInfo? entityInfo = raycastResultForGlobalEntities.GetEntityInfo();

            if (!raycastResultForGlobalEntities.IsValidHit || !canHover || entityInfo == null)
                return;

            currentEntityHovered = entityInfo.Value.EntityReference;

            if (!World.IsAlive(currentEntityHovered)
                || !World!.TryGet(currentEntityHovered, out Profile? profile)
                || World.Has<HiddenPlayerComponent>(currentEntityHovered)
                || World.Has<IgnoreInteractionComponent>(currentEntityHovered))
                return;

            var otherPosition = World.Get<CharacterTransform>(currentEntityHovered).Position;
            var playerPosition = World.Get<CharacterTransform>(playerEntity).Position;

            float sqrDistanceToAvatar = (otherPosition - playerPosition).sqrMagnitude;

            // Distance limit
            if (sqrDistanceToAvatar > socialEmotesSettings.VisibilityDistance * socialEmotesSettings.VisibilityDistance)
                return;

            currentPositionHovered = Mouse.current.position.ReadValue();
            currentProfileHovered = profile;
            hoverStateComponent.AssignCollider(raycastResultForGlobalEntities.Collider, true);

            bool isCloseEnoughToInteract = sqrDistanceToAvatar < socialEmotesSettings.InteractionDistance * socialEmotesSettings.InteractionDistance;

            // Avatar highlight
            if (!World.Has<ShowAvatarHighlightIntent>(currentEntityHovered))
                World.Add(currentEntityHovered, new ShowAvatarHighlightIntent(isCloseEnoughToInteract));

            // Tooltips
            var socialEmoteInteraction = SocialEmoteInteractionsManager.Instance.GetInteractionState(profile!.UserId);

            if (socialEmoteInteraction is { AreInteracting: false } &&
                (string.IsNullOrEmpty(socialEmoteInteraction.TargetWalletAddress) || // Is not a directed emote
                 socialEmoteInteraction.TargetWalletAddress == identityCache.Identity!.Address)) // Is a directed emote and the target is the local player
            {
                viewProfileTooltip = new HoverFeedbackComponent.Tooltip(OPTIONS_TOOLTIP, dclInput.Player.RightPointer);
                hoverFeedbackComponent.Add(viewProfileTooltip);
                socialEmoteInteractionTooltip = new HoverFeedbackComponent.Tooltip(socialEmoteInteraction.Emote.Model.Asset!.metadata.name, dclInput.Player.Pointer);
                hoverFeedbackComponent.Add(socialEmoteInteractionTooltip);
            }
            else
            {
                viewProfileTooltip = new HoverFeedbackComponent.Tooltip(OPTIONS_TOOLTIP, dclInput.Player.RightPointer);
                hoverFeedbackComponent.Add(viewProfileTooltip);
            }
        }

        private void OpenOptionsContextMenu(InputAction.CallbackContext context)
        {
            if (!context.control.IsPressed() || currentProfileHovered == null)
                return;

            string userId = currentProfileHovered.UserId;

            if (string.IsNullOrEmpty(userId))
                return;

            wasCursorLockedWhenMenuOpened = World.Get<CursorComponent>(cameraEntityProxy.Object).CursorState == CursorState.Locked;

            ref var cursor = ref World.Get<CursorComponent>(cameraEntityProxy.Object);

            if (cursor.CursorState == CursorState.Locked)
                World.Add(cameraEntityProxy.Object, new PointerLockIntention(true, true));

            World.Set(cameraEntityProxy.Object, cursor);

            contextMenuTask.TrySetResult();
            contextMenuTask = new UniTaskCompletionSource();
            menusAccessFacade.ShowUserProfileContextMenuFromWalletIdAsync(new Web3Address(userId), currentPositionHovered!.Value, new Vector2(50, 0), CancellationToken.None, contextMenuTask.Task, anchorPoint: MenuAnchorPoint.CENTER_RIGHT, isOpenedOnWorldAvatar: true, onHide: OnContextMenuClosed);
        }

        private void OnContextMenuClosed()
        {
            ReportHub.Log(ReportCategory.SOCIAL_EMOTE, "ProcessOtherAvatarsInteractionSystemOnContextMenuClosed()");

            if (wasCursorLockedWhenMenuOpened)
            {
                ReportHub.Log(ReportCategory.SOCIAL_EMOTE, "ProcessOtherAvatarsInteractionSystemOnContextMenuClosed() Cursor LOCKED");
                World.Get<CursorComponent>(cameraEntityProxy.Object).CursorState = CursorState.Locked;
            }
        }

        private void OpenEmoteOutcomeContextMenu(InputAction.CallbackContext context)
        {
            if (!context.control.IsPressed() || currentProfileHovered == null)
                return;

            string userId = currentProfileHovered.UserId;

            if (string.IsNullOrEmpty(userId))
                return;

            var interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(currentProfileHovered.UserId);

            if (interaction is { AreInteracting: false } &&
                (string.IsNullOrEmpty(interaction.TargetWalletAddress) || // Is not a directed emote
                 interaction.TargetWalletAddress == identityCache.Identity!.Address)) // Is a directed emote and the target is the local player
            {
                contextMenuConfiguration.ClearControls();

                contextMenuConfiguration.AddControl(new TextContextMenuControlSettings(interaction.Emote.Model.Asset.metadata.name));

                var outcomes = interaction.Emote.Model.Asset!.metadata.data!.outcomes!;

                if (interaction.Emote.Model.Asset!.metadata.data!.randomizeOutcomes)
                {
                    OnOutcomePerformed(Random.Range(0, outcomes.Length), currentProfileHovered.UserId);
                }
                else if (outcomes.Length == 1)
                {
                    OnOutcomePerformed(0, currentProfileHovered.UserId);
                }
                else
                {
                    for (int i = 0; i < outcomes.Length; ++i)
                    {
                        int outcomeIndex = i;
                        string initiatorWalletAddress = currentProfileHovered.UserId;
                        contextMenuConfiguration.AddControl(new ButtonContextMenuControlSettings(outcomes[i].title, contextMenuSettings.EmoteIcon,
                            () => OnOutcomePerformed(outcomeIndex, initiatorWalletAddress)));
                    }

                    contextMenuTask.TrySetResult();
                    contextMenuTask = new UniTaskCompletionSource();

                    var parameter = new GenericContextMenuParameter(
                        contextMenuConfiguration,
                        currentPositionHovered!.Value,
                        closeTask: contextMenuTask.Task,
                        actionOnHide: OnContextMenuClosed
                    );

                    ref var cursor = ref World.Get<CursorComponent>(cameraEntityProxy.Object);

                    wasCursorLockedWhenMenuOpened = World.Get<CursorComponent>(cameraEntityProxy.Object).CursorState == CursorState.Locked;

                    if (cursor.CursorState == CursorState.Locked)
                        World.Add(cameraEntityProxy.Object, new PointerLockIntention(true, true));

                    World.Set(cameraEntityProxy.Object, cursor);

                    menusAccessFacade.ShowGenericContextMenuAsync(parameter).Forget();
                }
            }
        }

        private void OnOutcomePerformed(int outcomeIndex, string interactingUserWalletAddress)
        {
            ReportHub.Log(ReportCategory.SOCIAL_EMOTE, "ProcessOtherAvatarsInteractionSystem.OnOutcomePerformed()");

            var interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(interactingUserWalletAddress);

            // The social emote interaction may have been cancelled since the context menu was open
            if (interaction == null)
                return;

            // Checks if the current emote has an outcome for the given index
            int outcomeCount = interaction!.Emote.Model.Asset!.metadata.data!.outcomes!.Length;

            if (outcomeIndex >= outcomeCount)
                return;

            if (interaction is { AreInteracting: false })
            {
                // Random outcome?
                if (outcomeIndex == 0 && interaction!.Emote.Model.Asset!.metadata.data!.randomizeOutcomes)
                {
                    outcomeIndex = Random.Range(0, outcomeCount);
                }

                var initiatorTransform = World.Get<CharacterTransform>(interaction.InitiatorEntity).Transform;

                World.Add(playerEntity, new MoveBeforePlayingSocialEmoteIntent(
                    initiatorTransform.position,
                    interaction.InitiatorEntity,
                    new TriggerEmoteReactingToSocialEmoteIntent(
                        interaction.Emote.DTO.Metadata.id,
                        outcomeIndex,
                        interaction.InitiatorWalletAddress,
                        interaction.Id))
                );

                // When reacting to a social emote, the camera mode is forced to be third person
                World.Get<CameraComponent>(cameraEntityProxy.Object).Mode = CameraMode.ThirdPerson;

                ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"ProcessOtherAvatarsInteractionSystem.OnOutcomePerformed() <color=#FF9933>MOVING --> TO INITIATOR outcome: {outcomeIndex}</color>");
            }
        }

        private void OnPlayerMoved(InputAction.CallbackContext obj)
        {
            // Moving the avatar closes the menus
            contextMenuTask.TrySetResult();
        }
    }
}
