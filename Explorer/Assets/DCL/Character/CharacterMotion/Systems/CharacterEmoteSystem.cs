using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.CharacterMotion.Emotes;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateInGroup(typeof(PostPhysicsSystemGroup))]
    [UpdateBefore(typeof(CharacterAnimationSystem))]
    public partial class CharacterEmoteSystem : BaseUnityLoopSystem
    {
        private readonly IEmoteCache emoteCache;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly string reportCategory;
        private readonly EmotePlayer emotePlayer;

        public CharacterEmoteSystem(World world, IEmoteCache emoteCache, IDebugContainerBuilder debugContainerBuilder) : base(world)
        {
            this.emoteCache = emoteCache;
            this.debugContainerBuilder = debugContainerBuilder;
            reportCategory = GetReportCategory();
            emotePlayer = new EmotePlayer(reportCategory);
        }

        protected override void Update(float t)
        {
            CancelEmotesQuery(World);
            ConsumeEmoteIntentQuery(World);
            ReplicateLoopingEmotesQuery(World);
        }

        [Query]
        private void CancelEmotes(ref CharacterEmoteComponent emoteComponent, in CharacterRigidTransform rigidTransform)
        {
            float velocity = rigidTransform.MoveVelocity.Velocity.sqrMagnitude;
            float verticalVelocity = Mathf.Abs(rigidTransform.GravityVelocity.sqrMagnitude);

            bool canEmoteBeCancelled = velocity > 0.2f || verticalVelocity > 0.2f;
            if (!canEmoteBeCancelled) return;

            EmoteReferences? emoteReference = emoteComponent.CurrentEmoteReference;
            if (emoteReference == null) return;

            emoteComponent.EmoteClip = null;
            emoteComponent.EmoteLoop = false;
            emotePlayer.Stop(emoteReference);
        }

        [Query]
        private void ConsumeEmoteIntent(in Entity entity, ref CharacterEmoteComponent emoteComponent, in CharacterEmoteIntent emoteIntent, in IAvatarView avatarView)
        {
            string emoteId = emoteIntent.EmoteId;

            if (emoteCache.TryGetEmote(emoteId, out IEmote emote))
            {
                // emote failed to load? remove intent
                if (emote.ManifestResult is { IsInitialized: true, Exception: not null })
                {
                    ReportHub.LogError(reportCategory, $"Cant play emote {emoteId} since it failed loading \n {emote.ManifestResult}");
                    World.Remove<CharacterEmoteIntent>(entity);
                    return;
                }

                StreamableLoadingResult<WearableAsset>? streamableAsset = emote.WearableAssetResults[0];

                // the emote is still loading? dont remove the intent yet, wait for it
                if (streamableAsset == null) return;
                if (!streamableAsset.Value.Succeeded) return;
                if (streamableAsset.Value.Exception != null) return;

                GameObject? mainAsset = streamableAsset.Value.Asset?.GetMainAsset<GameObject>();

                if (mainAsset == null) return;

                if (!emotePlayer.Play(mainAsset, emote.IsLooping(), in avatarView, ref emoteComponent))
                    ReportHub.LogWarning(reportCategory, $"Emote {emote.Model.Asset.metadata.name} cant be played, AB version: {emote.ManifestResult?.Asset?.dto.version} should be >= 16");

                // If the avatar is already doing an emote and we re-trigger it, we want to restart the animation to enable emote spamming
                if (emoteComponent.WasEmoteJustTriggered && emoteComponent.EmoteClip != null)
                {
                    avatarView.SetAnimatorTrigger(avatarView.IsAnimatorInTag(AnimationHashes.EMOTE) || avatarView.IsAnimatorInTag(AnimationHashes.EMOTE_LOOP) ? AnimationHashes.EMOTE_RESET : AnimationHashes.EMOTE);
                    avatarView.SetAnimatorBool(AnimationHashes.EMOTE_LOOP, emoteComponent.EmoteLoop);
                    avatarView.ReplaceEmoteAnimation(emoteComponent.EmoteClip);
                }

                emoteComponent.WasEmoteJustTriggered = false;
            }

            World.Remove<CharacterEmoteIntent>(entity);
        }

        [Query]
        private void ReplicateLoopingEmotes(ref CharacterEmoteComponent animationComponent, in IAvatarView avatarView)
        {
            int prevTag = animationComponent.CurrentAnimationTag;
            int currentTag = avatarView.GetAnimatorCurrentStateTag();

            if ((prevTag == AnimationHashes.EMOTE && currentTag == AnimationHashes.EMOTE_LOOP)
                || (prevTag == AnimationHashes.EMOTE_LOOP && currentTag == AnimationHashes.EMOTE))
            {
                // this means that the emote just looped
            }

            animationComponent.CurrentAnimationTag = currentTag;
        }
    }
}
