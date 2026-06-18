using DCL.Clipboard;
using DCL.EventsApi;
using DCL.Friends;
using DCL.Input;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.PlacesAPIService;
using DCL.Places;
using DCL.PrivateWorlds;
using DCL.RealmNavigation;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using ECS.SceneLifeCycle.Realm;
using MVC;

namespace DCL.AuthenticationScreenFlow
{
    /// <summary>
    /// Bundles the dependencies the authentication screen needs to build its own <see cref="DCL.Places.PlacesController" />
    /// (the place picker shown after login) plus the destination primitives the picker commits to before the loading screen.
    /// Grouped to keep <see cref="AuthenticationScreenController" />'s constructor manageable.
    /// </summary>
    public readonly struct AuthPlacesDependencies
    {
        public readonly IPlacesAPIService PlacesAPIService;
        public readonly IRealmNavigator RealmNavigator;
        public readonly IGlobalRealmController RealmController;
        public readonly StartParcel StartParcel;
        public readonly ISystemClipboard Clipboard;
        public readonly ObjectProxy<IFriendsService> FriendServiceProxy;
        public readonly ProfileRepositoryWrapper ProfileRepositoryWrapper;
        public readonly HomePlaceEventBus HomePlaceEventBus;
        public readonly IWorldPermissionsService WorldPermissionsService;
        public readonly HttpEventsApiService EventsApiService;
        public readonly ICursor Cursor;
        public readonly PlaceCategoriesSO PlaceCategories;
        public readonly IMVCManager MvcManager;

        public AuthPlacesDependencies(
            IPlacesAPIService placesAPIService,
            IRealmNavigator realmNavigator,
            IGlobalRealmController realmController,
            StartParcel startParcel,
            ISystemClipboard clipboard,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            HomePlaceEventBus homePlaceEventBus,
            IWorldPermissionsService worldPermissionsService,
            HttpEventsApiService eventsApiService,
            ICursor cursor,
            PlaceCategoriesSO placeCategories,
            IMVCManager mvcManager)
        {
            PlacesAPIService = placesAPIService;
            RealmNavigator = realmNavigator;
            RealmController = realmController;
            StartParcel = startParcel;
            Clipboard = clipboard;
            FriendServiceProxy = friendServiceProxy;
            ProfileRepositoryWrapper = profileRepositoryWrapper;
            HomePlaceEventBus = homePlaceEventBus;
            WorldPermissionsService = worldPermissionsService;
            EventsApiService = eventsApiService;
            Cursor = cursor;
            PlaceCategories = placeCategories;
            MvcManager = mvcManager;
        }
    }
}
