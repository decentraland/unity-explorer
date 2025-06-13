using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.Landscape;
using DCL.RealmNavigation;
using DCL.Utilities;
using ECS;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;
using Utility.Types;

namespace Global.Dynamic.Landscapes
{
    public class Landscape : ILandscape
    {
        private readonly IGlobalRealmController realmController;
        private readonly TerrainGenerator genesisTerrain;
        private readonly WorldTerrainGenerator worldsTerrain;
        private readonly bool landscapeEnabled;

        public Landscape(IGlobalRealmController realmController, TerrainGenerator genesisTerrain, WorldTerrainGenerator worldsTerrain, bool landscapeEnabled, bool isLocalSceneDevelopment)
        {
            this.realmController = realmController;
            this.genesisTerrain = genesisTerrain;
            this.worldsTerrain = worldsTerrain;
            this.landscapeEnabled = landscapeEnabled;
        }

        public async UniTask<EnumResult<LandscapeError>> LoadTerrainAsync(AsyncLoadProcessReport landscapeLoadReport, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return EnumResult<LandscapeError>.CancelledResult(LandscapeError.MessageError);

            if (landscapeEnabled == false)
                return EnumResult<LandscapeError>.ErrorResult(LandscapeError.LandscapeDisabled);

            if (realmController.RealmData.IsGenesis())
            {
                //TODO (Juani): The globalWorld terrain would be hidden. We need to implement the re-usage when going back
                worldsTerrain.SwitchVisibility(false);

                if (!genesisTerrain.IsTerrainGenerated)
                    await genesisTerrain.GenerateGenesisTerrainAndShowAsync(processReport: landscapeLoadReport,
                        cancellationToken: ct);
                else
                    await genesisTerrain.ShowAsync(landscapeLoadReport);
            }
            else
            {
                genesisTerrain.Hide();

                if (realmController.RealmData.IsLocalScene())
                    await GenerateStaticScenesTerrainAsync(landscapeLoadReport, ct);
                else
                    await GenerateFixedScenesTerrainAsync(landscapeLoadReport, ct);
            }

            return EnumResult<LandscapeError>.SuccessResult();
        }

        //TODO should it accept isLocal instead of encapsulating it?
        public Result IsParcelInsideTerrain(Vector2Int parcel, bool isLocal)
        {
            IContainParcel terrain = isLocal && !realmController.RealmData.IsGenesis() ? worldsTerrain : genesisTerrain;

            return !terrain.Contains(parcel)
                ? Result.ErrorResult($"Parcel {parcel} is outside of the bounds.")
                : Result.SuccessResult();
        }

        private async UniTask GenerateStaticScenesTerrainAsync(AsyncLoadProcessReport landscapeLoadReport, CancellationToken ct)
        {
            if (!worldsTerrain.IsInitialized)
                return;

            var staticScenesEntityDefinitions = await realmController.WaitForStaticScenesEntityDefinitionsAsync(ct);
            if (!staticScenesEntityDefinitions.HasValue) return;

            int parcelsAmount = staticScenesEntityDefinitions.Value.Value.Count;

            using (var parcels = new NativeParallelHashSet<int2>(parcelsAmount, AllocatorManager.Persistent))
            {
                foreach (var staticScene in staticScenesEntityDefinitions.Value.Value)
                {
                    foreach (Vector2Int parcel in staticScene.metadata.scene.DecodedParcels) { parcels.Add(parcel.ToInt2()); }
                }

                await worldsTerrain.GenerateTerrainAsync(parcels, (uint)realmController.RealmData.GetHashCode(), landscapeLoadReport, cancellationToken: ct);
            }
        }

        private async UniTask GenerateFixedScenesTerrainAsync(AsyncLoadProcessReport landscapeLoadReport, CancellationToken ct)
        {
            if (!worldsTerrain.IsInitialized)
                return;

            AssetPromise<SceneEntityDefinition, GetSceneDefinition>[]? promises = await realmController.WaitForFixedScenePromisesAsync(ct);

            var parcelsAmount = 0;

            foreach (AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise in promises)
                parcelsAmount += promise.Result!.Value.Asset!.metadata.scene.DecodedParcels.Count;

            using (var parcels = new NativeParallelHashSet<int2>(parcelsAmount, AllocatorManager.Persistent))
            {
                foreach (AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise in promises)
                {
                    foreach (Vector2Int parcel in promise.Result!.Value.Asset!.metadata.scene.DecodedParcels)
                        parcels.Add(parcel.ToInt2());
                }

                await worldsTerrain.GenerateTerrainAsync(parcels, (uint)realmController.RealmData.GetHashCode(), landscapeLoadReport, cancellationToken: ct);
            }
        }

    }
}
