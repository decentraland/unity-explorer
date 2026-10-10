using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Utilities;
using DCL.VoiceChat;
using System;
using System.Collections.Generic;
using CommunityData = DCL.Communities.CommunitiesDataProvider.DTOs.GetUserCommunitiesData.CommunityData;
using Utility;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserStateService : IDisposable
    {
        private readonly Dictionary<string, CommunityData> allCommunities = new();
        private readonly List<CommunityData> myCommunities = new();
        private readonly List<GetUserInviteRequestData.UserInviteRequestData> currentInvitationRequests = new ();
        private readonly List<GetUserInviteRequestData.UserInviteRequestData> currentJoinRequests = new ();
        private readonly EventSubscriptionScope scope = new ();
        public IReadonlyReactiveProperty<string> CurrentCommunityId { get; }

        public CommunitiesBrowserStateService(CommunitiesBrowserEventBus browserEventBus, ICommunityCallOrchestrator communityCallOrchestrator)
        {
            CurrentCommunityId = communityCallOrchestrator.CurrentCommunityId;
            scope.Add(browserEventBus.Subscribe<CommunitiesBrowserEvents.UpdateJoinedCommunityEvent>(UpdateJoinedCommunity));
            scope.Add(browserEventBus.Subscribe<CommunitiesBrowserEvents.UserRemovedFromCommunityEvent>(RemoveOneMemberFromCounter));
        }

        public IReadOnlyList<GetUserInviteRequestData.UserInviteRequestData> CurrentJoinRequests => currentJoinRequests;

        public IReadOnlyList<GetUserInviteRequestData.UserInviteRequestData> CurrentInvitationRequests => currentInvitationRequests;
        public IReadOnlyList<CommunityData> MyCommunities => myCommunities;

        public CommunityData GetCommunityDataById(string communityId) =>
            allCommunities.GetValueOrDefault(communityId);

        public void AddCommunities(CommunityData[] communities)
        {
            foreach (CommunityData community in communities)
                allCommunities[community.id] = community;
        }

        public void UpdateJoinedCommunity(CommunitiesBrowserEvents.UpdateJoinedCommunityEvent evt)
        {
            if (!evt.Success) return;

            if (!allCommunities.TryGetValue(evt.CommunityId, out CommunityData? community))
                return;

            community.SetAsJoined(evt.IsJoined);
        }

        public void RemoveOneMemberFromCounter(CommunitiesBrowserEvents.UserRemovedFromCommunityEvent evt)
        {
            if (allCommunities.TryGetValue(evt.CommunityId, out CommunityData? community))
                community.DecreaseMembersCount();
        }

        public void Dispose()
        {
            scope.Dispose();
            allCommunities.Clear();
        }

        public void ClearInvitationsRequests()
        {
            currentInvitationRequests.Clear();
        }

        public void AddInvitationsRequests(GetUserInviteRequestData.UserInviteRequestData[] newInvitations)
        {
            currentInvitationRequests.AddRange(newInvitations);
        }

        public void ClearJoinRequests()
        {
            currentJoinRequests.Clear();
        }

        public void AddJoinRequests(GetUserInviteRequestData.UserInviteRequestData[] joinRequests)
        {
            currentJoinRequests.AddRange(joinRequests);
        }

        public void AddJoinRequest(GetUserInviteRequestData.UserInviteRequestData joinRequest)
        {
            currentJoinRequests.Add(joinRequest);
        }


        public void RemoveJoinRequestAt(int indexToRemove)
        {
            currentJoinRequests.RemoveAt(indexToRemove);
        }

        public void RemoveInvitation(GetUserInviteRequestData.UserInviteRequestData invitationRequest)
        {
            currentInvitationRequests.Remove(invitationRequest);
        }

        public void RemoveJoinRequest(GetUserInviteRequestData.UserInviteRequestData joinRequest)
        {
            currentJoinRequests.Remove(joinRequest);
        }

        public void UpdateRequestToJoinCommunity(string communityId, string? requestId, bool isRequestedToJoin, bool isSuccess, bool alreadyExistsInvitation)
        {
            if (!isSuccess) return;

            if (!allCommunities.TryGetValue(communityId, out CommunityData? resultCommunityData)) return;

            resultCommunityData.inviteOrRequestId = requestId;

            if (alreadyExistsInvitation) return;

            resultCommunityData.pendingActionType = isRequestedToJoin ? InviteRequestAction.request_to_join : InviteRequestAction.none;

            if (resultCommunityData.pendingActionType == InviteRequestAction.none)
                resultCommunityData.inviteOrRequestId = null;
        }

        public void SetMyCommunities(CommunityData[] communities)
        {
            myCommunities.Clear();
            myCommunities.AddRange(communities);
        }
    }
}
