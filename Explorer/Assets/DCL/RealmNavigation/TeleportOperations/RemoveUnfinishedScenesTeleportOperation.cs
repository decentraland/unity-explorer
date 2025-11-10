using Arch.Core;
using Cysharp.Threading.Tasks;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    /// <summary>
    /// Remove those scenes which the promise failed and its missing the scene facade
    /// </summary>
    public class RemoveUnfinishedScenesTeleportOperation : TeleportOperationBase
    {
        private readonly World world;

        public RemoveUnfinishedScenesTeleportOperation(World world)
        {
            this.world = world;
        }

        protected override UniTask InternalExecuteAsync(TeleportParams args, CancellationToken ct)
        {
            // See https://github.com/decentraland/unity-explorer/issues/4935
            // The scene load process it is disrupted due to internet issues remaining in an invalid state
            // We need to remove them and reload them, otherwise they will keep in an inconsistent state forever
            world.Query(in new QueryDescription()
                          .WithAll<AssetPromise<ISceneFacade, GetSceneFacadeIntention>, SceneLoadingState>()
                          .WithNone<DeleteEntityIntention, ISceneFacade>(),
                (Entity entity, ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise, ref SceneLoadingState sceneLoadingState) =>
                {
                    if (promise is { IsConsumed: true, Result: { Succeeded: false } })
                    {
                        world.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
                        world.Add<DeleteEntityIntention>(entity);
                        sceneLoadingState.VisualSceneState = VisualSceneState.UNINITIALIZED;
                        sceneLoadingState.PromiseCreated = false;
                    }
                });

            return UniTask.CompletedTask;
        }
    }
}
