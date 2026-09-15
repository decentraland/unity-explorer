using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CommunicationData.URLHelpers;
using DCL.Friends.UI.FriendPanel;
using DCL.EventsApi;
using DCL.Utilities.Extensions;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connectivity;
using DCL.PlacesAPIService;
using DCL.UI;
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
    public sealed partial class LobbyController
    {
        private void RenderFeatured(IReadOnlyList<PlacesData.PlaceInfo> places, CancellationToken ct)
        {
            RenderPlaces(places, view.PlaceCard, view.FeaturedContent, "featured", ct);
            view.FeaturedStatus.text = places.Count == 0 ? "Explore Places to find a place to visit." : string.Empty;
            view.DestinationStatus.text = "Explore the plaza";
            foreach (PlacesData.PlaceInfo place in places)
                if (place.base_position == "0,0") view.DestinationStatus.text = place.user_count == 1 ? "1 person online" : $"{place.user_count:N0} people online";
        }

        private void RenderReturn(IReadOnlyList<PlacesData.PlaceInfo> places, CancellationToken ct)
        {
            RenderPlaces(places, view.ReturnCard, view.ReturnContent, "jump_back_in", ct);
            view.ReturnStatus.text = places.Count == 0 ? "Visit or favorite a place to keep it here." : string.Empty;
        }

        private void RenderEvents(IReadOnlyList<EventDTO> events, CancellationToken ct)
        {
            view.LiveSection.SetActive(events.Count > 0);
            RenderCards(events, view.EventCard, view.EventsContent, (card, liveEvent) =>
            {
                card.Bind(liveEvent.name, liveEvent.scene_name,
                    liveEvent.connected_addresses == null ? "Live now" : $"Live · {liveEvent.connected_addresses.Length:N0} here",
                    liveEvent.image, thumbnails, ct, () => TravelAsync("live_now", "event", liveEvent.id, card.transform.GetSiblingIndex(),
                        travelCt => eventsActions.JumpInEventAsync(liveEvent, travelCt)).SuppressToResultAsync(ReportCategory.UI).Forget());
            });
        }

        private void RenderPlaces(IReadOnlyList<PlacesData.PlaceInfo> places,
            LobbyCardView prefab, RectTransform parent, string section, CancellationToken ct)
        {
            RenderCards(places, prefab, parent, (card, place) =>
            {
                card.Bind(place.title, string.Empty, $"{place.user_count:N0} online", place.image, thumbnails, ct,
                    () => TravelAsync(section, "place", place.id, card.transform.GetSiblingIndex(),
                        travelCt => placesActions.JumpInPlaceAsync(place, travelCt)).SuppressToResultAsync(ReportCategory.UI).Forget());
            });
        }

        private void RenderCards<T>(IReadOnlyList<T> items, LobbyCardView prefab, Transform parent, Action<LobbyCardView, T> bind)
        {
            for (int i = items.Count; i < parent.childCount; i++) parent.GetChild(i).gameObject.SetActive(false);
            for (int i = 0; i < items.Count; i++)
            {
                LobbyCardView card = i < parent.childCount
                    ? parent.GetChild(i).GetComponent<LobbyCardView>()
                    : UnityEngine.Object.Instantiate(prefab, parent);
                bind(card, items[i]);
            }
        }

        private async UniTask TravelAsync(string section, string targetType, string? targetId, int? position,
            Func<CancellationToken, UniTask<EnumResult<TaskError>>> operation)
        {
            if (Visit is { } visit && await visit.TravelAsync(section, targetType, targetId, position, operation, view.destroyCancellationToken)
                && ReferenceEquals(Visit, visit))
                CloseRequested?.Invoke();
        }

        private async UniTask<EnumResult<TaskError>> JoinFriendAsync(string userId, CancellationToken ct)
        {
            IReadOnlyCollection<OnlineUserData> locations = await onlineUsers.GetAsync(new[] { userId }, ct);
            foreach (OnlineUserData location in locations)
            {
                if (!string.Equals(location.avatarId, userId, StringComparison.OrdinalIgnoreCase)) continue;
                if (location.IsInWorld && location.worldName is { } worldName)
                    return (await navigator.TryChangeRealmAsync(URLDomain.FromString(new ENS(worldName).ConvertEnsToWorldUrl(urls.Url(DecentralandUrl.WorldServer))),
                        ct, location.position.ToParcel(), isWorld: true)).As(ChangeRealmErrors.AsTaskError);
                return await navigator.TeleportToParcelAsync(location.position.ToParcel(), ct, false);
            }
            return EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "This friend is no longer online.");
        }

        private void OnCustomizeAvatarClicked() { Visit?.Action("customize_avatar", "home"); NavigationRequested?.Invoke(ExploreSections.Backpack); }
        private void OnJumpInClicked() =>
            JumpIntoPlaza("welcome");

        private void OnDestinationClicked() =>
            JumpIntoPlaza("destination");

        private void OnNotificationsClicked(RectTransform _) =>
            Visit?.Action("notifications", "navigation");

        private void JumpIntoPlaza(string section) => TravelAsync(section, "place", "genesis-plaza", null,
            ct => navigator.TeleportToParcelAsync(Vector2Int.zero, ct, false)).SuppressToResultAsync(ReportCategory.UI).Forget();

        private void OnPreviousFeaturedClicked() => ScrollFeatured(-1);
        private void OnNextFeaturedClicked() => ScrollFeatured(1);

        private void ScrollFeatured(int direction)
        {
            Visit?.Action(direction > 0 ? "next" : "previous", "featured");
            var scroll = view.FeaturedContent.parent.GetComponent<UnityEngine.UI.ScrollRect>();
            float overflow = view.FeaturedContent.rect.width - scroll.viewport.rect.width;
            if (overflow <= 0) return;
            scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition + (direction * scroll.viewport.rect.width / overflow));
        }

        private void OnBrowseEventsClicked() { Visit?.Action("browse_events", "home"); NavigationRequested?.Invoke(ExploreSections.Events); }
        private void OnSearchClicked() { Visit?.Action("search", "navigation"); NavigationRequested?.Invoke(ExploreSections.Places); }
        private void OnBrowseReturnClicked() { Visit?.Action("see_all", "jump_back_in"); NavigationRequested?.Invoke(ExploreSections.Places); }
        private void OnBrowseFriendsClicked()
        {
            Visit?.Action("friends", "home");
            mvcManager.ShowAndForget(FriendsPanelController.IssueCommand(default(FriendsPanelParameter)));
        }
    }
}
