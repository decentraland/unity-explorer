using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Favorites;
using DCL.PlacesAPIService;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal struct FavoritesMarkersInstaller
    {
        private const int PREWARM_COUNT = 60;

        private IAssetsProvisioner assetsProvisioner;
        private MapRendererSettings mapSettings;
        private IPlacesAPIService placesAPIService;

        public async UniTask InstallAsync(
            Dictionary<MapLayer, IMapLayerController> writer,
            List<IZoomScalingLayer> zoomScalingWriter,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IPlacesAPIService placesAPI,
            IAssetsProvisioner assetsProv,
            MapRendererSettings settings,
            CancellationToken cancellationToken
        )
        {
            placesAPIService = placesAPI;
            assetsProvisioner = assetsProv;
            mapSettings = settings;
            var prefab = await GetPrefab(cancellationToken);

            var objectsPool = new ObjectPool<FavoriteMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                defaultCapacity: PREWARM_COUNT,
                actionOnGet: (obj) => obj.gameObject.SetActive(true),
                actionOnRelease: (obj) => obj.gameObject.SetActive(false));

            var controller = new FavoritesMarkerController(
                placesAPIService,
                objectsPool,
                CreateMarker,
                configuration.FavoritesMarkersRoot,
                coordsUtils,
                cullingController
            );

            writer.Add(MapLayer.Favorites, controller);
            zoomScalingWriter.Add(controller);
        }

        private static FavoriteMarkerObject CreatePoolMethod(MapRendererConfiguration configuration, FavoriteMarkerObject prefab, ICoordsUtils coordsUtils)
        {
            FavoriteMarkerObject favorite = Object.Instantiate(prefab, configuration.FavoritesMarkersRoot);
            for (var i = 0; i < favorite.renderers.Length; i++)
                favorite.renderers[i].sortingOrder = MapRendererDrawOrder.FAVORITES;

            favorite.title.sortingOrder = MapRendererDrawOrder.FAVORITES;
            coordsUtils.SetObjectScale(favorite);
            return favorite;
        }

        private static IFavoritesMarker CreateMarker(IObjectPool<FavoriteMarkerObject> objectsPool, IMapCullingController cullingController) =>
            new FavoritesMarker(objectsPool, cullingController);

        private async UniTask<FavoriteMarkerObject> GetPrefab(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.FavoriteMarker, cancellationToken)).Value.GetComponent<FavoriteMarkerObject>();
    }
}
