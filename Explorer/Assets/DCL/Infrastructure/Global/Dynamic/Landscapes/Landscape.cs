using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Landscape;
using DCL.RealmNavigation;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace Global.Dynamic.Landscapes
{
    public class Landscape : ILandscape
    {
        private readonly IGlobalRealmController realmController;
        private readonly TerrainGenerator genesisTerrain;
        private readonly WorldTerrainGenerator worldsTerrain;
        private readonly bool landscapeEnabled;
        public readonly Transform Root;
        public Action<ITerrain>? TerrainLoaded;

        public Landscape(IGlobalRealmController realmController, TerrainGenerator genesisTerrain, WorldTerrainGenerator worldsTerrain, bool landscapeEnabled)
        {
            this.realmController = realmController;
            this.genesisTerrain = genesisTerrain;
            this.worldsTerrain = worldsTerrain;
            this.landscapeEnabled = landscapeEnabled;
            Root = new GameObject(nameof(Landscape)).transform;
        }

        public ITerrain CurrentTerrain => realmController.RealmData.IsGenesis() ? genesisTerrain : worldsTerrain;

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
                    await genesisTerrain.GenerateGenesisTerrainAndShowAsync(
                        realmController.RealmData.WorldManifest,
                        processReport: landscapeLoadReport,
                        cancellationToken: ct);
                else
                    await genesisTerrain.ShowAsync(landscapeLoadReport, ct);
            }
            else
            {
                genesisTerrain.Hide();

                WorldsTerrainResult result = realmController.RealmData.IsLocalScene()
                    ? await GenerateStaticScenesTerrainAsync(landscapeLoadReport, ct)
                    : await GenerateFixedScenesTerrainAsync(realmController.RealmData.WorldManifest, landscapeLoadReport, ct);

                if (result != WorldsTerrainResult.GENERATED)
                {
                    worldsTerrain.Hide();
                    landscapeLoadReport.SetProgress(1f);

                    return result == WorldsTerrainResult.DISABLED
                        ? EnumResult<LandscapeError>.SuccessResult()
                        : EnumResult<LandscapeError>.ErrorResult(LandscapeError.TerrainDataUnavailable);
                }
            }

            TerrainLoaded?.Invoke(CurrentTerrain);
            return EnumResult<LandscapeError>.SuccessResult();
        }

        public float GetHeight(float x, float z)
        {
            ITerrain terrain = CurrentTerrain;

            return TerrainGenerator.GetParcelNoiseHeight(x, z, terrain.OccupancyMapData,
                terrain.OccupancyMapSize, terrain.ParcelSize, terrain.OccupancyFloor,
                terrain.MaxHeight);
        }

        public Result IsParcelInsideTerrain(Vector2Int parcel, bool isLocal)
        {
            ITerrain terrain = isLocal && !realmController.RealmData.IsGenesis() ? worldsTerrain : genesisTerrain;

            // If terrain is disabled, allow teleporting anywhere. We're in editor and can assume the
            // user knows what they're doing.
            return terrain.TerrainModel != null && !terrain.TerrainModel.IsInsideBounds(parcel)
                ? Result.ErrorResult($"Parcel {parcel} is outside of the bounds.")
                : Result.SuccessResult();
        }

        private async UniTask<WorldsTerrainResult> GenerateStaticScenesTerrainAsync(AsyncLoadProcessReport landscapeLoadReport, CancellationToken ct)
        {
            if (!worldsTerrain.IsInitialized)
                return WorldsTerrainResult.DISABLED;

            SceneDefinitions? staticScenesEntityDefinitions = await realmController.WaitForStaticScenesEntityDefinitionsAsync(ct);

            if (!staticScenesEntityDefinitions.HasValue)
            {
                ReportHub.LogWarning(ReportCategory.LANDSCAPE, "Static scenes definitions are unavailable, worlds terrain generation skipped");
                return WorldsTerrainResult.UNAVAILABLE;
            }

            List<SceneEntityDefinition> sceneDefinitions = staticScenesEntityDefinitions.Value.Value;

            if (IsLandscapeTerrainDisabledByScene(sceneDefinitions))
                return WorldsTerrainResult.DISABLED;

            int parcelsAmount = sceneDefinitions.Count;

            using (var parcels = new NativeHashSet<int2>(parcelsAmount, AllocatorManager.Persistent))
            {
                foreach (SceneEntityDefinition staticScene in sceneDefinitions)
                {
                    foreach (Vector2Int parcel in staticScene.metadata.scene.DecodedParcels) { parcels.Add(parcel.ToInt2()); }
                }

                worldsTerrain.GenerateTerrain(parcels, landscapeLoadReport);
            }

            return WorldsTerrainResult.GENERATED;
        }

        private async UniTask<WorldsTerrainResult> GenerateFixedScenesTerrainAsync(WorldManifest worldManifest, AsyncLoadProcessReport landscapeLoadReport, CancellationToken ct)
        {
            if (!worldsTerrain.IsInitialized)
                return WorldsTerrainResult.DISABLED;

            List<SceneEntityDefinition> sceneEntityDefinitions = await realmController.WaitForFixedScenePromisesAsync(ct);
            if (IsLandscapeTerrainDisabledByScene(sceneEntityDefinitions))
                return WorldsTerrainResult.DISABLED;

            if (!worldManifest.IsEmpty)
            {
                worldsTerrain.GenerateTerrain(worldManifest.GetOccupiedParcels(), landscapeLoadReport);
                return WorldsTerrainResult.GENERATED;
            }

            var parcelsAmount = 0;

            foreach (SceneEntityDefinition sceneEntity in sceneEntityDefinitions)
                parcelsAmount += sceneEntity.metadata.scene.DecodedParcels.Count;

            using (var parcels = new NativeHashSet<int2>(parcelsAmount, AllocatorManager.Persistent))
            {
                foreach (SceneEntityDefinition sceneEntity in sceneEntityDefinitions)
                {
                    foreach (Vector2Int parcel in sceneEntity.metadata.scene.DecodedParcels)
                        parcels.Add(parcel.ToInt2());
                }

                worldsTerrain.GenerateTerrain(parcels, landscapeLoadReport);
            }

            return WorldsTerrainResult.GENERATED;
        }

        private static bool IsLandscapeTerrainDisabledByScene(IReadOnlyList<SceneEntityDefinition> sceneDefinitions) =>
            sceneDefinitions.Count == 1 && sceneDefinitions[0].metadata.enableTerrain == false;

        private enum WorldsTerrainResult
        {
            /// <summary>Terrain was generated and is shown.</summary>
            GENERATED,

            /// <summary>Terrain is intentionally off: generator not initialized or the scene opted out via scene.json.</summary>
            DISABLED,

            /// <summary>Scene definitions required to build the terrain could not be loaded.</summary>
            UNAVAILABLE,
        }
    }
}
