using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.Utilities;
using DCL.UI.Profiles.Helpers;
using Utility;
using Utility.Multithreading;

namespace DCL.Chat.ChatServices
{
    /// <summary>
    ///     Manages and provides data about members in the current chat channel. This service has two primary functions:
    ///     1. Provide a continuous, lightweight member count for UI elements like the chat title bar.
    ///     2. Provide a full, detailed list of members on-demand for the member list panel, with efficient live updates.
    /// </summary>
    public class ChatMemberListService : IDisposable
    {
        private const int UNIFIED_POLL_INTERVAL_MS = 500;

        private readonly CurrentChannelService currentChannelService;
        private readonly ProfileRepositoryWrapper profileRepository;
        private readonly ObjectProxy<IFriendsService> friendsServiceProxy;
        private readonly IEventBus eventBus;

        private readonly List<ChatMemberListView.MemberData> membersBuffer = new ();

        private readonly HashSet<string> lastKnownMemberIds = new (StringComparer.OrdinalIgnoreCase);

        private int lastKnownTitleBarCount = -1;

        /// <summary>
        ///     Will be cancelled when the live update is no longer needed (the view is closed).
        /// </summary>
        private CancellationTokenSource? liveUpdateCts;

        private IDisposable? subscriptionToChannel;

        private readonly EventSubscriptionScope subscriptionToCounterUpdate = new ();
        private readonly EventSubscriptionScope subscriptionToUserStatus = new ();

        /// <summary>
        ///     Fires when the total number of members in the current channel changes.
        ///     This is a lightweight event designed for the title bar's member counter.
        /// </summary>
        public event Action<int>? OnMemberCountUpdated;

        /// <summary>
        ///     Fires with a detailed list of members after an update is triggered.
        ///     This is used to populate the full member list view.
        /// </summary>
        private Action<IReadOnlyList<ChatMemberListView.MemberData>>? onMemberListUpdated;

        public ChatMemberListService(ProfileRepositoryWrapper profileRepository,
            ObjectProxy<IFriendsService> friendsServiceProxy,
            CurrentChannelService currentChannelService,
            IEventBus eventBus)
        {
            this.profileRepository = profileRepository;
            this.friendsServiceProxy = friendsServiceProxy;
            this.currentChannelService = currentChannelService;
            this.eventBus = eventBus;
        }

        public void Dispose() =>
            Stop();

        /// <summary>
        ///     Starts the service by subscribing to channel changes.
        /// </summary>
        public void Start()
        {
            if (!friendsServiceProxy.Configured)
            {
                ReportHub.LogWarning(ReportCategory.UI, "[ChatMemberListService] FriendsService is not configured. Cannot start member list service.");
                return;
            }

            subscriptionToChannel = eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected);
            subscriptionToCounterUpdate.Add(eventBus.Subscribe<ChatEvents.NearbyUsersStatusUpdated>(UpdateCounter));
            subscriptionToCounterUpdate.Add(eventBus.Subscribe<ChatEvents.UserStatusUpdatedEvent>(UpdateCounter));

            OnChannelSelected();
        }

        private void UpdateCounter(ChatEvents.NearbyUsersStatusUpdated evt)
        {
            if (evt.ChannelId.Equals(currentChannelService.CurrentChannelId))
                UpdateAndBroadcastCount(evt.OnlineUsers.Count);
        }

        private void UpdateCounter(ChatEvents.UserStatusUpdatedEvent evt)
        {
            if (evt.ChannelId.Equals(currentChannelService.CurrentChannelId))
                UpdateAndBroadcastCount(currentChannelService.UserStateService!.OnlineParticipants.Count);
        }

        /// <summary>
        ///     Stops the service, cancels all running tasks, and unsubscribes from events.
        /// </summary>
        private void Stop()
        {
            subscriptionToChannel?.Dispose();
            subscriptionToCounterUpdate.Dispose();
            StopLiveMemberUpdates();
        }

        /// <summary>
        ///     Starts a background polling loop to check for changes in the member list.
        ///     The polling strategy is optimized based on the current channel type.
        ///     This should be called AFTER the initial list is displayed.
        /// </summary>
        public void StartLiveMemberUpdates(Action<IReadOnlyList<ChatMemberListView.MemberData>> onMemberListUpdated)
        {
            ReportHub.Log(ReportCategory.UI, "[ChatMemberListService] Starting live member updates...");

            // Emitted from the current channel user state service
            subscriptionToUserStatus.Add(eventBus.Subscribe<ChatEvents.UserStatusUpdatedEvent>(RefreshFullListIfNeeded));
            subscriptionToUserStatus.Add(eventBus.Subscribe<ChatEvents.NearbyUsersStatusUpdated>(RefreshFullListIfNeeded));

            liveUpdateCts = new CancellationTokenSource();
            this.onMemberListUpdated = onMemberListUpdated;
        }

        private void RefreshFullListIfNeeded(ChatEvents.NearbyUsersStatusUpdated evt)
        {
            if (!evt.ChannelId.Equals(currentChannelService.CurrentChannelId))
                return;

            // If the event is for the current channel, refresh the full list
            RefreshFullListIfNeededAsync(liveUpdateCts!.Token).Forget();
        }

