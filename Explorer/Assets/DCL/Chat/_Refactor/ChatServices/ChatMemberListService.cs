using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Optimization.Pools;
using DCL.UI.Profiles.Helpers;
using DCL.Utility.Types;
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
        // Mirrors the retry horizon of CentralizedProfileRetryPolicy used for remote avatars, so the list and the avatars give up together
        internal const int MAX_UNRESOLVED_RETRIES = 5;
        private const int UNRESOLVED_RETRY_DELAY_MS = 2000;

        private static readonly Comparison<ChatMemberListData> BY_NAME = static (a, b) =>
            string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

        private static readonly Comparison<ChatMemberListData> BY_WALLET = static (a, b) =>
            string.Compare(a.Profile.UserId.Value, b.Profile.UserId.Value, StringComparison.OrdinalIgnoreCase);

        private readonly CurrentChannelService currentChannelService;
        private readonly ProfileRepositoryWrapper profileRepository;
        private readonly IFriendsService? friendsService;
        private readonly IEventBus eventBus;
        private readonly int unresolvedRetryDelayMs;

        // Published list: resolved members first, then one placeholder row per wallet whose profile is not available yet
        private readonly List<ChatMemberListData> membersBuffer = new (PoolConstants.AVATARS_COUNT);
        private readonly List<ChatMemberListData> resolvedMembers = new (PoolConstants.AVATARS_COUNT);
        private readonly List<ChatMemberListData> placeholderMembers = new (PoolConstants.AVATARS_COUNT);

        private readonly HashSet<string> lastKnownMemberIds = new (StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> participantsBuffer = new (StringComparer.OrdinalIgnoreCase);

        // Profile UserIds can differ in casing from LiveKit identities
        private readonly HashSet<string> unresolvedMemberIds = new (StringComparer.OrdinalIgnoreCase);
        private readonly List<string> requestBuffer = new (PoolConstants.AVATARS_COUNT);

        private readonly EventSubscriptionScope subscriptionToCounterUpdate = new ();
        private readonly EventSubscriptionScope subscriptionToUserStatus = new ();

        private int lastKnownTitleBarCount = -1;

        /// <summary>
        ///     Will be cancelled when the live update is no longer needed (the view is closed).
        /// </summary>
        private CancellationTokenSource? liveUpdateCts;

        /// <summary>
        ///     Only one refresh may write the buffers at a time; restarted on every refresh and linked to <see cref="liveUpdateCts" />.
        /// </summary>
        private CancellationTokenSource? refreshCts;

        private IDisposable? subscriptionToChannel;

        /// <summary>
        ///     Fires when the total number of members in the current channel changes.
        ///     This is a lightweight event designed for the title bar's member counter.
        /// </summary>
        public event Action<int>? OnMemberCountUpdated;

        /// <summary>
        ///     Fires with a detailed list of members after an update is triggered.
        ///     This is used to populate the full member list view.
        /// </summary>
        private Action<IReadOnlyList<ChatMemberListData>>? onMemberListUpdated;

        public ChatMemberListService(ProfileRepositoryWrapper profileRepository,
            IFriendsService? friendsService,
            CurrentChannelService currentChannelService,
            IEventBus eventBus) : this(profileRepository, friendsService, currentChannelService, eventBus, UNRESOLVED_RETRY_DELAY_MS) { }

        internal ChatMemberListService(ProfileRepositoryWrapper profileRepository,
            IFriendsService? friendsService,
            CurrentChannelService currentChannelService,
            IEventBus eventBus,
            int unresolvedRetryDelayMs)
        {
            this.profileRepository = profileRepository;
            this.friendsService = friendsService;
            this.currentChannelService = currentChannelService;
            this.eventBus = eventBus;
            this.unresolvedRetryDelayMs = unresolvedRetryDelayMs;
        }

        public void Dispose() =>
            Stop();

        /// <summary>
        ///     Starts the service by subscribing to channel changes.
        /// </summary>
        public void Start()
        {
            if (friendsService == null)
            {
                ReportHub.LogWarning(ReportCategory.UI, "[ChatMemberListService] FriendsService is not configured. Cannot start member list service.");
                return;
            }

            subscriptionToChannel = eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected);
            subscriptionToCounterUpdate.Add(eventBus.Subscribe<ChatEvents.ChannelUsersStatusUpdated>(UpdateCounter));
            subscriptionToCounterUpdate.Add(eventBus.Subscribe<ChatEvents.UserStatusUpdatedEvent>(UpdateCounter));

            OnChannelSelected();
        }

        /// <summary>
        ///     Stops the service, cancels all running tasks, and unsubscribes from events.
        /// </summary>
        public void Stop()
        {
            subscriptionToChannel?.Dispose();
            subscriptionToCounterUpdate.Dispose();
            StopLiveMemberUpdates();
        }

        /// <summary>
        ///     Subscribes to member status changes of the current channel so the full list is refreshed while the panel is open.
        ///     This should be called AFTER the initial list is displayed.
        /// </summary>
        public void StartLiveMemberUpdates(Action<IReadOnlyList<ChatMemberListData>> onMemberListUpdated)
        {
            ReportHub.Log(ReportCategory.UI, "[ChatMemberListService] Starting live member updates...");

            // Emitted from the current channel user state service
            subscriptionToUserStatus.Add(eventBus.Subscribe<ChatEvents.UserStatusUpdatedEvent>(RefreshFullListIfNeeded));
            subscriptionToUserStatus.Add(eventBus.Subscribe<ChatEvents.ChannelUsersStatusUpdated>(RefreshFullListIfNeeded));

            liveUpdateCts = new CancellationTokenSource();
            this.onMemberListUpdated = onMemberListUpdated;
        }

        /// <summary>
        ///     Stops the live member list updates.
        ///     This should be called when the member list panel is closed to conserve resources.
        /// </summary>
        public void StopLiveMemberUpdates()
        {
            ReportHub.Log(ReportCategory.UI, "[ChatMemberListService] Stopping live member updates...");

            subscriptionToUserStatus.Dispose();
            CancelRefresh();
            liveUpdateCts.SafeCancelAndDispose();
            onMemberListUpdated = null;
        }

        /// <summary>
        ///     Performs a single, fresh fetch of the full member list.
        ///     This should be called by the UI when the member list panel is first opened.
        /// </summary>
        public UniTask RequestInitialMemberListAsync() =>
            RefreshFullListIfNeeded(force: true);

        private void UpdateCounter(ChatEvents.ChannelUsersStatusUpdated evt)
        {
            if (evt.Qualifies(currentChannelService.CurrentChannel))
                UpdateAndBroadcastCount(evt.OnlineUsers.Count);
        }

        private void UpdateCounter(ChatEvents.UserStatusUpdatedEvent evt)
        {
            if (evt.ChannelId.Equals(currentChannelService.CurrentChannelId))
                UpdateAndBroadcastCount(currentChannelService.UserStateService!.OnlineParticipants.Count);
        }

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
            // A refresh started for the previous channel must not publish into the new one
            CancelRefresh();
            membersBuffer.Clear();
            resolvedMembers.Clear();
            lastKnownMemberIds.Clear();
            unresolvedMemberIds.Clear();
            lastKnownTitleBarCount = -1;
        }

        private void CancelRefresh()
        {
            refreshCts.SafeCancelAndDispose();
            refreshCts = null;
        }

        private void RefreshFullListIfNeeded(ChatEvents.ChannelUsersStatusUpdated evt)
        {
            if (!evt.Qualifies(currentChannelService.CurrentChannel))
                return;

            RefreshFullListIfNeeded(force: false).Forget();
        }

        private void RefreshFullListIfNeeded(ChatEvents.UserStatusUpdatedEvent @event)
        {
            if (!@event.ChannelId.Equals(currentChannelService.CurrentChannelId))
                return;

            RefreshFullListIfNeeded(force: false).Forget();
        }

        private UniTask RefreshFullListIfNeeded(bool force)
        {
            currentChannelService.UserStateService!.CopyOnlineParticipantsTo(participantsBuffer);

            // On panel re-open the last known set is still the one from the previous opening
            if (!force && lastKnownMemberIds.SetEquals(participantsBuffer))
                return UniTask.CompletedTask;

            lastKnownMemberIds.Clear();
            lastKnownMemberIds.UnionWith(participantsBuffer);

            refreshCts = refreshCts.SafeRestartLinked(liveUpdateCts!.Token);
            return RefreshFullListAsync(refreshCts.Token);
        }

        /// <summary>
        ///     Publishes one row per online wallet right away (a wallet placeholder when the profile is not available yet),
        ///     then keeps re-requesting the unresolved profiles for a bounded time and republishes as they land.
        /// </summary>
        private async UniTask RefreshFullListAsync(CancellationToken ct)
        {
            try
            {
                resolvedMembers.Clear();
                unresolvedMemberIds.Clear();
                unresolvedMemberIds.UnionWith(lastKnownMemberIds);

                await FetchUnresolvedAsync(ct);

                if (ct.IsCancellationRequested) return;

                PublishMembers();

                for (var attempt = 0; unresolvedMemberIds.Count > 0 && attempt < MAX_UNRESOLVED_RETRIES; attempt++)
                {
                    await UniTask.Delay(unresolvedRetryDelayMs, DelayType.Realtime, cancellationToken: ct);

                    int unresolvedBefore = unresolvedMemberIds.Count;

                    await FetchUnresolvedAsync(ct);

                    if (ct.IsCancellationRequested) return;

                    if (unresolvedMemberIds.Count < unresolvedBefore)
                        PublishMembers();
                }

                if (unresolvedMemberIds.Count > 0)
                    ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES,
                        $"[ChatMemberListService] {unresolvedMemberIds.Count} online member(s) have no resolvable profile after {MAX_UNRESOLVED_RETRIES} retries and stay listed by wallet: {string.Join(", ", unresolvedMemberIds)}");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportHub.LogException(ex, ReportCategory.CHAT_MESSAGES); }
        }

        private async UniTask FetchUnresolvedAsync(CancellationToken ct)
        {
            requestBuffer.Clear();

            foreach (string id in unresolvedMemberIds)
                requestBuffer.Add(id);

            // Suppresses failures per id: one null or throwing profile must not drop the others
            List<Profile.CompactInfo> profiles = await profileRepository.GetProfilesAsync(requestBuffer, ct);

            if (ct.IsCancellationRequested) return;

            foreach (Profile.CompactInfo profile in profiles)
                if (unresolvedMemberIds.Remove(profile.UserId.Value))
                    resolvedMembers.Add(new ChatMemberListData(profile, ChatMemberConnectionStatus.Online, hasProfile: true));
        }

        private void PublishMembers()
        {
            placeholderMembers.Clear();

            foreach (string identity in unresolvedMemberIds)
            {
                Option<UserId> userId = UserId.New(identity);

                if (!userId.Has)
                    continue;

                placeholderMembers.Add(new ChatMemberListData(new Profile.CompactInfo(userId.Value, PlaceholderName(identity)), ChatMemberConnectionStatus.Online, hasProfile: false));
            }

            resolvedMembers.Sort(BY_NAME);
            placeholderMembers.Sort(BY_WALLET);

            membersBuffer.Clear();
            membersBuffer.AddRange(resolvedMembers);
            membersBuffer.AddRange(placeholderMembers);

            onMemberListUpdated?.Invoke(membersBuffer);
        }

        /// <summary>
        ///     The row shows this as the name and the wallet's last four characters as the hashtag, like an unclaimed name.
        /// </summary>
        private static string PlaceholderName(string identity) =>
            identity.Length > 6 ? identity[..6] : identity;

        private void UpdateAndBroadcastCount(int newCount)
        {
            if (newCount == lastKnownTitleBarCount) return;
            lastKnownTitleBarCount = newCount;

            MultithreadingUtility.InvokeOnMainThread(() => OnMemberCountUpdated?.Invoke(lastKnownTitleBarCount));
        }
    }
}
