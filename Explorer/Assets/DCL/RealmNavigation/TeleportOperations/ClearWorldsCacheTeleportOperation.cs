using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class ClearWorldsCacheTeleportOperation : TeleportOperationBase
    {
        private readonly IPlacesAPIService placesAPIService;

        internal ClearWorldsCacheTeleportOperation(IPlacesAPIService placesAPIService)
        {
            this.placesAPIService = placesAPIService;
        }

        protected override UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            placesAPIService.ClearWorldsCache();
            return UniTask.CompletedTask;
        }
    }
}
