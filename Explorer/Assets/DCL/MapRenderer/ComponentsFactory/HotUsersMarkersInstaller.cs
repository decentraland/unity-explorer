﻿using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Users;
using DCL.MapRenderer.MapLayers.UsersMarker;
using ECS.SceneLifeCycle.Realm;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal struct HotUsersMarkersInstaller
    {
        private IAssetsProvisioner assetsProvisioner;
        private IMapRendererSettings mapSettings;

        public async UniTask InstallAsync(
            Dictionary<MapLayer, IMapLayerController> writer,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IAssetsProvisioner assetsProv,
            IMapRendererSettings settings,
            IRealmNavigator realmNavigator,
            RemoteUsersRequestController remoteUsersRequestController,
            CancellationToken cancellationToken)
        {
            assetsProvisioner = assetsProv;
            mapSettings = settings;
            HotUserMarkerObject? prefab = await GetPrefabAsync(cancellationToken);

            var objectsPool = new ObjectPool<HotUserMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            IHotUserMarker CreateWrap() =>
                new HotUserMarker(objectsPool, coordsUtils);

            var wrapsPool = new ObjectPool<IHotUserMarker>(CreateWrap, actionOnRelease: m => m.Dispose());

            var controller = new UsersMarkersHotAreaController(objectsPool, wrapsPool, configuration.HotUserMarkersRoot, coordsUtils, cullingController, realmNavigator, remoteUsersRequestController);
            await controller.InitializeAsync(cancellationToken);
            writer.Add(MapLayer.HotUsersMarkers, controller);
        }

        private static HotUserMarkerObject CreatePoolMethod(MapRendererConfiguration configuration, HotUserMarkerObject prefab, ICoordsUtils coordsUtils)
        {
            HotUserMarkerObject markerObject = Object.Instantiate(prefab, configuration.HotUserMarkersRoot);
            markerObject.UpdateSortOrder(MapRendererDrawOrder.HOT_USER_MARKERS);
            coordsUtils.SetObjectScale(markerObject);
            return markerObject;
        }

        private async UniTask<HotUserMarkerObject> GetPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.UserMarker, cancellationToken)).Value;
    }
}
