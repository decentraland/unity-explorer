using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility.Animations;

namespace DCL.AvatarRendering.Emotes.Play
{
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    public partial class SceneMaskedEmoteSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly World globalWorld;
        private readonly Entity globalPlayerEntity;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly ObjectProxy<EmotePlayer> emotePlayerProxy;
        private readonly ObjectProxy<IEmoteStorage> emoteStorageProxy;
        private readonly ObjectProxy<IEmotesMessageBus> messageBusProxy;
        private readonly ISceneStateProvider sceneStateProvider;

        // Cached per-frame to avoid repeated proxy dereferences across queries
        private IAvatarView cachedAvatarView;
        private EmotePlayer cachedEmotePlayer;
        private IEmoteStorage cachedEmoteStorage;
        private IEmotesMessageBus cachedMessageBus;

        internal SceneMaskedEmoteSystem(
            World world,
            World globalWorld,
            Entity globalPlayerEntity,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            ObjectProxy<EmotePlayer> emotePlayerProxy,
            ObjectProxy<IEmoteStorage> emoteStorageProxy,
            ObjectProxy<IEmotesMessageBus> messageBusProxy,
            ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.emotePlayerProxy = emotePlayerProxy;
            this.emoteStorageProxy = emoteStorageProxy;
            this.messageBusProxy = messageBusProxy;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            if (!mainPlayerAvatarBaseProxy.Configured || !emotePlayerProxy.Configured
                || !emoteStorageProxy.Configured || !messageBusProxy.Configured) return;

            // Cache proxy objects once per frame
            cachedAvatarView = mainPlayerAvatarBaseProxy.Object!;
            cachedEmotePlayer = emotePlayerProxy.Object!;
            cachedEmoteStorage = emoteStorageProxy.Object!;
            cachedMessageBus = messageBusProxy.Object!;

            CancelMaskedEmotesQuery(World);
            ConsumeMaskedEmoteIntentQuery(World, t);
            UpdateMaskedEmoteVisibilityQuery(World);
            UpdateMaskedEmoteTagsQuery(World);
        }

        [Query]
        [All(typeof(CharacterMaskedEmoteComponent))]
        private void FinalizeComponents(ref CharacterMaskedEmoteComponent masked)
        {
            if (masked.CurrentEmoteReference != null)
            {
                TryStopMaskedEmote(ref masked);
                messageBusProxy.Object?.SendStop();
            }

            masked.Reset();
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }

        [Query]
        private void CancelMaskedEmotes(ref CharacterMaskedEmoteComponent masked)
        {
            if (masked.StopEmote)
            {
                TryStopMaskedEmote(ref masked, permanent: true);
                return;
            }

            if (masked.CurrentEmoteReference == null) return;
            if (!masked.IsPlaying) return;

            string layer = AnimatorEmoteLayers.GetFromEmoteMask(masked.Mask);
            int currentTag = cachedAvatarView.GetAnimatorCurrentStateTag(layer);
            bool isOnAnotherTag = currentTag != AnimationHashes.MASKED_EMOTE && currentTag != AnimationHashes.MASKED_EMOTE_LOOP;

            if (isOnAnotherTag)
                TryStopMaskedEmote(ref masked);
        }

        [Query]
        private void ConsumeMaskedEmoteIntent([Data] float dt, Entity entity,
            ref CharacterMaskedEmoteComponent masked,
            ref CharacterEmoteIntent emoteIntent)
        {
            URN emoteId = emoteIntent.EmoteId;

            try
            {
                if (!cachedEmoteStorage.TryGetElement(emoteId.Shorten(), out IEmote emote)) return;

                if (emote.IsLoading)
                    return;

                if (emote.Model is { IsInitialized: true, Succeeded: false })
                {
                    World.Remove<CharacterEmoteIntent>(entity);
                    return;
                }

                if (emoteIntent.UpdatePlayTimeout(dt))
                {
                    ReportHub.LogError(GetReportData(), $"Cant play masked emote {emoteId} timeout reached.");
                    World.Remove<CharacterEmoteIntent>(entity);
                    return;
                }

                if (!globalWorld.TryGet(globalPlayerEntity, out AvatarShapeComponent avatarShapeComponent))
                {
                    World.Remove<CharacterEmoteIntent>(entity);
                    return;
                }

                BodyShape bodyShape = avatarShapeComponent.BodyShape;

                if (emote.AssetResults[bodyShape] == null)
                    return;

                StreamableLoadingResult<AttachmentRegularAsset> streamableAssetValue = emote.AssetResults[bodyShape].Value;
                GameObject? mainAsset;

                if (streamableAssetValue is { Succeeded: false } || (mainAsset = streamableAssetValue.Asset?.MainAsset) == null)
                {
                    World.Remove<CharacterEmoteIntent>(entity);
                    return;
                }

                StreamableLoadingResult<AudioClipData>? audioAssetResult = emote.AudioAssetResults[bodyShape];
                AudioClip? audioClip = audioAssetResult?.Asset;

                // Stop any full-body emote that's playing on the global player
                if (globalWorld.TryGet(globalPlayerEntity, out CharacterEmoteComponent emoteComponent) && emoteComponent.CurrentEmoteReference != null)
                {
                    emoteComponent.StopEmote = true;
                    globalWorld.Set(globalPlayerEntity, emoteComponent);
                }

                // Stop previous masked emote if one exists
                if (masked.CurrentEmoteReference != null)
                    TryStopMaskedEmote(ref masked);

                masked.EmoteUrn = emoteId;
                masked.Mask = emoteIntent.Mask;

                if (!cachedEmotePlayer.PlayMasked(mainAsset, audioClip, emote.IsLooping(), emoteIntent.Spatial, in cachedAvatarView, ref masked))
                    ReportHub.LogError(ReportCategory.EMOTE, $"Emote name:{emoteId} cant be played.");

                cachedMessageBus.Send(emoteId, emote.IsLooping(), emoteIntent.Mask);

                World.Remove<CharacterEmoteIntent>(entity);
            }
            catch (Exception e) { ReportHub.LogException(e, GetReportData()); }
        }

