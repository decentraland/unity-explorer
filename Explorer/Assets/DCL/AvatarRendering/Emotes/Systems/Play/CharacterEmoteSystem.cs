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
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
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
        // todo: use this to add nice Debug UI to trigger any emote?
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IScenesCache scenesCache;

        private readonly IEmoteStorage emoteStorage;
        private readonly EmotePlayer emotePlayer;
        private readonly IEmotesMessageBus messageBus;
        private readonly URN[] loadEmoteBuffer = new URN[1];
        private readonly bool localSceneDevelopment;

        private ReadOnlySpan<char> sceneEmotePrefixWithColon =>
            GetSceneEmoteFromRealmIntention.SCENE_EMOTE_PREFIX + ":";

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
            this.localSceneDevelopment = localSceneDevelopment;
            emotePlayer = new EmotePlayer(audioSource, legacyAnimationsEnabled: localSceneDevelopment || appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS));
        }

        protected override void Update(float t)
        {
            CancelEmotesQuery(World);
            CancelEmotesByTeleportIntentionQuery(World);
            CancelEmotesByMoveToWithDurationQuery(World);
            CancelEmotesByMovementInputQuery(World);
            ReplicateLoopingEmotesQuery(World);
            ConsumeEmoteIntentQuery(World, t);
            CancelEmotesByDeletionQuery(World);
            UpdateEmoteTagsQuery(World);
            DisableCharacterControllerQuery(World);
            DisableAnimatorWhenPlayingLegacyAnimationsQuery(World);
            CleanUpQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CancelEmotesByDeletion(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            StopEmote(ref emoteComponent, avatarView);
        }

        /// <summary>
        /// Stops emote playback whenever the teleport intent is present on the entity.
        /// Doesn't handle movePlayerTo calls.
        /// </summary>
        [Query]
        [All(typeof(PlayerTeleportIntent))]
        [None(typeof(CharacterEmoteIntent))]
        private void CancelEmotesByTeleportIntention(Entity entity, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            StopEmote(ref emoteComponent, avatarView);
        }

        /// <summary>
        /// Stops emote playback when smooth movement with duration is initiated.
        /// </summary>
        [Query]
        [All(typeof(PlayerMoveToWithDurationIntent))]
        [None(typeof(CharacterEmoteIntent))]
        private void CancelEmotesByMoveToWithDuration(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            StopEmote(ref emoteComponent, avatarView);
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
        private void CancelEmotes(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView, in Entity entity)
        {
            bool wantsToCancelEmote = emoteComponent.StopEmote;
            emoteComponent.StopEmote = false;

            EmoteReferences? emoteReference = emoteComponent.CurrentEmoteReference;
            if (!emoteReference) return;

            bool shouldCancelEmote = wantsToCancelEmote || World.Has<HiddenPlayerComponent>(entity);

            if (shouldCancelEmote)
            {
                StopEmote(ref emoteComponent, avatarView);
                return;
            }

            if (!emoteReference.legacy)
            {
                if (!emoteComponent.IsPlayingEmote)
                {
                    avatarView.ResetAnimatorTrigger(AnimationHashes.EMOTE_STOP);
                    return;
                }

                int animatorCurrentStateTag = avatarView.GetAnimatorCurrentStateTag();
                bool isOnAnotherTag = animatorCurrentStateTag != AnimationHashes.EMOTE && animatorCurrentStateTag != AnimationHashes.EMOTE_LOOP;

                if (isOnAnotherTag)
                    StopEmote(ref emoteComponent, avatarView);
            }
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

            StopEmote(ref emoteComponent, avatarView);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StopEmote(ref CharacterEmoteComponent emoteComponent, IAvatarView avatarView)
        {
            if (emoteComponent.CurrentEmoteReference == null) return;

            emotePlayer.Stop(emoteComponent.CurrentEmoteReference);

            // Create a clean slate for the animator before setting the stop trigger
            avatarView.ResetAnimatorTrigger(AnimationHashes.EMOTE);
            avatarView.ResetAnimatorTrigger(AnimationHashes.EMOTE_RESET);
            avatarView.SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);

            // See https://github.com/decentraland/unity-explorer/issues/4198
            // Some emotes changes the armature rotation, we need to restore it
            avatarView.ResetArmatureInclination();

            emoteComponent.Reset();
        }

        // This query takes care of consuming the CharacterEmoteIntent to trigger an emote
        [Query]
        [None(typeof(DeleteEntityIntention))]
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

                    emoteComponent.EmoteUrn = emoteId;
                    emoteComponent.Mask = emoteIntent.Mask;
                    StreamableLoadingResult<AudioClipData>? audioAssetResult = emote.AudioAssetResults[bodyShape];
                    AudioClip? audioClip = audioAssetResult?.Asset;

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
        private void ReplicateLoopingEmotes(ref CharacterEmoteComponent animationComponent, in IAvatarView avatarView)
        {
            int prevTag = animationComponent.CurrentAnimationTag;
            int currentTag = avatarView.GetAnimatorCurrentStateTag();

            if ((prevTag != AnimationHashes.EMOTE || currentTag != AnimationHashes.EMOTE_LOOP)
                && (prevTag != AnimationHashes.EMOTE_LOOP || currentTag != AnimationHashes.EMOTE)) return;

            messageBus.Send(animationComponent.EmoteUrn, true, animationComponent.Mask);
        }

        [Query]
        private void DisableCharacterController(ref CharacterController characterController, in CharacterEmoteComponent emoteComponent)
        {
            characterController.enabled = !emoteComponent.IsPlayingEmote;
        }

        [Query]
        private void DisableAnimatorWhenPlayingLegacyAnimations(in IAvatarView avatarView, in CharacterEmoteComponent emote)
        {
            if (emote.CurrentEmoteReference && emote.CurrentEmoteReference.legacy)
                avatarView.AvatarAnimator.enabled = false;
        }

        [Query]
        private void CleanUp(Profile profile, in DeleteEntityIntention deleteEntityIntention)
        {
            if (!deleteEntityIntention.DeferDeletion)
                messageBus.OnPlayerRemoved(profile.UserId);
        }

        private void CreateEmotePromise(URN urn, BodyShape bodyShape)
        {
            Debug.Log($"(Maurizio) CreateEmotePromise called with URN: {urn}, bodyShape: {bodyShape}");

            loadEmoteBuffer[0] = urn;

            if (TryParseSceneEmoteURN(urn, out string sceneId, out string emoteHash, out bool loop, out ISceneFacade? scene))
            {
                Debug.Log($"(Maurizio) Parsed scene emote URN. sceneKey='{sceneId}', emoteHash='{emoteHash}', loop='{loop}', localSceneDevelopment='{localSceneDevelopment}'");

                if (scene == null)
                {
                    Debug.Log($"(Maurizio) Failed to resolve sceneFacade for key '{sceneId}'");
                    return;
                }

                Debug.Log($"(Maurizio) Resolved sceneFacade for key '{sceneId}'. SceneEntityDefinition.id='{scene.SceneData.SceneEntityDefinition.id}' SceneShortInfo.Name='{scene.SceneData.SceneShortInfo.Name}'");

                // Local scene preview path, this is needed if a remote client plays a scene emote this client has not yet played
                if (localSceneDevelopment && TryResolveLocalSceneEmotePath(scene, emoteHash, out string emotePath))
                {
                    Debug.Log($"(Maurizio) Using local-scene emote load for hash '{emoteHash}' with path '{emotePath}'");

                    SceneEmoteFromLocalPromise.Create(World,
                        new GetSceneEmoteFromLocalSceneIntention(scene.SceneData, emotePath, emoteHash, bodyShape, loop),
                        PartitionComponent.TOP_PRIORITY);

                    return;
                }

                // Deployed scenes path (asset bundles)
                if (scene.SceneData.SceneEntityDefinition.assetBundleManifestVersion == null)
                {
                    Debug.Log($"(Maurizio) Scene '{scene.SceneData.SceneEntityDefinition.id}' has no assetBundleManifestVersion, skipping realm emote load");
                    return;
                }

                Debug.Log($"(Maurizio) Using realm emote load for sceneKey='{sceneId}', hash='{emoteHash}'");

                SceneEmoteFromRealmPromise.Create(World,
                    new GetSceneEmoteFromRealmIntention(sceneId, scene.SceneData.SceneEntityDefinition.assetBundleManifestVersion!, emoteHash, loop, bodyShape),
                    PartitionComponent.TOP_PRIORITY);
            }
            else
            {
                Debug.Log($"(Maurizio) URN '{urn}' is not a scene-emote URN, falling back to pointer-based emote load");

                EmotePromise.Create(World,
                    EmoteComponentsUtils.CreateGetEmotesByPointersIntention(bodyShape, loadEmoteBuffer),
                    PartitionComponent.TOP_PRIORITY);
            }
        }

        private bool TryParseSceneEmoteURN(URN urnToParse, out string sceneId, out string parsedEmoteHash, out bool parsedLoop, out ISceneFacade? resolvedScene)
        {
            sceneId = string.Empty;
            parsedEmoteHash = string.Empty;
            parsedLoop = false;
            resolvedScene = null;

            ReadOnlySpan<char> urnStr = urnToParse.ToString().AsSpan();

            if (urnStr.IsEmpty || !urnStr.StartsWith(sceneEmotePrefixWithColon, StringComparison.OrdinalIgnoreCase))
                return false;

            ReadOnlySpan<char> payload = urnStr.Slice(sceneEmotePrefixWithColon.Length);

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
            Debug.Log($"(Maurizio) TryResolveLocalSceneEmotePath: failed to find hash '{hash}' in SceneEntityDefinition.content (len={content.Length})");
            return false;
        }
    }
}
