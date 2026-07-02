using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Load;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.Multiplayer.Movement;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Emotes;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility.Animations;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
using SceneEmoteFromRealmPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetSceneEmoteFromRealmIntention>;
using SceneEmoteFromLocalPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetSceneEmoteFromLocalSceneIntention>;

namespace DCL.AvatarRendering.Emotes.Play
{
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AvatarGroup))]
    [UpdateAfter(typeof(RemoteEmotesSystem))]
    [UpdateAfter(typeof(LoadEmotesByPointersSystem))]
    [UpdateBefore(typeof(ChangeCharacterPositionGroup))]
    public partial class CharacterEmoteSystem : BaseUnityLoopSystem
    {
        private static readonly string SCENE_EMOTE_PREFIX_WITH_COLON = GetSceneEmoteFromRealmIntention.SCENE_EMOTE_PREFIX + ":";

        // todo: use this to add nice Debug UI to trigger any emote?
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IScenesCache scenesCache;

        private readonly IEmoteStorage emoteStorage;
        private readonly EmotePlayer emotePlayer;
        private readonly IEmotesMessageBus messageBus;
        private readonly URN[] loadEmoteBuffer = new URN[1];
        private readonly bool localSceneDevelopment;

        public CharacterEmoteSystem(
            World world,
            IEmoteStorage emoteStorage,
            IEmotesMessageBus messageBus,
            EmotePlayer emotePlayer,
            IDebugContainerBuilder debugContainerBuilder,
            bool localSceneDevelopment,
            IScenesCache scenesCache) : base(world)
        {
            this.messageBus = messageBus;
            this.emoteStorage = emoteStorage;
            this.emotePlayer = emotePlayer;
            this.debugContainerBuilder = debugContainerBuilder;
            this.scenesCache = scenesCache;
            this.localSceneDevelopment = localSceneDevelopment;
        }

        protected override void Update(float t)
        {
            CancelSceneEmotesBySceneChangeQuery(World);
            CancelEmotesQuery(World);
            CancelRemoteMaskedEmotesQuery(World);
            CancelEmotesByTeleportIntentionQuery(World);
            CancelEmotesByMoveToWithDurationQuery(World);
            CancelEmotesByMovementInputQuery(World);
            ReplicateLoopingEmotesQuery(World);
            ConsumeEmoteIntentQuery(World, t);
            BroadcastEmoteOnLocalPlayerQuery(World);
            DiscardEmoteBroadcastOnRemotePlayersQuery(World);
            CancelEmotesByDeletionQuery(World);
            CancelMaskedEmotesByDeletionQuery(World);
            UpdateEmoteTagsQuery(World);
            UpdateRemoteMaskedEmoteTagsQuery(World);
            DisableCharacterControllerQuery(World);
            CleanUpQuery(World);
        }

        /// <summary>
        /// Stops scene emotes when the player is no longer in the scene that triggered them.
        /// </summary>
        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void CancelSceneEmotesBySceneChange(Entity entity, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            if (emoteComponent.CurrentEmoteReference == null) return;
            if (!TryParseSceneEmoteURN(emoteComponent.EmoteUrn, out _, out _, out _, out ISceneFacade? emoteScene)) return;
            if (emoteScene != null && emoteScene == scenesCache.CurrentScene.Value) return;

            StopEmote(entity, ref emoteComponent, avatarView);
            messageBus.SendStop();
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CancelEmotesByDeletion(Entity entity, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView) =>
            StopEmote(entity, ref emoteComponent, avatarView);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CancelMaskedEmotesByDeletion(ref CharacterMaskedEmoteComponent masked, in IAvatarView avatarView)
        {
            if (masked.CurrentEmoteReference == null) return;

            // Force-stop regardless of animator state — the entity is being destroyed.
            masked.StopEmote = true;
            emotePlayer.TryCancelMaskedEmote(ref masked, avatarView);
        }

        /// <summary>
        /// Stops emote playback whenever the teleport intent is present on the entity.
        /// Doesn't handle movePlayerTo calls.
        /// </summary>
        [Query]
        [All(typeof(PlayerTeleportIntent))]
        [None(typeof(CharacterEmoteIntent))]
        private void CancelEmotesByTeleportIntention(Entity entity, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView) =>
            StopEmote(entity, ref emoteComponent, avatarView);

        /// <summary>
        /// Stops emote playback when smooth movement with duration is initiated.
        /// </summary>
        [Query]
        [All(typeof(PlayerMoveToWithDurationIntent))]
        [None(typeof(CharacterEmoteIntent))]
        private void CancelEmotesByMoveToWithDuration(Entity entity, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView) =>
            StopEmote(entity, ref emoteComponent, avatarView);

        // looping emotes and cancelling emotes by tag depend on tag change, this query alone is the one that updates that value at the ond of the update
        [Query]
        private void UpdateEmoteTags(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            int currentStateTag = avatarView.GetAnimatorCurrentStateTag(AnimatorEmoteLayers.BASE_LAYER);
            emoteComponent.SetAnimationTag(currentStateTag);
        }

        // emotes that do not loop need to trigger some kind of cancellation, so we can take care of the emote props and sounds
        [Query]
        [None(typeof(CharacterEmoteIntent), typeof(DeleteEntityIntention))]
        private void CancelEmotes(Entity entity, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            bool wantsToCancelEmote = emoteComponent.StopEmote;
            emoteComponent.StopEmote = false;

            EmoteReferences? emoteReference = emoteComponent.CurrentEmoteReference;
            if (!emoteReference) return;

            bool shouldCancelEmote = wantsToCancelEmote || World.Has<HiddenPlayerComponent>(entity);
            if (shouldCancelEmote)
            {
                StopEmote(entity, ref emoteComponent, avatarView);
                return;
            }

            // Tear down only when the legacy Animation component has stopped on its own (non-looping).
            if (emoteReference.legacy)
            {
                if (!avatarView.IsLegacyAnimationPlaying)
                    StopEmote(entity, ref emoteComponent, avatarView);
                return;
            }

            if (!emoteComponent.IsPlayingEmote)
            {
                avatarView.ResetAnimatorTrigger(AnimationHashes.EMOTE_STOP);
                return;
            }

            int animatorCurrentStateTag = avatarView.GetAnimatorCurrentStateTag(AnimatorEmoteLayers.BASE_LAYER);
            bool isOnAnotherTag = animatorCurrentStateTag != AnimationHashes.EMOTE && animatorCurrentStateTag != AnimationHashes.EMOTE_LOOP;

            if (isOnAnotherTag)
                StopEmote(entity, ref emoteComponent, avatarView);
        }

        // Related issues:
        // https://github.com/decentraland/unity-explorer/issues/6246
        // https://github.com/decentraland/unity-explorer/issues/4306
        // Following how it works on alternative clients, we should only cancel emotes given on the user's input
        // and not by the physics velocity. This prevents undesired interruptions like movePlayerTo + triggerEmote simultaneously
        // or random jumps due to physics imprecision
        // This is a base on which we can keep growing how scenes may interact with the avatar's emotes
        [Query]
        [None(typeof(CharacterEmoteIntent), typeof(PlayerTeleportIntent.JustTeleported))]
        private void CancelEmotesByMovementInput(
            Entity entity,
            ref CharacterEmoteComponent emoteComponent,
            in IAvatarView avatarView,
            ref JumpInputComponent jumpInputComponent,
            ref MovementInputComponent movementInputComponent,
            in Profile profile)
        {
            if (!emoteComponent.IsPlayingEmote) return;

            const float HORIZONTAL_THRESHOLD_SQ = 0.1f * 0.1f;

            float horizontalSpeedSq = movementInputComponent.Axes.sqrMagnitude;
            bool shouldCancelEmote = horizontalSpeedSq > HORIZONTAL_THRESHOLD_SQ || jumpInputComponent.IsPressed;

            if (!shouldCancelEmote) return;

            ReportHub.Log(ReportCategory.EMOTE, $"CancelEmotesByMovementInput() {profile.UserId} Stopping emote");

            StopEmote(entity, ref emoteComponent, avatarView);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StopEmote(Entity entity, ref CharacterEmoteComponent emoteComponent, IAvatarView avatarView)
        {
            if (emoteComponent.CurrentEmoteReference == null) return;

            emotePlayer.Stop(emoteComponent.CurrentEmoteReference);

            // Legacy emotes keep the Mecanim animator disabled while playing
            avatarView.StopLegacyAnimation();

            // Create a clean slate for the animator before setting the stop trigger
            avatarView.ResetAnimatorTrigger(AnimationHashes.EMOTE);
            avatarView.ResetAnimatorTrigger(AnimationHashes.EMOTE_RESET);
            avatarView.SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);

            // See https://github.com/decentraland/unity-explorer/issues/4198
            // Some emotes changes the armature rotation, we need to restore it
            avatarView.ResetArmatureInclination();

            // Propagate emote stop only for local player
            if (World.Has<PlayerComponent>(entity))
                messageBus.SendStop();

            emoteComponent.Reset();
        }

        /// <summary>
        /// Handles cancellation of masked emotes on remote player entities in the global world.
        /// Local player masked emotes are handled by SceneMaskedEmoteSystem in each scene world.
        /// </summary>
        [Query]
        [None(typeof(CharacterEmoteIntent), typeof(DeleteEntityIntention), typeof(PlayerComponent))]
        private void CancelRemoteMaskedEmotes(ref CharacterMaskedEmoteComponent masked, in IAvatarView avatarView) =>
            emotePlayer.TryCancelMaskedEmote(ref masked, avatarView);

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdateRemoteMaskedEmoteTags(ref CharacterMaskedEmoteComponent masked, in IAvatarView avatarView) =>
            EmotePlayer.UpdateMaskedEmoteTag(ref masked, avatarView);

        // This query takes care of consuming the CharacterEmoteIntent to trigger an emote
        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PlayerTeleportIntent.JustTeleported))]
        private void ConsumeEmoteIntent([Data] float dt, Entity entity,
            ref CharacterEmoteComponent emoteComponent,
            ref CharacterEmoteIntent emoteIntent,
            in IAvatarView avatarView,
            ref AvatarShapeComponent avatarShapeComponent)
        {
            URN emoteId = emoteIntent.EmoteId;

            // it's very important to catch any exception here to avoid not consuming the emote intent, so we don't infinitely create props
            try
            {
                // we wait until the avatar finishes moving to trigger the emote,
                // avoid the case where: you stop moving, trigger the emote, the emote gets triggered and next frame it gets cancelled because inertia keeps moving the avatar
                //We also avoid triggering the emote while the character is jumping or landing, as the landing animation breaks the emote flow if they have props
                if (emoteIntent.Mask == AvatarEmoteMask.AemFullBody &&
                    (avatarView.IsAnimatorInTag(AnimationHashes.JUMPING_TAG) ||
                     !avatarView.GetAnimatorBool(AnimationHashes.GROUNDED) ||
                     avatarView.GetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND) > 0.1f))
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

                    // Fixes https://github.com/decentraland/unity-explorer/issues/6531
                    // Rarely happens for an unknown reason that emote.AssetResults[bodyShape] is null, provoking the emote intent to never finish,
                    // thus props of the previous emote cannot be disposed either.
                    // By setting a timeout we force unstuck the process
                    if (emoteIntent.UpdatePlayTimeout(dt))
                    {
                        ReportHub.LogError(GetReportData(), $"Cant play emote {emoteId} timeout reached.");
                        World.Remove<CharacterEmoteIntent>(entity);
                        return;
                    }

                    BodyShape bodyShape = avatarShapeComponent.BodyShape;

                    // Loading not complete
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

                    StreamableLoadingResult<AudioClipData>? audioAssetResult = emote.AudioAssetResults[bodyShape];
                    AudioClip? audioClip = audioAssetResult?.Asset;

                    // Capture intent and view values before any structural change that invalidate query-provided refs.
                    AvatarEmoteMask mask = emoteIntent.Mask;
                    bool spatial = emoteIntent.Spatial;
                    IAvatarView view = avatarView;

                    bool isLooping = emote.IsLooping();

                    if (mask == AvatarEmoteMask.AemFullBody)
                    {
                        // A masked emote may still be playing on a separate animator layer (remote players).
                        // EmoteStart replaces the current emote, so the full-body emote supersedes it —
                        // mirrors the masked branch below, which stops a playing full-body emote.
                        ref CharacterMaskedEmoteComponent masked = ref World.TryGetRef<CharacterMaskedEmoteComponent>(entity, out bool hasMasked);

                        if (hasMasked && masked.CurrentEmoteReference != null)
                        {
                            masked.StopEmote = true;
                            emotePlayer.TryCancelMaskedEmote(ref masked, view);
                        }

                        emoteComponent.EmoteUrn = emoteId;
                        emoteComponent.Mask = mask;

                        if (!emotePlayer.Play(mainAsset, audioClip, isLooping, spatial, in view, ref emoteComponent))
                            ReportHub.LogError(ReportCategory.EMOTE, $"Emote name:{emoteId} cant be played.");
                        else
                        {
                            uint durationMs = !isLooping ? (uint)(emoteComponent.PlayingEmoteDuration * 1000) : 0;
                            World.Add(entity, new EmotePendingToBroadcast { EmoteId = emoteId, DurationMs = durationMs, Mask = mask});
                        }
                    }
                    else
                    {
                        // Masked emotes for remote players are handled here in the global world.
                        // Local player masked emotes go through SceneMaskedEmoteSystem instead.
                        if (emoteComponent.CurrentEmoteReference != null)
                            StopEmote(entity, ref emoteComponent, view);

                        // After AddOrGet the query-provided refs (emoteIntent, avatarView,
                        // emoteComponent) are potentially dangling, use only local copies.
                        ref CharacterMaskedEmoteComponent masked = ref World.AddOrGet<CharacterMaskedEmoteComponent>(entity);

                        masked.EmoteUrn = emoteId;
                        masked.Mask = mask;

                        if (!emotePlayer.PlayMasked(mainAsset, audioClip, isLooping, spatial, in view, ref masked))
                            ReportHub.LogError(ReportCategory.EMOTE, $"Emote name:{emoteId} cant be played.");
                        else
                        {
                            uint durationMs = !isLooping ? (uint)(emoteComponent.PlayingEmoteDuration * 1000) : 0;
                            World.Add(entity, new EmotePendingToBroadcast { EmoteId = emoteId, DurationMs = durationMs, Mask = mask});
                        }
                    }

                    World.Remove<CharacterEmoteIntent>(entity);
                }
                else
                    // Request the emote when not it cache. It will eventually endup in the emoteStorage so it can be played by this query
                    CreateEmotePromise(emoteId, avatarShapeComponent.BodyShape, emoteIntent.Mask);
            }
            catch (Exception e) { ReportHub.LogException(e, GetReportData()); }
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void BroadcastEmoteOnLocalPlayer(
            in Entity entity,
            ref EmotePendingToBroadcast broadcast,
            in PlayerMovementNetworkComponent playerMovement,
            in CharacterAnimationComponent animation,
            in StunComponent stun,
            in MovementInputComponent input,
            in HeadIKComponent headIK,
            in HandPointAtComponent pointAt)
        {
            var playerState = new NetworkMovementMessage
            {
                timestamp = UnityEngine.Time.unscaledTime,
                position = playerMovement.Character.transform.position,
                velocity = playerMovement.Character.velocity,
                velocitySqrMagnitude = playerMovement.Character.velocity.sqrMagnitude,
                rotationY = playerMovement.Character.transform.eulerAngles.y,
                isEmoting = true,
                isInstant = true,
                isStunned = stun.IsStunned,
                isSliding = animation.States.IsSliding,
                isPointingAt = pointAt.IsPointing,
                pointAtWorldHitPoint = pointAt.WorldHitPoint,
                headIKYawEnabled = headIK.YawEnabled,
                headIKPitchEnabled = headIK.PitchEnabled,
                headYawAndPitch = headIK.GetHeadYawAndPitch(),
                movementKind = input.Kind,
                animState = new AnimationStates
                {
                    IsSliding = animation.States.IsSliding,
                    IsGrounded = animation.States.IsGrounded,
                    JumpCount = animation.States.JumpCount,
                    IsLongJump = animation.States.IsLongJump,
                    IsFalling = animation.States.IsFalling,
                    IsLongFall = animation.States.IsLongFall,
                    IsStunned = stun.IsStunned,
                    GlideState = animation.States.GlideState,
                    SlideBlendValue = animation.States.SlideBlendValue,
                    MovementBlendValue = animation.States.MovementBlendValue,
                },
            };

            URN emoteId = broadcast.EmoteId;
            uint durationMs = broadcast.DurationMs;
            AvatarEmoteMask mask = broadcast.Mask;

            World.Remove<EmotePendingToBroadcast>(entity);

            messageBus.Send(emoteId, false, mask, durationMs, playerState);
        }

        [Query]
        [None(typeof(PlayerComponent))]
        [All(typeof(EmotePendingToBroadcast))]
        private void DiscardEmoteBroadcastOnRemotePlayers(Entity entity)
        {
            World.Remove<EmotePendingToBroadcast>(entity);
        }

        // Every time the emote is looped we send a new message that should refresh the looping emotes on clients that didn't receive the initial message yet
        // TODO: we can safely remove this propagation for pulse multiplayer as it is no longer needed (based on emote start/emote stop events)
        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(CharacterEmoteIntent))]
        private void ReplicateLoopingEmotes(ref CharacterEmoteComponent animationComponent, in IAvatarView avatarView)
        {
            int prevTag = animationComponent.CurrentAnimationTag;
            if (prevTag == 0) return;

            int currentTag = avatarView.GetAnimatorCurrentStateTag(AnimatorEmoteLayers.BASE_LAYER);

            if ((prevTag != AnimationHashes.EMOTE || currentTag != AnimationHashes.EMOTE_LOOP)
                && (prevTag != AnimationHashes.EMOTE_LOOP || currentTag != AnimationHashes.EMOTE)) return;

            messageBus.Send(animationComponent.EmoteUrn, true, animationComponent.Mask);
        }

        [Query]
        private void DisableCharacterController(ref CharacterController characterController, in CharacterEmoteComponent emoteComponent) =>
            characterController.enabled = !emoteComponent.IsPlayingEmote;

        [Query]
        private void CleanUp(Profile profile, in DeleteEntityIntention deleteEntityIntention)
        {
            if (!deleteEntityIntention.DeferDeletion)
                messageBus.OnPlayerRemoved(profile.UserId);
        }

        private void CreateEmotePromise(URN urn, BodyShape bodyShape, AvatarEmoteMask mask)
        {
            loadEmoteBuffer[0] = urn;

            if (TryParseSceneEmoteURN(urn, out string sceneId, out string emoteHash, out bool loop, out ISceneFacade? scene))
            {
                if (scene == null)
                    return;

                // Local scene preview path, this is needed if a remote client plays a scene emote this client has not yet played
                if (localSceneDevelopment && TryResolveLocalSceneEmotePath(scene, emoteHash, out string emotePath))
                {
                    SceneEmoteFromLocalPromise.Create(World,
                        new GetSceneEmoteFromLocalSceneIntention(scene.SceneData, emotePath, emoteHash, bodyShape, loop, mask),
                        PartitionComponent.TOP_PRIORITY);

                    return;
                }

                // Deployed scenes path (asset bundles)
                if (scene.SceneData.SceneEntityDefinition.assetBundleManifestVersion == null)
                    return;

                SceneEmoteFromRealmPromise.Create(World,
                    new GetSceneEmoteFromRealmIntention(sceneId, scene.SceneData.SceneEntityDefinition.assetBundleManifestVersion!, emoteHash, loop, bodyShape),
                    PartitionComponent.TOP_PRIORITY);
            }
            else
                EmotePromise.Create(World,
                    EmoteComponentsUtils.CreateGetEmotesByPointersIntention(bodyShape, loadEmoteBuffer),
                    PartitionComponent.TOP_PRIORITY);
        }

        private bool TryParseSceneEmoteURN(URN urnToParse, out string sceneId, out string parsedEmoteHash, out bool parsedLoop, out ISceneFacade? resolvedScene)
        {
            sceneId = string.Empty;
            parsedEmoteHash = string.Empty;
            parsedLoop = false;
            resolvedScene = null;

            ReadOnlySpan<char> urnStr = urnToParse.ToString().AsSpan();
            ReadOnlySpan<char> emotePrefixWithColon = (ReadOnlySpan<char>)SCENE_EMOTE_PREFIX_WITH_COLON;

            if (urnStr.IsEmpty || !urnStr.StartsWith(emotePrefixWithColon, StringComparison.OrdinalIgnoreCase))
                return false;

            ReadOnlySpan<char> payload = urnStr.Slice(emotePrefixWithColon.Length);

            // Parse loop from the right-most "-{bool}" segment
            int lastDash = payload.LastIndexOf('-');

            if (lastDash <= 0 || lastDash == payload.Length - 1)
                return false;

            ReadOnlySpan<char> loopSpan = payload.Slice(lastDash + 1);

            if (!bool.TryParse(loopSpan, out parsedLoop))
                return false;

            ReadOnlySpan<char> payloadWithoutLoop = payload.Slice(0, lastDash);

            // sceneId and emoteHash can contain '-' in local preview,
            // so we can't just split by "last two dashes". Instead, match against loaded scenes by prefix.
            foreach (ISceneFacade facade in scenesCache.Scenes)
            {
                string candidateName = facade.SceneData.SceneShortInfo.Name;

                if (candidateName.Length == 0)
                    continue;

                ReadOnlySpan<char> candidatePrefix = (candidateName + "-").AsSpan();

                if (payloadWithoutLoop.StartsWith(candidatePrefix, StringComparison.Ordinal))
                {
                    sceneId = candidateName;
                    parsedEmoteHash = payloadWithoutLoop.Slice(candidatePrefix.Length).ToString();
                    resolvedScene = facade;
                    return true;
                }
            }

            // Fallback: assume "{sceneKey}-{emoteHash}" split by first dash.
            // This is primarily for deployed realm format where scene id has no '-'.
            int firstDash = payloadWithoutLoop.IndexOf('-');

            if (firstDash > 0 && firstDash < payloadWithoutLoop.Length - 1)
            {
                sceneId = payloadWithoutLoop.Slice(0, firstDash).ToString();
                parsedEmoteHash = payloadWithoutLoop.Slice(firstDash + 1).ToString();
                scenesCache.TryGetBySceneId(sceneId, out resolvedScene);
                return true;
            }

            return false;
        }

        private bool TryResolveLocalSceneEmotePath(ISceneFacade sceneFacade, string hash, out string emotePath)
        {
            var content = sceneFacade.SceneData.SceneEntityDefinition.content;

            for (var i = 0; i < content.Length; i++)
            {
                if (content[i].hash.Equals(hash, StringComparison.Ordinal))
                {
                    emotePath = content[i].file;
                    return true;
                }
            }

            emotePath = string.Empty;
            return false;
        }
    }
}
