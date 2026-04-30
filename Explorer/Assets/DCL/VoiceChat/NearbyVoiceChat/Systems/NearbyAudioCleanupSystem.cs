using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.VoiceChat.Nearby.Audio;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using LiveKit.Rooms.Streaming;
using System.Collections.Generic;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Detection + teardown for Nearby audio-source entities.
    ///     <para>
    ///         <b>Detection</b> — per tick tags doomed audio entities with <see cref="DeleteEntityIntention"/> on any of:
    ///         <list type="bullet">
    ///             <item><description><b>Trigger #1 (avatar gone)</b> — linked avatar entity is dead or flagged with  <see cref="DeleteEntityIntention"/>.</description></item>
    ///             <item><description><b>Trigger #2 (stream gone)</b> — registry no longer reports the bound <c>(walletId, sid)</c>.</description></item>
    ///             <item><description><b>Trigger #3 (blocked)</b> — <see cref="IUserBlockingCache.UserIsBlocked"/> returns  <c>true</c> for the bound <c>walletId</c>.</description></item>
    ///             <item><description><b>Trigger #4 (listening gate)</b> — bulk removal when <see cref="NearbyVoiceChatStateModel"/> is in   <see cref="NearbyVoiceChatState.SUPPRESSED"/> or <see cref="NearbyVoiceChatState.DISABLED"/>;</description></item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         <b>Teardown</b> — reacts to entities now carrying <see cref="DeleteEntityIntention"/>:
    ///          - disposes the <see cref="LivekitAudioSource"/> to the pool via <see cref="NearbyAudioSourceFactory"/> (Stop → Free → SafeDestroyGameObject)
    ///          - removes the <c>(walletId, sid) → entity</c> binding. Physical entity destruction is delegated to <see cref="DestroyEntitiesSystem"/>.
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.NEARBY_VOICE_CHAT)]
    public partial class NearbyAudioCleanupSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription LIVE_AUDIO_QUERY =
            new QueryDescription().WithAll<NearbyAudioSourceComponent>().WithNone<DeleteEntityIntention>();

        private readonly INearbyAudioStreamRegistry registry;
        private readonly Dictionary<StreamKey, Entity> bindings;
        private readonly IUserBlockingCache userBlockingCache;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly NearbyAudioSourceFactory sourceFactory;

        internal NearbyAudioCleanupSystem(World world, INearbyAudioStreamRegistry registry, Dictionary<StreamKey, Entity> bindings, IUserBlockingCache userBlockingCache, NearbyVoiceChatStateModel stateModel, NearbyAudioSourceFactory sourceFactory) : base(world)
        {
            this.registry = registry;
            this.bindings = bindings;
            this.userBlockingCache = userBlockingCache;
            this.stateModel = stateModel;
            this.sourceFactory = sourceFactory;
        }

        protected override void Update(float t)
        {
            // Listening-gate is one bulk archetype-move; per-entity detection is skipped entirely when closed.
            if (stateModel.IsListeningDisabled)
                World.Add<DeleteEntityIntention>(in LIVE_AUDIO_QUERY);
            else
                FlagDoomedAudioEntitiesQuery(World);

            TearDownMarkedAudioEntitiesQuery(World);
        }

        protected override void OnDispose()
        {
            DisposeAllAudioSourcesQuery(World);
            bindings.Clear();
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void FlagDoomedAudioEntities(Entity audioEntity, ref NearbyAudioSourceComponent comp)
        {
            Entity avatar = comp.AvatarEntity;

            // Component absence ≠ avatar gone. Component absence ≠ specific sid gone either. Both fallbacks must remain.
            bool avatarGoneOrOutOfRange = !World.IsAlive(avatar) || World.Has<DeleteEntityIntention>(avatar) || !World.Has<NearbyAudioStreamerComponent>(avatar) || !World.Has<InAudibleRangeTag>(avatar);
            if (avatarGoneOrOutOfRange || registry.IsStreamGone(comp.Key) || userBlockingCache.UserIsBlocked(comp.Key.identity))
                World.Add<DeleteEntityIntention>(audioEntity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void TearDownMarkedAudioEntities(ref NearbyAudioSourceComponent comp)
        {
            sourceFactory.Dispose(comp.LivekitAudioSource);
            bindings.Remove(comp.Key);
        }

        [Query]
        private void DisposeAllAudioSources(ref NearbyAudioSourceComponent comp)
        {
            sourceFactory.Dispose(comp.LivekitAudioSource);
        }
    }
}