        [Query]
        private void UpdateMaskedEmoteVisibility(ref CharacterMaskedEmoteComponent masked)
        {
            if (masked.EmoteUrn.IsNullOrEmpty()) return;

            bool isInScene = sceneStateProvider.IsCurrent;
            bool fullBodyIsPlaying = globalWorld.TryGet(globalPlayerEntity, out CharacterEmoteComponent ec)
                && ec.CurrentEmoteReference != null;

            bool shouldPlay = isInScene && !fullBodyIsPlaying;

            if (shouldPlay && masked.CurrentEmoteReference == null)
            {
                ReplayMaskedEmote(ref masked);
            }
            else if (!shouldPlay && masked.CurrentEmoteReference != null)
            {
                TryStopMaskedEmote(ref masked);
                cachedMessageBus.SendStop();
            }
        }

        private void ReplayMaskedEmote(ref CharacterMaskedEmoteComponent masked)
        {
            if (!cachedEmoteStorage.TryGetElement(masked.EmoteUrn.Shorten(), out IEmote emote)) return;
            if (emote.IsLoading) return;

            if (!globalWorld.TryGet(globalPlayerEntity, out AvatarShapeComponent avatarShape)) return;

            BodyShape bodyShape = avatarShape.BodyShape;

            if (emote.AssetResults[bodyShape] == null) return;

            StreamableLoadingResult<AttachmentRegularAsset> streamableAssetValue = emote.AssetResults[bodyShape].Value;

            if (streamableAssetValue is { Succeeded: false } || streamableAssetValue.Asset?.MainAsset == null) return;

            GameObject mainAsset = streamableAssetValue.Asset.MainAsset;
            StreamableLoadingResult<AudioClipData>? audioAssetResult = emote.AudioAssetResults[bodyShape];
            AudioClip? audioClip = audioAssetResult?.Asset;

            if (!cachedEmotePlayer.PlayMasked(mainAsset, audioClip, emote.IsLooping(), true, in cachedAvatarView, ref masked))
                return;

            // Reset stored tag so CancelMaskedEmotes doesn't fire on the next frame
            // before UpdateMaskedEmoteTags has a chance to set the real animator state.
            masked.SetAnimationTag(0);

            cachedMessageBus.Send(masked.EmoteUrn, emote.IsLooping(), masked.Mask);
        }

        [Query]
        private void UpdateMaskedEmoteTags(ref CharacterMaskedEmoteComponent masked) =>
            EmotePlayer.UpdateMaskedEmoteTag(ref masked, cachedAvatarView);

        /// <summary>
        /// Stops the masked emote animation and releases pool references.
        /// When permanent=true, clears EmoteUrn (no replay possible).
        /// When permanent=false, preserves EmoteUrn/Mask for replay on re-entry.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryStopMaskedEmote(ref CharacterMaskedEmoteComponent masked, bool permanent = false)
        {
            if (masked.CurrentEmoteReference == null)
            {
                if (permanent)
                    masked.Reset();
                return;
            }

            cachedEmotePlayer.StopMasked(masked.CurrentEmoteReference, in cachedAvatarView, masked.Mask);

            if (permanent)
                masked.Reset();
            else
            {
                // Keep EmoteUrn and Mask so we can replay when re-entering the scene.
                // Reset the stored animation tag to prevent CancelMaskedEmotes from
                // erroneously firing with a stale IsPlaying=true on the next frame.
                masked.CurrentEmoteReference = null;
                masked.EmoteLoop = false;
                masked.StopEmote = false;
                masked.SetAnimationTag(0);
            }
        }
    }
}
