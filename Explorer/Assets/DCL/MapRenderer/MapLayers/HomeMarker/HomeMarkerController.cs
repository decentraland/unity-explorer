using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.Navmap;
using DCL.PlacesAPIService;
using DCL.Prefs;
using DCL.Web3.Identities;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
    /// <summary>
    ///     Controls the home marker on the map, handling placement, highlighting, and interaction with the home location.
    ///     The home location belongs to the signed-in account: it is persisted per wallet and reloaded whenever the identity changes.
    /// </summary>
    public class HomeMarkerController : MapLayerControllerBase, IMapLayerController, IZoomScalingLayer
    {
        internal delegate IHomeMarker HomeMarkerBuilder(Transform parent);

        private readonly INavmapBus navmapBus;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IEventBus analyticsEventBus;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IHomeMarker homeMarker;

        private CancellationTokenSource highlightCt = new ();
        private CancellationTokenSource deHighlightCt = new ();
        private CancellationTokenSource placesCts = new ();

        public Vector2Int? CurrentCoordinates { get; private set; }
        public string? CurrentWorldName { get; private set; }

        public bool HomeIsSet => CurrentCoordinates.HasValue || !string.IsNullOrEmpty(CurrentWorldName);
        public bool IsWorldHome => !string.IsNullOrEmpty(CurrentWorldName);
        public bool ZoomBlocked { get; set; }

        internal HomeMarkerController(
            HomeMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            INavmapBus navmapBus,
            IPlacesAPIService placesAPIService,
            IEventBus analyticsEventBus,
            IWeb3IdentityCache identityCache)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.navmapBus = navmapBus;
            this.placesAPIService = placesAPIService;
            this.analyticsEventBus = analyticsEventBus;
            this.identityCache = identityCache;

            homeMarker = builder(instantiationParent);
        }

        internal void Initialize()
        {
            identityCache.OnIdentityChanged += RestoreHome;
            identityCache.OnIdentityCleared += RestoreHome;

            RestoreHome();
        }

        protected override void DisposeImpl()
        {
            identityCache.OnIdentityChanged -= RestoreHome;
            identityCache.OnIdentityCleared -= RestoreHome;

            highlightCt.SafeCancelAndDispose();
            deHighlightCt.SafeCancelAndDispose();
            placesCts.SafeCancelAndDispose();
            homeMarker.Dispose();
        }

        public static Vector2Int? Deserialize(IWeb3IdentityCache identityCache)
        {
            if (identityCache.Identity is not { } identity)
                return null;

            string key = PositionKey(identity);

            return DCLPlayerPrefs.HasVectorKey(key) ? DCLPlayerPrefs.GetVector2Int(key, Vector2Int.zero) : null;
        }

        public static bool HasSerializedPosition(IWeb3IdentityCache identityCache) =>
            identityCache.Identity is { } identity && DCLPlayerPrefs.HasVectorKey(PositionKey(identity));

        public static string? DeserializeWorldName(IWeb3IdentityCache identityCache)
        {
            if (identityCache.Identity is not { } identity)
                return null;

            string key = WorldNameKey(identity);

            if (!DCLPlayerPrefs.HasKey(key))
                return null;

            string value = DCLPlayerPrefs.GetString(key);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        public static bool HasSerializedWorldName(IWeb3IdentityCache identityCache) =>
            DeserializeWorldName(identityCache) != null;

        public static bool HasSerializedHome(IWeb3IdentityCache identityCache) =>
            HasSerializedWorldName(identityCache) || HasSerializedPosition(identityCache);

        internal static void Serialize(IWeb3IdentityCache identityCache, Vector2Int? coordinates)
        {
            // Without an account there is nobody to keep the home for
            if (identityCache.Identity is not { } identity)
                return;

            if (!coordinates.HasValue)
            {
                DCLPlayerPrefs.DeleteVector2Key(PositionKey(identity));
                return;
            }

            DCLPlayerPrefs.SetVector2Int(PositionKey(identity), coordinates.Value);
        }

        internal static void SerializeWorldName(IWeb3IdentityCache identityCache, string? worldName)
        {
            if (identityCache.Identity is not { } identity)
                return;

            if (string.IsNullOrEmpty(worldName))
            {
                DCLPlayerPrefs.DeleteKey(WorldNameKey(identity));
                return;
            }

            DCLPlayerPrefs.SetString(WorldNameKey(identity), worldName);
        }

        public void SetMarker(Vector2Int? coordinates)
        {
            ApplyMarker(coordinates);

            Serialize(identityCache, CurrentCoordinates);
            SerializeWorldName(identityCache, null);
            analyticsEventBus.Publish(new HomeMarkerEvents.MessageHomePositionChanged(CurrentCoordinates));
        }

        public void SetWorldMarker(string? worldName)
        {
            ApplyWorldMarker(worldName);

            Serialize(identityCache, null);
            SerializeWorldName(identityCache, worldName);
            analyticsEventBus.Publish(new HomeMarkerEvents.MessageHomePositionChanged(null, worldName));
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;

        public UniTask EnableAsync(CancellationToken cancellationToken)
        {
            if (HomeIsSet && !IsWorldHome)
                homeMarker.SetActive(true);

            return UniTask.CompletedTask;
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            homeMarker.SetActive(false);

            mapCullingController.StopTracking(homeMarker);

            return UniTask.CompletedTask;
        }

        public void ApplyCameraZoom(float baseZoom, float zoom, int zoomLevel)
        {
            if (ZoomBlocked)
                return;

            homeMarker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);
        }

        public void ResetToBaseScale()
        {
            homeMarker.ResetToBaseScale();
        }

        public bool TryHighlightObject(GameObject gameObject, out IMapRendererMarker? mapMarker)
        {
            mapMarker = null;

            if (gameObject != homeMarker.MarkerObject.gameObject)
                return false;

            mapMarker = homeMarker;
            highlightCt = highlightCt.SafeRestart();
            homeMarker.AnimateSelectionAsync(highlightCt.Token);
            return true;
        }

        public bool TryDeHighlightObject(GameObject gameObject)
        {
            if (gameObject != homeMarker.MarkerObject.gameObject)
                return false;

            deHighlightCt = deHighlightCt.SafeRestart();
            homeMarker.AnimateDeSelectionAsync(deHighlightCt.Token);
            return true;
        }

        public bool TryClickObject(GameObject gameObject, CancellationTokenSource cts, out IMapRendererMarker? mapRenderMarker)
        {
            mapRenderMarker = null;

            if (gameObject != homeMarker.MarkerObject.gameObject)
                return false;

            DisplayPlacesInfoPanelAsync(CurrentCoordinates).Forget();
            return true;
        }

        internal async UniTask DisplayPlacesInfoPanelAsync(Vector2Int? coords)
        {
            if (!coords.HasValue)
                return;

            try
            {
                placesCts = placesCts.SafeRestart();

                PlacesData.PlaceInfo placeInfo = await placesAPIService.GetPlaceAsync(coords.Value, placesCts.Token)
                                                 ?? new PlacesData.PlaceInfo(coords.Value);

                if (placesCts.IsCancellationRequested)
                    return;

                navmapBus.SelectPlaceAsync(placeInfo, placesCts.Token, true, coords.Value).Forget();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.UNSPECIFIED, "HomeMarkerController: Error while fetching place info" + e);
            }
        }

        /// <summary>
        ///     Shows the home persisted for the current account without writing it back or announcing a change.
        /// </summary>
        private void RestoreHome()
        {
            string? worldName = DeserializeWorldName(identityCache);

            if (!string.IsNullOrEmpty(worldName))
                ApplyWorldMarker(worldName);
            else
                ApplyMarker(Deserialize(identityCache));
        }

        private void ApplyMarker(Vector2Int? coordinates)
        {
            homeMarker.SetActive(coordinates.HasValue);
            CurrentCoordinates = coordinates;
            CurrentWorldName = null;

            if (CurrentCoordinates.HasValue)
                homeMarker.SetPosition(coordsUtils.CoordsToPositionWithOffset(CurrentCoordinates.Value));
        }

        private void ApplyWorldMarker(string? worldName)
        {
            homeMarker.SetActive(false);
            CurrentCoordinates = null;
            CurrentWorldName = worldName;
        }

        private static string PositionKey(IWeb3Identity identity) =>
            string.Format(DCLPrefKeys.MAP_HOME_MARKER_DATA, identity.Address);

        private static string WorldNameKey(IWeb3Identity identity) =>
            string.Format(DCLPrefKeys.MAP_HOME_WORLD_NAME, identity.Address);
    }
}
