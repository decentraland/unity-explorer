using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Load;
using DCL.AvatarRendering.Emotes.SocialEmotes;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Profiles;
using DCL.SocialEmotes;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using System;
using System.Runtime.CompilerServices;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using Utility.Animations;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
using SceneEmoteFromRealmPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetSceneEmoteFromRealmIntention>;

namespace DCL.AvatarRendering.Emotes.Play
{
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AvatarGroup))]
    [UpdateAfter(typeof(RemoteEmotesSystem))]
    [UpdateAfter(typeof(LoadEmotesByPointersSystem))]
    [UpdateBefore(typeof(ChangeCharacterPositionGroup))]
    [UpdateBefore(typeof(CleanUpGroup))]
    public partial class CharacterEmoteSystem : BaseUnityLoopSystem
    {
        // todo: use this to add nice Debug UI to trigger any emote?
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IScenesCache scenesCache;

        private readonly IEmoteStorage emoteStorage;
        private readonly EmotePlayer emotePlayer;
        private readonly IEmotesMessageBus messageBus;
        private readonly URN[] loadEmoteBuffer = new URN[1];

        public CharacterEmoteSystem(
            World world,
            IEmoteStorage emoteStorage,
            IEmotesMessageBus messageBus,
            AudioSource audioSource,
            IDebugContainerBuilder debugContainerBuilder,
            bool localSceneDevelopment,
            IAppArgs appArgs,
            IScenesCache scenesCache) : base(world)
        {
            this.messageBus = messageBus;
            this.emoteStorage = emoteStorage;
            this.debugContainerBuilder = debugContainerBuilder;
            this.scenesCache = scenesCache;
            emotePlayer = new EmotePlayer(audioSource, legacyAnimationsEnabled: localSceneDevelopment || appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS));
        }

        protected override void Update(float t)
        {
            CancelEmotesByTeleportIntentionQuery(World);
            ConsumeEmoteIntentQuery(World);
            ReplicateLoopingEmotesQuery(World);
            CancelEmotesByDeletionQuery(World);
            CancelEmotesByMovementQuery(World);
            CancelEmotesQuery(World);
            UpdateEmoteTagsQuery(World);
            DisableCharacterControllerQuery(World);
            CleanUpQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CancelEmotesByDeletion(Entity entity, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView, in Profile profile)
        {
            StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);
        }

        [Query]
        [All(typeof(PlayerTeleportIntent))]
        [None(typeof(CharacterEmoteIntent))]
        private void CancelEmotesByTeleportIntention(Entity entity, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView, in Profile profile)
        {
            StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);
        }

        // looping emotes and cancelling emotes by tag depend on tag change, this query alone is the one that updates that value at the ond of the update
        [Query]
        private void UpdateEmoteTags(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            emoteComponent.CurrentAnimationTag = avatarView.GetAnimatorCurrentStateTag();
        }

        // emotes that do not loop need to trigger some kind of cancellation, so we can take care of the emote props and sounds
        [Query]
        [None(typeof(CharacterEmoteIntent), typeof(DeleteEntityIntention))]
        private void CancelEmotes(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView, in Entity entity, in Profile profile)
        {
            bool wantsToCancelEmote = emoteComponent.StopEmote;
            emoteComponent.StopEmote = false;

            bool wasPlayingEmote = emoteComponent.CurrentAnimationTag == AnimationHashes.EMOTE || emoteComponent.CurrentAnimationTag == AnimationHashes.EMOTE_LOOP;

            if (!wasPlayingEmote)
            {
                avatarView.ResetTrigger(AnimationHashes.EMOTE_STOP);
                return;
            }

            EmoteReferences? emoteReference = emoteComponent.CurrentEmoteReference;

            if (emoteReference == null)
                return;

            if (wantsToCancelEmote || World.Has<BlockedPlayerComponent>(entity))
            {
                StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);
                return;
            }

            int animatorCurrentStateTag = avatarView.GetAnimatorCurrentStateTag();
            bool isOnAnotherTag = animatorCurrentStateTag != AnimationHashes.EMOTE && animatorCurrentStateTag != AnimationHashes.EMOTE_LOOP;

            if (isOnAnotherTag)
                StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);
        }

        // when moving or jumping we detect the emote cancellation, and we take care of getting rid of the emote props and sounds
        [Query]
        [None(typeof(CharacterEmoteIntent))]
        private void CancelEmotesByMovement(Entity entity, ref CharacterEmoteComponent emoteComponent, in CharacterRigidTransform rigidTransform, in IAvatarView avatarView, in Profile profile)
        {
            const float CUTOFF_LIMIT = 0.2f;

            float velocity = rigidTransform.MoveVelocity.Velocity.sqrMagnitude;
            float verticalVelocity = Mathf.Abs(rigidTransform.GravityVelocity.sqrMagnitude);

            bool canEmoteBeCancelled = velocity > CUTOFF_LIMIT || verticalVelocity > CUTOFF_LIMIT;

            if (!canEmoteBeCancelled) return;

            StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StopEmote(Entity entity, ref CharacterEmoteComponent emoteComponent, IAvatarView avatarView, string walletAddress)
        {
            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "-StopEmote- " + ((AvatarBase)avatarView).name);

            if (emoteComponent.CurrentEmoteReference == null)
                return;

            emotePlayer.Stop(emoteComponent.CurrentEmoteReference);
            emoteComponent.HasOutcomeAnimationStarted = false;

            if (emoteComponent.Metadata.IsSocialEmote)
            {
                SocialEmoteInteractionsManager.Instance.StopInteraction(walletAddress);

                if (emoteComponent.IsReactingToSocialEmote &&
                    World.TryGet(entity, out CharacterController? characterController))
                {
                    // Returns the receiver avatar in the social emote interaction to its original position
                    if (World.TryGet(entity, out MoveToInitiatorIntent intent))
                    {
                        // It has to be disabled, otherwise position will be overriden
                        characterController!.enabled = false;
                        characterController.transform.position = intent.OriginalPosition;
                        characterController.transform.rotation = intent.OriginalRotation;
                        characterController.enabled = true;
                        World.Remove<MoveToInitiatorIntent>(entity);
                    }
                }
            }

            // Create a clean slate for the animator before setting the stop trigger
            avatarView.ResetTrigger(AnimationHashes.EMOTE);
            avatarView.ResetTrigger(AnimationHashes.EMOTE_RESET);
            avatarView.SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);

            avatarView.RestoreArmatureName();

            emoteComponent.Reset();
        }

        // This query takes care of consuming the CharacterEmoteIntent to trigger an emote
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ConsumeEmoteIntent(Entity entity, ref CharacterEmoteComponent emoteComponent, in CharacterEmoteIntent emoteIntent,
            in IAvatarView avatarView, ref AvatarShapeComponent avatarShapeComponent, CharacterTransform characterTransform)
        {
            URN emoteId = emoteIntent.EmoteId;

            // it's very important to catch any exception here to avoid not consuming the emote intent, so we don't infinitely create props
            try
            {
                // we wait until the avatar finishes moving to trigger the emote,
                // avoid the case where: you stop moving, trigger the emote, the emote gets triggered and next frame it gets cancelled because inertia keeps moving the avatar
                //We also avoid triggering the emote while the character is jumping or landing, as the landing animation breaks the emote flow if they have props
                if (avatarView.IsAnimatorInTag(AnimationHashes.JUMPING_TAG) ||
                    avatarView.GetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND) > 0.1f)
                    return;

                if (emoteStorage.TryGetElement(emoteId.Shorten(), out IEmote emote))
                {
                    if (emote.IsLoading)
                        return;

                    // emote failed to load? remove intent
                    if (emote.Model is { IsInitialized: true, Succeeded: false })
                    {
                        ReportHub.LogError(GetReportData(), $"Cant play emote {emoteId} since it failed loading \n the DTO");
                        World.Remove<CharacterEmoteIntent>(entity);
                        return;
                    }

                    // emote failed to load? remove intent
                    if (emote.DTO.assetBundleManifestVersion is { assetBundleManifestRequestFailed: true } and { IsLSDAsset: false })
                    {
                        ReportHub.LogError(GetReportData(), $"Cant play emote {emoteId} since it failed loading the manifest");
                        World.Remove<CharacterEmoteIntent>(entity);
                        return;
                    }

                    BodyShape bodyShape = avatarShapeComponent.BodyShape;

                    //Loading not complete
                    if (emote.AssetResults[bodyShape] == null)
                        return;

                    StreamableLoadingResult<AttachmentRegularAsset> streamableAssetValue = emote.AssetResults[bodyShape].Value;
                    GameObject? mainAsset;

                    if (streamableAssetValue is { Succeeded: false } || (mainAsset = streamableAssetValue.Asset?.MainAsset) == null)
                    {
                        // We can't play emote, remove intent, otherwise there is no place to remove it
                        World.Remove<CharacterEmoteIntent>(entity);
                        return;
                    }

                    // Previous social emote interaction has to be stopped before starting a new one
                    // When the avatar is already playing a social emote (outcome phase) and then it plays the same one (start phase) it cancels the interaction
                    // Playing a different emote cancels the interaction
                    // If the emote is the same (excepting previous rules) it may be a loop animation and must NOT cancel the interaction
                    if (emoteComponent.Metadata != null &&
                        emoteComponent.Metadata.IsSocialEmote &&
                        emoteComponent.IsPlayingEmote &&
                        (emoteComponent.EmoteUrn.Shorten() != emoteIntent.EmoteId.Shorten() || (emoteComponent.IsPlayingSocialEmoteOutcome && !emoteIntent.UseSocialEmoteOutcomeAnimation))) // It's a different emote OR it was playing the outcome phase and now it wants to play the start phase of the same emote interaction (triggered the same social emote again while the previous interaction didn't finish yet, it cancels it)
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "DIFFERENT PHASE? " + (emoteComponent.IsPlayingSocialEmoteOutcome && !emoteIntent.UseSocialEmoteOutcomeAnimation));
                        SocialEmoteInteractionsManager.Instance.StopInteraction(emoteComponent.SocialEmoteInitiatorWalletAddress);
                    }

                    // Existing emoteComponent is overwritten with new emote info
                    emoteComponent.EmoteUrn = emoteId;
                    emoteComponent.Metadata = (EmoteDTO.EmoteMetadataDto)emote.DTO.Metadata;
                    StreamableLoadingResult<AudioClipData>? audioAssetResult = emote.AudioAssetResults[bodyShape];
                    AudioClip? audioClip = audioAssetResult?.Asset;

                    emoteComponent.IsPlayingSocialEmoteOutcome = emoteIntent.UseSocialEmoteOutcomeAnimation;
                    emoteComponent.CurrentSocialEmoteOutcome = emoteIntent.SocialEmoteOutcomeIndex;
                    emoteComponent.IsReactingToSocialEmote = emoteIntent.UseOutcomeReactionAnimation;

                    if (emoteComponent.Metadata.IsSocialEmote)
                    {
                        if (emoteComponent.IsPlayingSocialEmoteOutcome)
                        {
                            if (emoteComponent.IsReactingToSocialEmote)
                                SocialEmoteInteractionsManager.Instance.AddParticipantToInteraction(emoteIntent.WalletAddress, emoteComponent.CurrentSocialEmoteOutcome, emoteIntent.SocialEmoteInitiatorWalletAddress);

                            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "AUDIO for outcome " + emoteComponent.CurrentSocialEmoteOutcome);

                            audioClip = emote.SocialEmoteOutcomeAudioAssetResults[emoteComponent.CurrentSocialEmoteOutcome].Asset;
                        }
                        else // Starting interaction
                        {
                            SocialEmoteInteractionsManager.Instance.StartInteraction(emoteIntent.WalletAddress, emote, characterTransform.Transform);
                            emoteComponent.SocialEmoteInitiatorWalletAddress = emoteIntent.WalletAddress;
                        }
                    }

                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "PLAY USER: " + emoteIntent.WalletAddress);

                    if (!emotePlayer.Play(mainAsset, audioClip, emote.IsLooping(), emoteIntent.Spatial, in avatarView, ref emoteComponent))
                        ReportHub.LogError(ReportCategory.EMOTE, $"Emote name:{emoteId} cant be played.");

                    World.Remove<CharacterEmoteIntent>(entity);
                }
                else
                {
                    // Request the emote when not it cache. It will eventually endup in the emoteStorage so it can be played by this query
                    CreateEmotePromise(emoteId, avatarShapeComponent.BodyShape);
                }

            }
            catch (Exception e) { ReportHub.LogException(e, GetReportData()); }
        }

        // Every time the emote is looped we send a new message that should refresh the looping emotes on clients that didn't receive the initial message yet
        // TODO (Kinerius): This does not support scene emotes yet
        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(CharacterEmoteIntent))]
        private void ReplicateLoopingEmotes(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            // When the avatar that initiated the social emote interaction "reacts to the reaction" of the other avatar,
            // it must not send back the signal of starting the outcome animation, as it will be already animating in the other clients
            if(emoteComponent.IsPlayingSocialEmoteOutcome && !emoteComponent.IsReactingToSocialEmote)
                return;

            int prevTag = emoteComponent.CurrentAnimationTag;
            int currentTag = avatarView.GetAnimatorCurrentStateTag();

            if ((prevTag != AnimationHashes.EMOTE || currentTag != AnimationHashes.EMOTE_LOOP)
                && (prevTag != AnimationHashes.EMOTE_LOOP || currentTag != AnimationHashes.EMOTE)) return;

            messageBus.Send(emoteComponent.EmoteUrn, true, emoteComponent.IsPlayingSocialEmoteOutcome, emoteComponent.CurrentSocialEmoteOutcome, emoteComponent.IsReactingToSocialEmote, emoteComponent.SocialEmoteInitiatorWalletAddress);
        }

        [Query]
        private void CleanUp(Profile profile, in DeleteEntityIntention deleteEntityIntention)
        {
            if (!deleteEntityIntention.DeferDeletion)
                messageBus.OnPlayerRemoved(profile.UserId);
        }

        [Query]
        private void DisableCharacterController(ref CharacterController characterController, in CharacterEmoteComponent emoteComponent)
        {
            characterController.enabled = !emoteComponent.IsPlayingEmote;
        }

        private void CreateEmotePromise(URN urn, BodyShape bodyShape)
        {
            loadEmoteBuffer[0] = urn;

            if (GetSceneEmoteFromRealmIntention.TryParseFromURN(urn, out string sceneId, out string emoteHash, out bool loop))
            {
                if (!scenesCache.TryGetBySceneId(sceneId, out ISceneFacade? scene)) return;

                SceneEmoteFromRealmPromise.Create(World,
                    new GetSceneEmoteFromRealmIntention(sceneId, scene!.SceneData.SceneEntityDefinition.assetBundleManifestVersion!, emoteHash, loop, bodyShape),
                    PartitionComponent.TOP_PRIORITY);
            }
            else
                EmotePromise.Create(World,
                    EmoteComponentsUtils.CreateGetEmotesByPointersIntention(bodyShape, loadEmoteBuffer),
                    PartitionComponent.TOP_PRIORITY);
        }
    }
}
