using DCL.Communities;
using DCL.Utilities;
using System;
using System.Collections.Generic;

namespace DCL.VoiceChat
{
    public class JoinedCommunitiesVoiceLiveTracker : IDisposable
    {
        private readonly ICommunityCallOrchestrator orchestrator;
        private readonly ICommunityDataService communityDataService;

        private readonly Dictionary<string, IDisposable> perCommunitySubscriptions = new ();
        private readonly HashSet<string> liveJoinedCommunityIds = new ();
        private readonly ReactiveProperty<bool> hasAnyJoinedCommunityLive = new (false);

        public IReadonlyReactiveProperty<bool> HasAnyJoinedCommunityLive => hasAnyJoinedCommunityLive;

        public JoinedCommunitiesVoiceLiveTracker(
            ICommunityCallOrchestrator orchestrator,
            ICommunityDataService communityDataService)
        {
            this.orchestrator = orchestrator;
            this.communityDataService = communityDataService;

            communityDataService.CommunityJoined += OnCommunityJoined;
            communityDataService.CommunityRemoved += OnCommunityRemoved;

            foreach (string id in communityDataService.JoinedCommunityIds)
                AddSubscription(id);

            Recompute();
        }

        private void OnCommunityJoined(string communityId)
        {
            AddSubscription(communityId);
            Recompute();
        }

        private void OnCommunityRemoved(string communityId)
        {
            RemoveSubscription(communityId);
            liveJoinedCommunityIds.Remove(communityId);
            Recompute();
        }

        private void AddSubscription(string communityId)
        {
            if (string.IsNullOrEmpty(communityId)) return;
            if (perCommunitySubscriptions.ContainsKey(communityId)) return;

            IReadonlyReactiveProperty<bool> updates = orchestrator.CommunityConnectionUpdates(communityId);

            if (updates.Value)
                liveJoinedCommunityIds.Add(communityId);

            perCommunitySubscriptions[communityId] = updates.Subscribe(isLive => OnCommunityLiveChanged(communityId, isLive));
        }

        private void RemoveSubscription(string communityId)
        {
            if (perCommunitySubscriptions.TryGetValue(communityId, out IDisposable? subscription))
            {
                subscription.Dispose();
                perCommunitySubscriptions.Remove(communityId);
            }
        }

        private void OnCommunityLiveChanged(string communityId, bool isLive)
        {
            if (isLive)
                liveJoinedCommunityIds.Add(communityId);
            else
                liveJoinedCommunityIds.Remove(communityId);

            Recompute();
        }

        private void Recompute()
        {
            hasAnyJoinedCommunityLive.Value = liveJoinedCommunityIds.Count > 0;
        }

        public void Dispose()
        {
            communityDataService.CommunityJoined -= OnCommunityJoined;
            communityDataService.CommunityRemoved -= OnCommunityRemoved;

            foreach (IDisposable subscription in perCommunitySubscriptions.Values)
                subscription.Dispose();

            perCommunitySubscriptions.Clear();
            liveJoinedCommunityIds.Clear();

            hasAnyJoinedCommunityLive.ClearSubscriptionsList();
        }
    }
}
