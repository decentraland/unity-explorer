using Arch.Core;
using ECS.StreamableLoading.Common.Components;
using UnityEngine.Assertions;

namespace ECS.StreamableLoading.Common
{
    /// <summary>
    ///     Enriches Entity Reference to provide additional functionality
    /// </summary>
    /// <typeparam name="TAsset">Asset Type</typeparam>
    /// <typeparam name="TLoadingIntention">Loading Intention Type needed to dereference unused assets</typeparam>
    public struct AssetPromise<TAsset, TLoadingIntention> where TLoadingIntention: ILoadingIntention
    {
        /// <summary>
        ///     Entity Intention will be alive if the loading process is not consumed
        /// </summary>
        public EntityReference LoadingEntity { get; private set; }

        /// <summary>
        ///     Loading intention will persist so it can be used to dereference unused assets
        /// </summary>
        public TLoadingIntention LoadingIntention { get; private set; }

        /// <summary>
        ///     The result if it was loaded
        /// </summary>
        public StreamableLoadingResult<TAsset>? Result { get; private set; }

        public static AssetPromise<TAsset, TLoadingIntention> Create(World world, TLoadingIntention loadingIntention) =>
            new ()
            {
                LoadingIntention = loadingIntention,
                LoadingEntity = world.Reference(world.Create(loadingIntention)),
            };

        /// <summary>
        ///     Returns the asset if the loading is finished
        /// </summary>
        public bool TryGetResult(World world, out StreamableLoadingResult<TAsset> result)
        {
            if (Result.HasValue)
            {
                result = Result.Value;
                return true;
            }

            result = default(StreamableLoadingResult<TAsset>);

            if (world.TryGet(LoadingEntity, out result))
            {
                Result = result;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Returns the result and delete an entity if the loading is finished
        /// </summary>
        public bool TryConsume(World world, out StreamableLoadingResult<TAsset> result)
        {
            Assert.IsFalse(Result.HasValue, $"Promise {LoadingIntention} has been already consumed");

            result = default(StreamableLoadingResult<TAsset>);

            if (world.TryGet(LoadingEntity, out result))
            {
                Result = result;
                world.Destroy(LoadingEntity);
                LoadingEntity = EntityReference.Null;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Destroys the loading entity
        /// </summary>
        public void Consume(World world)
        {
            if (LoadingEntity == EntityReference.Null || !LoadingEntity.IsAlive(world)) return;

            world.Destroy(LoadingEntity);
        }

        /// <summary>
        ///     Places <see cref="ForgetLoadingIntent" /> and nullifies the reference
        /// </summary>
        /// <param name="world"></param>
        public void ForgetLoading(World world)
        {
            if (LoadingEntity == EntityReference.Null || !LoadingEntity.IsAlive(world)) return;

            world.Add(LoadingEntity.Entity, new ForgetLoadingIntent());
            LoadingEntity = EntityReference.Null;
        }
    }
}
