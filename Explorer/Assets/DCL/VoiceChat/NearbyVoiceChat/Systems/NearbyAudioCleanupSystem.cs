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
    ///             <item><description><b>Trigger #1 (avatar gone)</b> — linked avatar entity is dead or flagged with
    ///                 <see cref="DeleteEntityIntention"/>.</description></item>
    ///             <item><description><b>Trigger #2 (stream gone)</b> — registry no longer reports the bound <c>(walletId, sid)</c>.</description></item>
    ///             <item><description><b>Trigger #3 (blocked)</b> — <see cref="IUserBlockingCache.UserIsBlocked"/> returns
    ///                 <c>true</c> for the bound <c>walletId</c>.</description></item>
    ///             <item><description><b>Trigger #4 (listening gate)</b> — <see cref="NearbyVoiceChatStateModel"/> is in
    ///                 <see cref="NearbyVoiceChatState.SUPPRESSED"/> or <see cref="NearbyVoiceChatState.DISABLED"/>;
    ///                 takes a single bulk archetype-move path that marks every live audio entity at once,
    ///                 bypassing per-entity detection entirely.</description></item>
    ///         </list>
    ///         Triggers #1–#3 run via a per-entity source-generated query; Trigger #4 short-circuits to a single
    ///         <c>World.Add&lt;DeleteEntityIntention&gt;(QueryDescription, default)</c> call.
    ///     </para>
    ///     <para>
    ///         <b>Teardown</b> — reacts to entities now carrying <see cref="DeleteEntityIntention"/>:
    ///         disposes the <see cref="LivekitAudioSource"/> via <see cref="NearbyAudioSourceFactory"/>
    ///         (Stop → Free → SafeDestroyGameObject) and removes the <c>(walletId, sid) → entity</c>
    ///         binding. Physical entity destruction is delegated to <see cref="DestroyEntitiesSystem"/>.
    ///     </para>
    ///     <para>
    ///         <see cref="OnDispose"/> disposes every remaining <see cref="LivekitAudioSource"/> and clears bindings
    ///         when the world is torn down.
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
            TearDownAllAudioSourcesQuery(World);
            bindings.Clear();
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void FlagDoomedAudioEntities(Entity audioEntity, ref NearbyAudioSourceComponent comp)
        {
            Entity avatar = comp.AvatarEntity;

            // Cheap-first short-circuit. Order matters — IsAlive is a generation-counter compare,
            // Has<T> is an archetype-bitmap test (both sub-10 ns), IsStreamGone is a ConcurrentDictionary
            // lookup + Array.IndexOf (N≈1), UserIsBlocked is heavier still. The dominant doom signal
            // at scale is "avatar's last sid disappeared" — covered by the component-absence clause
            // without touching the registry.
            //
            // Component absence ≠ avatar gone: NearbyLivekitBridgeSystem filters its UpdateStreaming
            // query with [None<DeleteEntityIntention>], so a doomed avatar keeps StreamingAudioComponent
            // until physical destruction. Component absence ≠ specific sid gone either: the component
            // is per-walletId (one entry per avatar carries its full sid set), so for multi-sid
            // participants the per-sid IsStreamGone fallback is the only granular signal. Both
            // fallbacks must remain.
            bool avatarGoneOrOutOfRange =
                !World.IsAlive(avatar)
                || World.Has<DeleteEntityIntention>(avatar)
                || !World.Has<StreamingAudioComponent>(avatar)
                || !World.Has<InAudibleRangeTag>(avatar);

            if (avatarGoneOrOutOfRange
                || registry.IsStreamGone(comp.Key)
                || userBlockingCache.UserIsBlocked(comp.Key.identity))
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
        private void TearDownAllAudioSources(ref NearbyAudioSourceComponent comp)
        {
            sourceFactory.Dispose(comp.LivekitAudioSource);
        }
    }
}
