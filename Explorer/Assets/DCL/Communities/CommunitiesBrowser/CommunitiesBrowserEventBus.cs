using System;
using Utility;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserEventBus : IEventBus
    {
        private readonly IEventBus eventBus = new EventBus(invokeSubscribersOnMainThread: true);

        public void Publish<T>(T evt) => eventBus.Publish(evt);
        public IDisposable Subscribe<T>(Action<T> handler) => eventBus.Subscribe(handler);

        public void RaiseUpdateJoinedCommunityEvent(string communityId, bool success, bool isJoined) =>
            Publish(new CommunitiesBrowserEvents.UpdateJoinedCommunityEvent(communityId, success, isJoined));

        public void RaiseRequestToJoinCommunityEvent(string communityId) =>
            Publish(new CommunitiesBrowserEvents.RequestedToJoinCommunityEvent(communityId));

        public void RaiseRequestToJoinCommunityCancelledEvent(string communityId, string requestId) =>
            Publish(new CommunitiesBrowserEvents.RequestToJoinCommunityCancelledEvent(communityId, requestId));

        public void RaiseUserRemovedFromCommunity(string communityId) =>
            Publish(new CommunitiesBrowserEvents.UserRemovedFromCommunityEvent(communityId));

        public void RaiseCommunityProfileOpened(string communityId) =>
            Publish(new CommunitiesBrowserEvents.CommunityProfileOpenedEvent(communityId));

        public void RaiseClearSearchBarEvent() =>
            Publish(new CommunitiesBrowserEvents.ClearSearchBarEvent());

        public void RaiseCommunityJoinedClickedEvent(string communityId) =>
            Publish(new CommunitiesBrowserEvents.CommunityJoinedClickedEvent(communityId));
    }
}
