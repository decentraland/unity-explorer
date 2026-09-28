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
    ///     The home location belongs to the signed-in account: it is persisted per wallet address and reloaded whenever the identity changes.
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

        /// <summary>
        ///     The account whose home applies before sign-in: the session restored from the previous run or, when that
        ///     session has already expired, the last account the map loaded a home for. The account-less home of earlier
        ///     releases is adopted by it.
        /// </summary>
        public static string? ResolveStartupAccount(IWeb3IdentityCache identityCache)
        {
            string? account = AccountOf(identityCache);

            if (account == null && DCLPlayerPrefs.HasKey(DCLPrefKeys.MAP_HOME_LAST_ACCOUNT))
                account = DCLPlayerPrefs.GetString(DCLPrefKeys.MAP_HOME_LAST_ACCOUNT);

            if (!string.IsNullOrEmpty(account))
                AdoptLegacyHome(account);

            return string.IsNullOrEmpty(account) ? null : account;
        }

        public static Vector2Int? Deserialize(string? account)
        {
            if (account == null)
                return null;

            string key = PositionKey(account);

            return DCLPlayerPrefs.HasVectorKey(key) ? DCLPlayerPrefs.GetVector2Int(key, Vector2Int.zero) : null;
        }

        public static bool HasSerializedPosition(string? account) =>
            account != null && DCLPlayerPrefs.HasVectorKey(PositionKey(account));

        public static string? DeserializeWorldName(string? account)
        {
            if (account == null)
                return null;

            string key = WorldNameKey(account);

            if (!DCLPlayerPrefs.HasKey(key))
                return null;

            string value = DCLPlayerPrefs.GetString(key);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        public static bool HasSerializedWorldName(string? account) =>
            DeserializeWorldName(account) != null;

        public static bool HasSerializedHome(string? account) =>
            HasSerializedWorldName(account) || HasSerializedPosition(account);

        internal static void Serialize(string? account, Vector2Int? coordinates)
        {
            // Without an account there is nobody to keep the home for
            if (account == null)
                return;

            if (!coordinates.HasValue)
            {
                DCLPlayerPrefs.DeleteVector2Key(PositionKey(account));
                return;
            }

            DCLPlayerPrefs.SetVector2Int(PositionKey(account), coordinates.Value);
        }

        internal static void SerializeWorldName(string? account, string? worldName)
        {
            if (account == null)
                return;

            if (string.IsNullOrEmpty(worldName))
            {
                DCLPlayerPrefs.DeleteKey(WorldNameKey(account));
                return;
            }

            DCLPlayerPrefs.SetString(WorldNameKey(account), worldName);
        }

        public void SetMarker(Vector2Int? coordinates)
        {
            ApplyMarker(coordinates);

            string? account = AccountOf(identityCache);
            Serialize(account, CurrentCoordinates);
            SerializeWorldName(account, null);
            analyticsEventBus.Publish(new HomeMarkerEvents.MessageHomePositionChanged(CurrentCoordinates));
        }

        public void SetWorldMarker(string? worldName)
        {
            ApplyWorldMarker(worldName);

            string? account = AccountOf(identityCache);
            Serialize(account, null);
            SerializeWorldName(account, worldName);
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
        ///     Shows the home persisted for the signed-in account without writing it back or announcing a change.
        /// </summary>
        private void RestoreHome()
        {
            string? account = AccountOf(identityCache);

            if (account != null)
            {
                AdoptLegacyHome(account);
                DCLPlayerPrefs.SetString(DCLPrefKeys.MAP_HOME_LAST_ACCOUNT, account);
            }

            string? worldName = DeserializeWorldName(account);

            if (!string.IsNullOrEmpty(worldName))
                ApplyWorldMarker(worldName);
            else
                ApplyMarker(Deserialize(account));
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

        /// <summary>
        ///     Moves the account-less home written by earlier releases under the given account, unless that account already
        ///     has one, and removes the old keys so no later account inherits it.
        /// </summary>
        private static void AdoptLegacyHome(string account)
        {
            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.MAP_HOME_WORLD_NAME_LEGACY))
            {
                string legacyWorldName = DCLPlayerPrefs.GetString(DCLPrefKeys.MAP_HOME_WORLD_NAME_LEGACY);

                if (!string.IsNullOrEmpty(legacyWorldName) && !HasSerializedHome(account))
                    DCLPlayerPrefs.SetString(WorldNameKey(account), legacyWorldName);

                DCLPlayerPrefs.DeleteKey(DCLPrefKeys.MAP_HOME_WORLD_NAME_LEGACY);
            }

            if (DCLPlayerPrefs.HasVectorKey(DCLPrefKeys.MAP_HOME_MARKER_DATA_LEGACY))
            {
                if (!HasSerializedHome(account))
                    DCLPlayerPrefs.SetVector2Int(PositionKey(account), DCLPlayerPrefs.GetVector2Int(DCLPrefKeys.MAP_HOME_MARKER_DATA_LEGACY, Vector2Int.zero));

                DCLPlayerPrefs.DeleteVector2Key(DCLPrefKeys.MAP_HOME_MARKER_DATA_LEGACY);
            }
        }

        private static string? AccountOf(IWeb3IdentityCache identityCache) =>
            identityCache.Identity is { } identity ? identity.Address.ToString() : null;

        private static string PositionKey(string account) =>
            string.Format(DCLPrefKeys.MAP_HOME_MARKER_DATA, account);

        private static string WorldNameKey(string account) =>
            string.Format(DCLPrefKeys.MAP_HOME_WORLD_NAME, account);
    }
}