        /// <summary>
        ///     Stops the live member list polling.
        ///     This should be called when the member list panel is closed to conserve resources.
        /// </summary>
        public void StopLiveMemberUpdates()
        {
            ReportHub.Log(ReportCategory.UI, "[ChatMemberListService] Stopping live member updates...");

            subscriptionToUserStatus?.Dispose();
            liveUpdateCts.SafeCancelAndDispose();
            onMemberListUpdated = null;
        }

        private void RefreshFullListIfNeeded(ChatEvents.UserStatusUpdatedEvent @event)
        {
            if (!@event.ChannelId.Equals(currentChannelService.CurrentChannelId))
                return;

            RefreshFullListIfNeededAsync(liveUpdateCts!.Token).Forget();
        }

        /// <summary>
        ///     Performs a single, fresh fetch of the full member list.
        ///     This should be called by the UI when the member list panel is first opened.
        /// </summary>
        public UniTask RequestInitialMemberListAsync() =>
            RefreshFullListAsync(currentChannelService.UserStateService!.OnlineParticipants, liveUpdateCts!.Token);

        private void OnChannelSelected(ChatEvents.ChannelSelectedEvent @event)
        {
            OnChannelSelected();
        }

        private void OnChannelSelected()
        {
            ResetAllMemberState();
            UpdateAndBroadcastCount(currentChannelService.UserStateService?.OnlineParticipants.Count ?? 0);
        }

        private void ResetAllMemberState()
        {
            membersBuffer.Clear();
            lastKnownMemberIds.Clear();
            lastKnownTitleBarCount = -1;
        }

        private UniTask RefreshFullListIfNeededAsync(CancellationToken ct)
        {
            ReadOnlyHashSet<string> participants = currentChannelService.UserStateService!.OnlineParticipants;

            if (lastKnownMemberIds.SetEquals(participants))
                return UniTask.CompletedTask;

            lastKnownMemberIds.Clear();

            foreach (string participant in participants)
                lastKnownMemberIds.Add(participant);

            return RefreshFullListAsync(lastKnownMemberIds, ct);
        }

        /// <summary>
        ///     Fetches the full, detailed member list for the current channel, sorts it,
        ///     and broadcasts it via the OnMemberListUpdated event.
        /// </summary>
        private async UniTask RefreshFullListAsync(IReadOnlyCollection<string> participants, CancellationToken ct)
        {
            // TODO execution must be deferred

            membersBuffer.Clear();

            try
            {
                await FetchOnlineParticipantsMemberData(participants, ct);

                if (ct.IsCancellationRequested) return;

                membersBuffer.Sort((a, b) =>
                    string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

                onMemberListUpdated?.Invoke(membersBuffer);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportHub.LogException(ex, ReportCategory.CHAT_MESSAGES); }
        }

        private static UniTask UnifiedDelay(CancellationToken ct) =>
            UniTask.Delay(UNIFIED_POLL_INTERVAL_MS, DelayType.UnscaledDeltaTime, cancellationToken: ct);

        private void UpdateAndBroadcastCount(int newCount)
        {
            if (newCount == lastKnownTitleBarCount) return;
            lastKnownTitleBarCount = newCount;

            MultithreadingUtility.InvokeOnMainThread(() => OnMemberCountUpdated?.Invoke(lastKnownTitleBarCount));
        }

        private async UniTask FetchOnlineParticipantsMemberData(IReadOnlyCollection<string> participants, CancellationToken ct)
        {
            // TODO requires pooling
            var profiles = new List<Profile>();

            // 1. Await the new asynchronous method to get the fully populated list of profiles.
            await GetProfilesFromParticipantsAsync(participants, profiles, ct);

            // If cancellation was requested during the fetch, stop processing.
            if (ct.IsCancellationRequested) return;

            // 2. The rest of your logic remains the same.
            //    By the time we get here, 'profiles' contains all members that could be found.
            foreach (Profile? profile in profiles)
            {
                if (ct.IsCancellationRequested) return;
                membersBuffer.Add(CreateMemberDataFromProfile(profile));
            }
        }

        private async UniTask GetProfilesFromParticipantsAsync(IEnumerable<string> participantIdentities, List<Profile> outProfiles, CancellationToken ct)
        {
            outProfiles.Clear();

            // 1. Create a list to hold all the asynchronous operations (Tasks).
            var profileTasks = new List<UniTask<Profile?>>();

            foreach (string? identity in participantIdentities)
            {
                if (ct.IsCancellationRequested) return;

                // 2. Start the fetch operation for each identity and add the Task to our list.
                //    We do NOT await here. This starts the download immediately.
                profileTasks.Add(profileRepository.GetProfileAsync(identity, ct));
            }

            // 3. Now, wait for ALL the tasks in the list to complete.
            //    The requests run concurrently, making this very efficient.
            Profile?[] profiles = await UniTask.WhenAll(profileTasks);

            // 4. Iterate through the results and add the valid, non-null profiles.
            foreach (Profile? profile in profiles)
            {
                if (ct.IsCancellationRequested) return;

                if (profile != null) { outProfiles.Add(profile); }
            }
        }

        private ChatMemberListView.MemberData CreateMemberDataFromProfile(Profile profile) =>
            new ()
            {
                Id = profile.UserId, Name = profile.ValidatedName, FaceSnapshotUrl = profile.Avatar.FaceSnapshotUrl, ConnectionStatus = ChatMemberConnectionStatus.Online,
                WalletId = profile.WalletId, ProfileColor = profile.UserNameColor, HasClaimedName = profile.HasClaimedName,
            };
    }
}
