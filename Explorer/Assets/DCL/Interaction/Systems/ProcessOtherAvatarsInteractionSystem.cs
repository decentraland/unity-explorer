using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Components;
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
using System;
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
        private const string EMOTE_TOOLTIP = "Interact: ";

        private readonly IEventSystem eventSystem;
        private readonly DCLInput dclInput;
        private readonly IMVCManagerMenusAccessFacade menusAccessFacade;
        private HoverFeedbackComponent.Tooltip viewProfileTooltip;
        private HoverFeedbackComponent.Tooltip socialEmoteInteractionTooltip;
        private Profile? currentProfileHovered;
        private readonly IMVCManager mvcManager;
        private Vector2? currentPositionHovered;
        private UniTaskCompletionSource contextMenuTask = new ();
        private readonly SocialEmoteOutcomeMenuController socialEmoteOutcomeMenuController;
        private readonly IWeb3IdentityCache identityCache;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly ObjectProxy<Entity> cameraEntityProxy;
        private readonly Entity playerEntity;

        private GenericContextMenu contextMenuConfiguration;

        class SocialEmoteOutcomesContextMenuSettings
        {
            [Header("Layout")]
            [SerializeField]
            private int width = 260;

            [SerializeField]
            private int elementsSpacing = 5;

            [SerializeField]
            private Vector2 offset = new (50, 0);

            [SerializeField]
            private RectOffset verticalLayoutPadding = new RectOffset(){left = 10, right = 10, top = 8, bottom = 16};

            public int Width => width;
            public int ElementsSpacing => elementsSpacing;
            public Vector2 Offset => offset;
            public RectOffset VerticalLayoutPadding => verticalLayoutPadding;
        }

        private ProcessOtherAvatarsInteractionSystem(
            World world,
            IEventSystem eventSystem,
            IMVCManagerMenusAccessFacade menusAccessFacade,
            IMVCManager mvcManager,
            SocialEmoteOutcomeMenuController socialEmoteOutcomeMenuController,
            IWeb3IdentityCache identityCache,
            ObjectProxy<Entity> cameraEntityProxy,
            Entity playerEntity) : base(world)
        {
            this.eventSystem = eventSystem;
            dclInput = DCLInput.Instance;
            this.menusAccessFacade = menusAccessFacade;
            this.mvcManager = mvcManager;
            this.socialEmoteOutcomeMenuController = socialEmoteOutcomeMenuController;
            this.identityCache = identityCache;
            this.cameraEntityProxy = cameraEntityProxy;
            this.playerEntity = playerEntity;

            dclInput.Player.Pointer!.performed += OnLeftClickPressed;
            dclInput.Player.RightPointer!.performed += OpenContextMenu;

            SocialEmoteOutcomesContextMenuSettings contextMenuSettings = new SocialEmoteOutcomesContextMenuSettings();

            contextMenuConfiguration = new GenericContextMenu(contextMenuSettings.Width,
                    contextMenuSettings.Offset,
                    contextMenuSettings.VerticalLayoutPadding,
                    contextMenuSettings.ElementsSpacing,
                    ContextMenuOpenDirection.CENTER_LEFT);
        }

        protected override void Update(float t)
        {
            ProcessRaycastResultQuery(World);
        }

        protected override void OnDispose()
        {
            cts.SafeCancelAndDispose();
            dclInput.Player.Pointer!.performed -= OnLeftClickPressed;
            dclInput.Player.RightPointer!.performed -= OpenContextMenu;
            contextMenuTask.TrySetResult();
        }

        private bool wasLeftClickPressed;

        private void OnLeftClickPressed(InputAction.CallbackContext obj)
        {
            wasLeftClickPressed = true;
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
            {
                if(socialEmoteOutcomeMenuController.State != ControllerState.ViewHiding && socialEmoteOutcomeMenuController.State != ControllerState.ViewHidden)
                    socialEmoteOutcomeMenuController.HideViewAsync(cts.Token).Forget();

                wasLeftClickPressed = false;
                return;
            }

            Entity entityRef = entityInfo.Value.EntityReference;

            if (!World.IsAlive(entityRef)
                || !World!.TryGet(entityRef, out Profile? profile)
                || World.Has<HiddenPlayerComponent>(entityRef)
                || World.Has<IgnoreInteractionComponent>(entityRef))
            {
                if(socialEmoteOutcomeMenuController.State != ControllerState.ViewHiding && socialEmoteOutcomeMenuController.State != ControllerState.ViewHidden)
                    socialEmoteOutcomeMenuController.HideViewAsync(cts.Token).Forget();

                wasLeftClickPressed = false;
                return;
            }

            currentPositionHovered = Mouse.current.position.ReadValue();
            currentProfileHovered = profile;
            hoverStateComponent.AssignCollider(raycastResultForGlobalEntities.Collider, true);

            if (wasLeftClickPressed)
            {
                wasLeftClickPressed = false;
                OpenEmoteOutcomeContextMenu(entityRef);
            }

            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? socialEmoteInteraction = SocialEmoteInteractionsManager.Instance.GetInteractionState(profile.UserId);

            if (socialEmoteInteraction is { AreInteracting: false } &&
                (string.IsNullOrEmpty(socialEmoteInteraction.TargetWalletAddress) || // Is not a directed emote
                    socialEmoteInteraction.TargetWalletAddress == identityCache.Identity!.Address)) // Is a directed emote and the target is the local player
            {
             /*   Vector3 otherPosition = World.Get<CharacterTransform>(entityRef).Position;
                Vector3 playerPosition = World.Get<CharacterTransform>(playerEntity).Position;

                const float MAX_SQR_DISTANCE_TO_INTERACT = 5.0f * 5.0f; // TODO: Move to a proper place
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
                }*/

                viewProfileTooltip = new HoverFeedbackComponent.Tooltip(OPTIONS_TOOLTIP, dclInput.Player.RightPointer);
                hoverFeedbackComponent.Add(viewProfileTooltip);
                socialEmoteInteractionTooltip = new HoverFeedbackComponent.Tooltip(EMOTE_TOOLTIP + socialEmoteInteraction.Emote.Model.Asset.metadata.name, dclInput.Player.Pointer);
                hoverFeedbackComponent.Add(socialEmoteInteractionTooltip);
            }
            else
            {
                // The initiator is probably interacting with another avatar so the menu should be hidden immediately
                if(socialEmoteOutcomeMenuController.State != ControllerState.ViewHiding && socialEmoteOutcomeMenuController.State != ControllerState.ViewHidden)
                    socialEmoteOutcomeMenuController.HideViewAsync(cts.Token).Forget();

                viewProfileTooltip = new HoverFeedbackComponent.Tooltip(OPTIONS_TOOLTIP, dclInput.Player.Pointer);
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

            contextMenuTask.TrySetResult();
            contextMenuTask = new UniTaskCompletionSource();
            menusAccessFacade.ShowUserProfileContextMenuFromWalletIdAsync(new Web3Address(userId), currentPositionHovered!.Value, new Vector2(50, 0), CancellationToken.None, contextMenuTask.Task, anchorPoint: MenuAnchorPoint.CENTER_RIGHT, enableSocialEmotes: true);
        }

        private void OpenEmoteOutcomeContextMenu(Entity entityRef)
        {
            string userId = currentProfileHovered.UserId;

            if (string.IsNullOrEmpty(userId))
                return;

            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(currentProfileHovered.UserId);

            if (interaction is { AreInteracting: false } &&
                (string.IsNullOrEmpty(interaction.TargetWalletAddress) || // Is not a directed emote
                    interaction.TargetWalletAddress == identityCache.Identity!.Address)) // Is a directed emote and the target is the local player
            {
                Vector3 otherPosition = World.Get<CharacterTransform>(entityRef).Position;
                Vector3 playerPosition = World.Get<CharacterTransform>(playerEntity).Position;

                const float MAX_SQR_DISTANCE_TO_INTERACT = 5.0f * 5.0f; // TODO: Move to a proper place
                float sqrDistanceToAvatar = (otherPosition - playerPosition).sqrMagnitude;

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
                        contextMenuConfiguration.AddControl(new SimpleButtonContextMenuControlSettings(outcomes[i].title,
                                                            () => OnOutcomePerformed(outcomeIndex, initiatorWalletAddress, playerEntity)));
                    }

                    contextMenuTask.TrySetResult();
                    contextMenuTask = new UniTaskCompletionSource();

                    GenericContextMenuParameter parameter = new GenericContextMenuParameter(
                        contextMenuConfiguration,
                        currentPositionHovered!.Value,
                        closeTask: contextMenuTask.Task
                    );

                    menusAccessFacade.ShowGenericContextMenuAsync(parameter).Forget();

                    // Unlocks the camera when showing the outcomes context menu
                    World.Add(cameraEntityProxy.Object, new PointerLockIntention(false));
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
