using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using ECS.Abstract;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Systems
{
    public static class DefaultWearablesWorldExtension
    {
        private static readonly QueryDescription QUERY = new QueryDescription().WithAll<DefaultWearablesComponent>();

        public static SingleInstanceEntity CacheDefaultWearablesState(this World world) =>
            new (in QUERY, world);

        public static ref readonly DefaultWearablesComponent GetDefaultWearablesState(this in SingleInstanceEntity instance, World world) =>
            ref world.Get<DefaultWearablesComponent>(instance);

        /// <summary>
        ///     Can be used at loading screen to wait for default wearable resolution
        /// </summary>
        /// <returns></returns>
        /// <exception cref="OperationCancelledException"></exception>
        public static async UniTask<DefaultWearablesComponent.State> WaitForDefaultWearablesLoadingAsync(this World world,
            PlayerLoopTiming playerLoopTiming = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default)
        {
            SingleInstanceEntity entity = world.CacheDefaultWearablesState();

            // Poll the entity until its result is consumed
            DefaultWearablesComponent.State state;

            while ((state = entity.GetDefaultWearablesState(world).ResolvedState) == DefaultWearablesComponent.State.InProgress)
                await UniTask.Yield(playerLoopTiming, cancellationToken: cancellationToken);

            return state;
        }
    }
}
