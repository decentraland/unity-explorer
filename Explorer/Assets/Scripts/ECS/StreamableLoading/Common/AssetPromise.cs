using Arch.Core;
using DCL.Diagnostics;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using System;

namespace ECS.StreamableLoading.Common
{
    /// <summary>
    ///     Enriches Entity Reference to provide additional functionality
    /// </summary>
    /// <typeparam name="TAsset">Asset Type</typeparam>
    /// <typeparam name="TLoadingIntention">Loading Intention Type needed to dereference unused assets</typeparam>
    public struct AssetPromise<TAsset, TLoadingIntention> : IEquatable<AssetPromise<TAsset, TLoadingIntention>>
        where TLoadingIntention: IAssetIntention, IEquatable<TLoadingIntention>
    {
        private static readonly string ASSERTION_MESSAGE = $"{nameof(AssetPromise<TAsset, TLoadingIntention>)} <{typeof(TAsset)}, {typeof(TLoadingIntention)}> is already consumed";

        public static readonly AssetPromise<TAsset, TLoadingIntention> NULL = new () { Entity = EntityReference.Null };

        /// <summary>
        ///     Entity Intention will be alive if the loading process is not consumed
        /// </summary>
        public EntityReference Entity { get; private set; }

        /// <summary>
        ///     Loading intention will persist so it can be used to dereference unused assets
        /// </summary>
        public TLoadingIntention LoadingIntention { get; private set; }

        /// <summary>
        ///     The result if it was loaded
        /// </summary>
        public StreamableLoadingResult<TAsset>? Result { get; private set; }

        public bool IsConsumed => Entity == EntityReference.Null;

        public static AssetPromise<TAsset, TLoadingIntention> Create(World world, TLoadingIntention loadingIntention, IPartitionComponent partition) =>
            new ()
            {
                LoadingIntention = loadingIntention,
                Entity = world.Reference(world.Create(loadingIntention, partition, new StreamableLoadingState())),
            };

        public static AssetPromise<TAsset, TLoadingIntention> CreateFinalized(TLoadingIntention loadingIntention, StreamableLoadingResult<TAsset>? result) =>
            new ()
            {
                LoadingIntention = loadingIntention,
                Entity = EntityReference.Null,
                Result = result,
            };

        /// <summary>
        ///     Returns the asset if the loading is finished
        /// </summary>
        public  bool TryGetResult(World world, out StreamableLoadingResult<TAsset> result)
        {
            if (Result.HasValue)
            {
                result = Result.Value;
                return true;
            }

            result = default(StreamableLoadingResult<TAsset>);

            if (Entity == EntityReference.Null || !Entity.IsAlive(world)) return false;

            if (world.TryGet(Entity, out result))
            {
                Result = result;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Returns the result and deletes an entity if the loading is finished,
        ///     can't be consumed several times
        /// </summary>
        public bool TryConsume(World world, out StreamableLoadingResult<TAsset> result)
        {
            if (Result.HasValue)
            {
                // It means `TryGet` was called so now remove the entity
                if (Entity != EntityReference.Null)
                {
                    ReportHub.LogError(ReportCategory.STREAMABLE_LOADING,
                        $"{nameof(TryGetResult)} was called before {nameof(TryConsume)} for {LoadingIntention.ToString()}, the flow is inconclusive and should be fixed!");

                    world.Destroy(Entity);
                    Entity = EntityReference.Null;
                    result = Result.Value;
                    return true;
                }

                throw new Exception($"{ASSERTION_MESSAGE} {LoadingIntention.ToString()}");
            }

            result = default(StreamableLoadingResult<TAsset>);

            if (world.TryGet(Entity, out result))
            {
                Result = result;
                world.Destroy(Entity);
                Entity = EntityReference.Null;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Destroys the loading entity
        /// </summary>
        public void Consume(World world)
        {
            if (Entity == EntityReference.Null || !Entity.IsAlive(world)) return;

            world.Destroy(Entity);
            Entity = EntityReference.Null;
        }

        /// <summary>
        ///     Places <see cref="ForgetLoadingIntent" /> and nullifies the reference
        /// </summary>
        /// <param name="world"></param>
        public void ForgetLoading(World world)
        {
            if (Entity == EntityReference.Null || !Entity.IsAlive(world)) return;

            LoadingIntention.CancellationTokenSource.Cancel();
            Entity = EntityReference.Null;
        }

        public bool Equals(AssetPromise<TAsset, TLoadingIntention> other) =>
            Entity.Equals(other.Entity) && LoadingIntention.Equals(other.LoadingIntention) && Nullable.Equals(Result, other.Result);

        public override bool Equals(object obj) =>
            obj is AssetPromise<TAsset, TLoadingIntention> other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Entity, LoadingIntention, Result);

        public static bool operator ==(AssetPromise<TAsset, TLoadingIntention> left, AssetPromise<TAsset, TLoadingIntention> right) =>
            left.Equals(right);

        public static bool operator !=(AssetPromise<TAsset, TLoadingIntention> left, AssetPromise<TAsset, TLoadingIntention> right) =>
            !left.Equals(right);
    }
}
