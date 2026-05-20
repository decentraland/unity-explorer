using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.SceneBannedUsers;
using DCL.VoiceChat.Nearby.Audio;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Detection + teardown for Nearby audio-source components (co-located on the avatar entity).
    ///     <para>
    ///         <b>Triggers:</b>
    ///         <list type="bullet">
    ///             <item><description><b>#1 streamer marker gone</b> — avatar lost <see cref="NearbyAudioStreamerComponent"/>.</description></item>
    ///             <item><description><b>#2 out of range</b> — avatar lost <see cref="InAudibleRangeTag"/>.</description></item>
    ///             <item><description><b>#3 sid not active</b> — registry's resolver no longer picks the bound <c>(walletId, sid)</c>.</description></item>
    ///             <item><description><b>#4 blocked</b> — <see cref="IUserBlockingCache.UserIsBlocked"/> returns <c>true</c>.</description></item>
    ///             <item><description><b>#5 scene-banned</b> — <see cref="RoomMetadataCurrentScene.IsUserBanned"/> returns <c>true</c>.</description></item>
    ///             <item><description><b>#6 listening gate</b> — bulk removal when state is <see cref="NearbyVoiceChatState.SUPPRESSED"/> / <see cref="NearbyVoiceChatState.DISABLED"/>.</description></item>
    ///             <item><description><b>#7 avatar dying</b> — avatar carries <see cref="DeleteEntityIntention"/>; dispose source, component goes away with the entity.</description></item>
    ///         </list>
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.NEARBY_VOICE_CHAT)]
    public partial class NearbyAudioCleanupSystem : BaseUnityLoopSystem
    {
        private readonly INearbyAudioStreamRegistry registry;
        private readonly IUserBlockingCache userBlockingCache;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly INearbyAudioSourceFactory sourceFactory;
        private readonly RoomMetadataCurrentScene roomMetadataCurrentScene;

        // Mismatch with registry.RebuildEpoch ⇒ output-device changed since last tick.
        private int lastSeenRebuildEpoch;

        internal NearbyAudioCleanupSystem(World world, INearbyAudioStreamRegistry registry, IUserBlockingCache userBlockingCache, NearbyVoiceChatStateModel stateModel, INearbyAudioSourceFactory sourceFactory, RoomMetadataCurrentScene roomMetadataCurrentScene) : base(world)
        {
            this.registry = registry;
            this.userBlockingCache = userBlockingCache;
            this.stateModel = stateModel;
            this.sourceFactory = sourceFactory;
            this.roomMetadataCurrentScene = roomMetadataCurrentScene;

            lastSeenRebuildEpoch = registry.RebuildEpoch;
        }

        protected override void Update(float t)
        {
            int currentEpoch = registry.RebuildEpoch;

            bool deviceChanged = currentEpoch != lastSeenRebuildEpoch;
            if (deviceChanged)
            {
                lastSeenRebuildEpoch = currentEpoch;
                sourceFactory.InvalidateForDeviceChange();
            }

            // Listening-gate AND device-change wipe every live source; per-entity detection is skipped in either case.
            if (stateModel.IsListeningDisabled || deviceChanged)
                DisposeAllLiveSourcesQuery(World);
            else
            {
                ReapOrphanedSourceQuery(World);     // #1: streamer marker gone
                ReapOutOfRangeSourceQuery(World);   // #2: audible-range tag gone
                ReapFilteredSourceQuery(World);     // #3-4-5 sid demoted / user blocked or banned by the scene
            }

            DisposeDyingAvatarSourcesQuery(World); // #7: avatars marked for deletion
        }

        protected override void OnDispose()
        {
            DisposeAllLiveSourcesQuery(World);
            DisposeDyingAvatarSourcesQuery(World);
        }

        // #1: avatar is not a streamer anymore (lost NearbyAudioStreamerComponent).
        [Query]
        [None(typeof(DeleteEntityIntention), typeof(NearbyAudioStreamerComponent))]
        private void ReapOrphanedSource(Entity entity, ref NearbyAudioSourceComponent comp) =>
            DisposeAndRemove(entity, ref comp);

        // #2: avatar left audible range.
        [Query]
        [All(typeof(NearbyAudioStreamerComponent))]
        [None(typeof(DeleteEntityIntention), typeof(InAudibleRangeTag))]
        private void ReapOutOfRangeSource(Entity entity, ref NearbyAudioSourceComponent comp) =>
            DisposeAndRemove(entity, ref comp);

        // #3/#4/#5:
        // !IsActiveSid covers sid evicted entirely (resolver picks different/null sid)
        // AND sid demoted (resolver picked a fresher candidate — ghost loser reaped here so binding spawns the winner).
        [Query]
        [All(typeof(NearbyAudioStreamerComponent), typeof(InAudibleRangeTag))]
        [None(typeof(DeleteEntityIntention))]
        private void ReapFilteredSource(Entity entity, ref NearbyAudioSourceComponent comp)
        {
            bool remove = userBlockingCache.UserIsBlocked(comp.Key.identity)
                          || roomMetadataCurrentScene.IsUserBanned(comp.Key.identity)
                          || !registry.IsActiveSid(comp.Key);

            if (remove)
                DisposeAndRemove(entity, ref comp);
        }

        private void DisposeAndRemove(Entity entity, ref NearbyAudioSourceComponent comp)
        {
            sourceFactory.Dispose(comp.LivekitAudioSource);
            World.Remove<NearbyAudioSourceComponent>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void DisposeAllLiveSources(Entity entity, ref NearbyAudioSourceComponent comp) =>
            DisposeAndRemove(entity, ref comp);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void DisposeDyingAvatarSources(ref NearbyAudioSourceComponent comp)
        {
            sourceFactory.Dispose(comp.LivekitAudioSource);
        }
    }
}
