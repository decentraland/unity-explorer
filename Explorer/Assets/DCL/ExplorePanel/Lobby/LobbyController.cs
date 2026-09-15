using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.Events;
using DCL.EventsApi;
using DCL.Friends;
using DCL.PlacesAPIService;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connectivity;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Places;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.UI;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.ExplorePanel.Lobby
{
    /// <summary>Home section lifecycle, live refresh and avatar preview.</summary>
    public sealed partial class LobbyController : ISection, IDisposable
    {
        private static readonly Vector3 PREVIEW_POSITION = new (0, 9000, 0);
        private readonly LobbyView view;
        private readonly IPlacesAPIService placesApi;
        private readonly HttpEventsApiService eventsApi;
        private readonly FriendsConnectivityStatusTracker? connectivity;
        private readonly LobbyAvatarController avatar;
        private readonly ISelfProfile selfProfile;
        private readonly ProfileChangesBus profileChanges;
        private readonly ISpriteCache images;
        private readonly ThumbnailLoader thumbnails;
        private readonly Material ambientMaterial;
        private readonly IAnalyticsController analytics;
        private readonly PlacesCardSocialActionsController placesActions;
        private readonly EventCardActionsController eventsActions;
        private readonly IMVCManager mvcManager;
        private readonly IOnlineUsersProvider onlineUsers;
        private readonly IRealmNavigator navigator;
        private readonly IDecentralandUrlsSource urls;
        private readonly IReadOnlyLoadingStatus loadingStatus;
        private CancellationTokenSource? refreshCts;
        private bool viewFocused = true;

        public string EntryPoint { get; set; } = "navigation";
        public bool IsWorldReady => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed;
        public LobbyVisit? Visit { get; private set; }

        public event Action<ExploreSections>? NavigationRequested;
        public event Action? CloseRequested;
        public event Action<bool>? WorldReadinessChanged;

        public LobbyController(LobbyView view, IPlacesAPIService placesApi, HttpEventsApiService eventsApi,
            FriendsConnectivityStatusTracker? connectivity, LobbyAvatarController avatar,
            ISelfProfile selfProfile, ProfileChangesBus profileChanges, ISpriteCache images,
            IAnalyticsController analytics, PlacesCardSocialActionsController placesActions,
            EventCardActionsController eventsActions, IMVCManager mvcManager, IOnlineUsersProvider onlineUsers,
            IRealmNavigator navigator, IDecentralandUrlsSource urls, IReadOnlyLoadingStatus loadingStatus)
        {
            this.view = view;
            this.placesApi = placesApi;
            this.eventsApi = eventsApi;
            this.connectivity = connectivity;
            this.avatar = avatar;
            this.selfProfile = selfProfile;
            this.profileChanges = profileChanges;
            this.images = images;
            thumbnails = new ThumbnailLoader(images);
            this.analytics = analytics;
            ambientMaterial = view.Background.material;
            view.Background.material = null;
            this.placesActions = placesActions;
            this.eventsActions = eventsActions;
            this.mvcManager = mvcManager;
            this.onlineUsers = onlineUsers;
            this.navigator = navigator;
            this.urls = urls;
            this.loadingStatus = loadingStatus;
            view.CustomizeAvatarClicked += OnCustomizeAvatarClicked;
            view.BrowseEventsClicked += OnBrowseEventsClicked;
            view.BrowseFriendsClicked += OnBrowseFriendsClicked;
            view.BrowseReturnClicked += OnBrowseReturnClicked;
            view.JumpInClicked += OnJumpInClicked;
            view.DestinationClicked += OnDestinationClicked;
            view.PreviousFeaturedClicked += OnPreviousFeaturedClicked;
            view.NextFeaturedClicked += OnNextFeaturedClicked;
            view.SearchClicked += OnSearchClicked;
            view.NotificationsClicked += OnNotificationsClicked;
            view.FriendsList.InitListView(0, OnGetFriendItem);
            profileChanges.SubscribeToUpdate(ProfileChanged);
            Application.focusChanged += ApplicationFocusChanged;
            loadingStatus.CurrentStage.Subscribe(LoadingStageChanged);

            if (connectivity == null) return;
            connectivity.OnFriendBecameOnline += FriendBecameOnline;
            connectivity.OnFriendBecameAway += FriendBecameOnline;
            connectivity.OnFriendBecameOffline += FriendBecameOffline;
        }

        public void Dispose()
        {
            Deactivate();
            view.CustomizeAvatarClicked -= OnCustomizeAvatarClicked;
            view.BrowseEventsClicked -= OnBrowseEventsClicked;
            view.BrowseFriendsClicked -= OnBrowseFriendsClicked;
            view.BrowseReturnClicked -= OnBrowseReturnClicked;
            view.JumpInClicked -= OnJumpInClicked;
            view.DestinationClicked -= OnDestinationClicked;
            view.PreviousFeaturedClicked -= OnPreviousFeaturedClicked;
            view.NextFeaturedClicked -= OnNextFeaturedClicked;
            view.SearchClicked -= OnSearchClicked;
            view.NotificationsClicked -= OnNotificationsClicked;
            profileChanges.UnsubscribeToUpdate(ProfileChanged);
            Application.focusChanged -= ApplicationFocusChanged;
            loadingStatus.CurrentStage.Unsubscribe(LoadingStageChanged);
            avatar.Dispose();
            images.Clear();

            if (connectivity == null) return;
            connectivity.OnFriendBecameOnline -= FriendBecameOnline;
            connectivity.OnFriendBecameAway -= FriendBecameOnline;
            connectivity.OnFriendBecameOffline -= FriendBecameOffline;
        }

        public void Activate()
        {
            view.gameObject.SetActive(true);
            if (Visit != null) return;
            view.LiveSection.SetActive(false);
            view.ReturnStatus.text = string.Empty;
            view.FeaturedStatus.text = string.Empty;
            view.PlayerName.text = string.Empty;
            Visit = new LobbyVisit(analytics, EntryPoint, IsWorldReady);
            ShowAvatarAsync(Visit.Token).SuppressToResultAsync(ReportCategory.UI).Forget();
            EntryPoint = "navigation";
            ResetFriends();
            UpdateFocus(true);
            LoadingStageChanged(loadingStatus.CurrentStage.Value);
        }

        public void Deactivate()
        {
            UpdateFocus(false);
            if (Visit != null)
            {
                Visit.Dispose();
                Visit = null;
                avatar.OnHide();
                ClearFriends();
            }
            view.gameObject.SetActive(false);
        }

        public void Suspend() =>
            UpdateFocus(false);

        public void Resume() =>
            UpdateFocus(true);

        public void Animate(int triggerId) { }
        public void ResetAnimator() { }
        public RectTransform GetRectTransform() => (RectTransform)view.transform;

        private void ApplicationFocusChanged(bool _) =>
            UpdateFocus(viewFocused);

        private void UpdateFocus(bool focused)
        {
            viewFocused = focused;
            if (Visit is not { } visit) return;
            bool rendering = focused && Application.isFocused;
            if (rendering == (refreshCts != null)) return;
            avatar.SetRenderingActive(rendering);
            view.Background.material = rendering ? ambientMaterial : null;
            visit.SetFocused(rendering);
            if (rendering)
            {
                refreshCts = CancellationTokenSource.CreateLinkedTokenSource(visit.Token);
                RefreshWhileVisibleAsync(refreshCts.Token).SuppressToResultAsync(ReportCategory.UI).Forget();
                view.FriendsList.RefreshAllShownItem();
            }
            else
            {
                refreshCts.SafeCancelAndDispose();
                refreshCts = null;
            }
        }

        private async UniTask ShowAvatarAsync(CancellationToken ct)
        {
            if (await selfProfile.ProfileAsync(ct) is not { } profile)
                throw new InvalidOperationException("Your profile could not be found.");
            if (!ct.IsCancellationRequested)
                ProfileChanged(profile);
        }
        private void LoadingStageChanged(LoadingStatus.LoadingStage _) => WorldReadinessChanged?.Invoke(IsWorldReady);

        private void ProfileChanged(Profile profile)
        {
            if (Visit is not { } visit) return;
            view.PlayerName.text = profile.DisplayName;
            view.SetProfilePortrait(profile.Compact.FaceSnapshotUrl.Value, thumbnails, visit.Token);
            avatar.Initialize(profile.Avatar, PREVIEW_POSITION);
            avatar.OnBeforeShow();
            avatar.OnShow();
            avatar.SetRenderingActive(refreshCts != null);
        }

        private async UniTask RefreshWhileVisibleAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await RefreshAsync(ct);
                await UniTask.Delay(TimeSpan.FromSeconds(30), cancellationToken: ct);
                placeNames.Clear();
                view.FriendsList.RefreshAllShownItem();
            }
        }

        private UniTask RefreshAsync(CancellationToken ct) => UniTask.WhenAll(
            RefreshSectionAsync("featured", LoadPlacesAsync(ct), RenderFeatured, ct),
            RefreshSectionAsync("jump_back_in", LoadReturnPlacesAsync(ct), RenderReturn, ct),
            RefreshSectionAsync("live_now", LoadEventsAsync(ct), RenderEvents, ct));

        private async UniTask RefreshSectionAsync<T>(string section, UniTask<IReadOnlyList<T>> request,
            Action<IReadOnlyList<T>, CancellationToken> render, CancellationToken ct)
        {
            Result<IReadOnlyList<T>> result = await request.SuppressToResultAsync(ReportCategory.UI);
            if (ct.IsCancellationRequested) return;
            IReadOnlyList<T> items = result.Success ? result.Value : Array.Empty<T>();
            render(items, ct);
            Visit?.Content(section, items.Count, result.Success);
        }
    }
}
