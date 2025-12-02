using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes;
using DCL.Character.CharacterCamera.Components;
using DCL.Character.Components;
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
        private const string OPTIONS_TOOLTIP = "Options...";

        private readonly IEventSystem eventSystem;
        private readonly DCLInput dclInput;
        private readonly IMVCManagerMenusAccessFacade menusAccessFacade;
        private HoverFeedbackComponent.Tooltip viewProfileTooltip;
        private HoverFeedbackComponent.Tooltip socialEmoteInteractionTooltip;
        private Profile? currentProfileHovered;
        private Vector2? currentPositionHovered;
        private UniTaskCompletionSource contextMenuTask = new ();
        private readonly IWeb3IdentityCache identityCache;
        private readonly CancellationTokenSource cts = new ();
        private readonly ObjectProxy<Entity> cameraEntityProxy;
        private readonly Entity playerEntity;
        private readonly SocialEmoteOutcomesContextMenuSettings contextMenuSettings;
        private readonly SocialEmotesSettings socialEmotesSettings;

        private GenericContextMenu contextMenuConfiguration;

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
            contextMenuTask.TrySetResult();
        }

        private bool wasLocked;

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

            Entity entityRef = entityInfo.Value.EntityReference;

            if (!World.IsAlive(entityRef)
                || !World!.TryGet(entityRef, out Profile? profile)
                || World.Has<HiddenPlayerComponent>(entityRef)
                || World.Has<IgnoreInteractionComponent>(entityRef))
                return;

            currentPositionHovered = Mouse.current.position.ReadValue();
            currentProfileHovered = profile;
            hoverStateComponent.AssignCollider(raycastResultForGlobalEntities.Collider, true);

            Vector3 otherPosition = World.Get<CharacterTransform>(entityRef).Position;
            Vector3 playerPosition = World.Get<CharacterTransform>(playerEntity).Position;

            float sqrDistanceToAvatar = (otherPosition - playerPosition).sqrMagnitude;

            // Distance limit
            if(sqrDistanceToAvatar > socialEmotesSettings.VisibilityDistance * socialEmotesSettings.VisibilityDistance)
                return;

            // Tooltips
            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? socialEmoteInteraction = SocialEmoteInteractionsManager.Instance.GetInteractionState(profile!.UserId);

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

            wasLocked = World.Get<CursorComponent>(cameraEntityProxy.Object).CursorState == CursorState.Locked;

            ref CursorComponent cursor = ref World.Get<CursorComponent>(cameraEntityProxy.Object);

            if(cursor.CursorState == CursorState.Locked)
                World.Add(cameraEntityProxy.Object, new PointerLockIntention(true, true));

            World.Set(cameraEntityProxy.Object, cursor);

            contextMenuTask.TrySetResult();
            contextMenuTask = new UniTaskCompletionSource();
            menusAccessFacade.ShowUserProfileContextMenuFromWalletIdAsync(new Web3Address(userId), currentPositionHovered!.Value, new Vector2(50, 0), CancellationToken.None, contextMenuTask.Task, anchorPoint: MenuAnchorPoint.CENTER_RIGHT, enableSocialEmotes: true, onHide: OnHide);
        }

        private void OnHide()
        {
            ReportHub.Log(ReportCategory.EMOTE_DEBUG, "HIDDEN");

            if (wasLocked)
            {
                ReportHub.Log(ReportCategory.EMOTE_DEBUG, "--> LOCKED");
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

            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(currentProfileHovered.UserId);

            if (interaction is { AreInteracting: false } &&
                (string.IsNullOrEmpty(interaction.TargetWalletAddress) || // Is not a directed emote
                    interaction.TargetWalletAddress == identityCache.Identity!.Address)) // Is a directed emote and the target is the local player
            {
                contextMenuConfiguration.ClearControls();

                contextMenuConfiguration.AddControl(new TextContextMenuControlSettings(interaction.Emote.Model.Asset.metadata.name));

                EmoteDTO.EmoteOutcomeDTO[] outcomes = interaction.Emote.Model.Asset!.metadata.data!.outcomes!;

                if (interaction.Emote.Model.Asset!.metadata.data!.randomizeOutcomes)
                {
                    OnOutcomePerformed(Random.Range(0, outcomes.Length), currentProfileHovered.UserId, playerEntity);
                }
                else if (outcomes.Length == 1)
                {
                    OnOutcomePerformed(0, currentProfileHovered.UserId, playerEntity);
                }
                else
                {
                    for (int i = 0; i < outcomes.Length; ++i)
                    {
                        int outcomeIndex = i;
                        string initiatorWalletAddress = currentProfileHovered.UserId;
                        contextMenuConfiguration.AddControl(new ButtonContextMenuControlSettings(outcomes[i].title, contextMenuSettings.EmoteIcon,
                                                            () => OnOutcomePerformed(outcomeIndex, initiatorWalletAddress, playerEntity)));
                    }

                    contextMenuTask.TrySetResult();
                    contextMenuTask = new UniTaskCompletionSource();

                    GenericContextMenuParameter parameter = new GenericContextMenuParameter(
                        contextMenuConfiguration,
                        currentPositionHovered!.Value,
                        closeTask: contextMenuTask.Task,
                        actionOnHide: OnHide
                    );

                    ref CursorComponent cursor = ref World.Get<CursorComponent>(cameraEntityProxy.Object);

                    wasLocked = World.Get<CursorComponent>(cameraEntityProxy.Object).CursorState == CursorState.Locked;

                    if(cursor.CursorState == CursorState.Locked)
                        World.Add(cameraEntityProxy.Object, new PointerLockIntention(true, true));

                    World.Set(cameraEntityProxy.Object, cursor);

                    menusAccessFacade.ShowGenericContextMenuAsync(parameter).Forget();
                }
            }
        }

        private void OnOutcomePerformed(int outcomeIndex, string interactingUserWalletAddress, Entity playerEntity)
        {
            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(interactingUserWalletAddress);

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

                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=#FF9933>MOVING --> TO INITIATOR</color>");
                Transform initiatorTransform = World.Get<CharacterTransform>(interaction.InitiatorEntity).Transform;

                World.Add(playerEntity, new MoveBeforePlayingSocialEmoteIntent(
                    initiatorTransform.position,
                    initiatorTransform.rotation,
                    interaction.InitiatorEntity,
                    new TriggerEmoteReactingToSocialEmoteIntent(
                        interaction.Emote.DTO.Metadata.id,
                        outcomeIndex,
                        interaction.InitiatorWalletAddress,
                        interaction.Id))
                );
            }
        }
    }
}
