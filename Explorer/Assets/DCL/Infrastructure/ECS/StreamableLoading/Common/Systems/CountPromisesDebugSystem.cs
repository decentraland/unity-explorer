using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using ECS.Groups;
using ECS.StreamableLoading.Common.Components;
using Unity.Profiling;
using UnityEngine;
using Utility;

namespace ECS.StreamableLoading.Common.Systems
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    public partial class CountPromisesDebugSystem : BaseUnityLoopSystem
    {
        private static readonly StreamableLoadingState.Status[] POSSIBLE_STATUSES = EnumUtils.Values<StreamableLoadingState.Status>();

        private readonly ISubQuery[] subQueries;

        internal CountPromisesDebugSystem(World world, ISubQuery[] subQueries) : base(world)
        {
            this.subQueries = subQueries;
        }

        protected override void Update(float t)
        {
            foreach (ISubQuery subQuery in subQueries)
                subQuery.Update(World);
        }

        private readonly struct WasAllowed { }

        public class TimeToAllowAverage
        {
            private double sumMs;
            private ulong count;

            public TimeToAllowAverage(ProfilerCounterValue<float> profilerCounterValue)
            {
                this.profilerCounterValue = profilerCounterValue;
            }

            internal readonly ProfilerCounterValue<float> profilerCounterValue;
            internal ElementBinding<ulong> averageNs { get; } = new (0);

            internal void Add(float seconds)
            {
                sumMs += seconds * 1_000;
                count++;
                double avg = sumMs / count;
                averageNs.Value = (ulong)(avg * 1_000_000);
                profilerCounterValue.Value = (float)avg;
            }
        }

        public interface ISubQuery
        {
            TimeToAllowAverage TimeToAllowAverage { get; }

            void Update(World world);
        }

        /// <summary>
        ///     Exists in a single copy and shared between all instance of this system
        /// </summary>
        /// <typeparam name="TIntent"></typeparam>
        public class SubQuery<TIntent> : ISubQuery where TIntent: ILoadingIntention
        {
            private readonly QueryDescription updateStatusQuery;
            private readonly QueryDescription timeToAllowAverageQuery;

            private readonly ProfilerCounterValue<uint>[] statusCounters;

            private UpdateStatusCounters updateStatusCounters;
            private UpdateTimeToAllowAverage timeToAllowAverage;

            public TimeToAllowAverage TimeToAllowAverage { get; }

            public SubQuery()
            {
                updateStatusQuery = new QueryDescription().WithAll<TIntent, StreamableLoadingState>();
                timeToAllowAverageQuery = new QueryDescription().WithAll<TIntent, StreamableLoadingState, IntentionCreationTime>().WithNone<WasAllowed>();

                statusCounters = new ProfilerCounterValue<uint>[POSSIBLE_STATUSES.Length];

                for (var i = 0; i < POSSIBLE_STATUSES.Length; i++)
                    statusCounters[i] = new ProfilerCounterValue<uint>($"{typeof(TIntent).Name}_{POSSIBLE_STATUSES[i]}");

                updateStatusCounters = new UpdateStatusCounters(statusCounters);

                TimeToAllowAverage = new TimeToAllowAverage(new ProfilerCounterValue<float>($"{typeof(TIntent).Name}_TTA", ProfilerMarkerDataUnit.TimeNanoseconds));
                timeToAllowAverage = new UpdateTimeToAllowAverage(TimeToAllowAverage);
            }

            void ISubQuery.Update(World world)
            {
                for (var i = 0; i < POSSIBLE_STATUSES.Length; i++)
                    statusCounters[i].Value = 0U;

                timeToAllowAverage.world = world;

                if (StreamableLoadingDebug.ENABLED)
                    world.InlineQuery<UpdateStatusCounters, StreamableLoadingState>(updateStatusQuery, ref updateStatusCounters);

                world.InlineEntityQuery<UpdateTimeToAllowAverage, StreamableLoadingState, IntentionCreationTime>(timeToAllowAverageQuery, ref timeToAllowAverage);
            }

            private readonly struct UpdateStatusCounters : IForEach<StreamableLoadingState>
            {
                private readonly ProfilerCounterValue<uint>[] statusCounters;

                public UpdateStatusCounters(ProfilerCounterValue<uint>[] statusCounters)
                {
                    this.statusCounters = statusCounters;
                }

                public void Update(ref StreamableLoadingState t0)
                {
                    statusCounters[(int)t0.Value].Value++;
                }
            }

            private struct UpdateTimeToAllowAverage : IForEachWithEntity<StreamableLoadingState, IntentionCreationTime>
            {
                private readonly TimeToAllowAverage timeToAllowAverage;

                internal World? world;

                public UpdateTimeToAllowAverage(TimeToAllowAverage timeToAllowAverage)
                {
                    this.timeToAllowAverage = timeToAllowAverage;
                    world = null;
                }

                public void Update(Entity entity, ref StreamableLoadingState state, ref IntentionCreationTime time)
                {
                    if (state.Value != StreamableLoadingState.Status.Allowed) return;

                    timeToAllowAverage.Add(Time.realtimeSinceStartup - time.Value);
                    world!.Add<WasAllowed>(entity);
                }
            }
        }
    }
}
