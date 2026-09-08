using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Optimization.Pools;
using DCL.UI.Profiles.Helpers;
using Utility;
using Utility.Multithreading;

namespace DCL.Chat.ChatServices
{
    /// <summary>
    ///     Manages and provides data about members in the current chat channel. This service has two primary functions:
    ///     1. Provide a continuous, lightweight member count for UI elements like the chat title bar.
    ///     2. Provide a full, detailed list of members on-demand for the member list panel, with efficient live updates.
    ///     Both are derived from the participants whose profile could be resolved, so the counter always matches the list.
    /// </summary>
    public class ChatMemberListService : IDisposable
    {
        // Mirrors the retry horizon of CentralizedProfileRetryPolicy used for remote avatars, so the list and the avatars give up together
        internal const int MAX_UNRESOLVED_RETRIES = 5;
        private const int UNRESOLVED_RETRY_DELAY_MS = 2000;

        private static readonly Comparison<ChatMemberListData> BY_NAME = static (a, b) =>
            string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

        private readonly CurrentChannelService currentChannelService;
        private readonly ProfileRepositoryWrapper profileRepository;
        private readonly IFriendsService? friendsService;
        private readonly IEventBus eventBus;
        private readonly int unresolvedRetryDelayMs;

        private readonly List<ChatMemberListData> membersBuffer = new (PoolConstants.AVATARS_COUNT);

        private readonly HashSet<string> lastKnownMemberIds = new (StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> participantsBuffer = new (StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> unresolvedMemberIds = new (StringComparer.OrdinalIgnoreCase);
        private readonly List<string> requestBuffer = new (PoolConstants.AVATARS_COUNT);

        private readonly EventSubscriptionScope subscriptions = new ();

        private int lastKnownTitleBarCount = -1;

        /// <summary>
        ///     Lives from <see cref="Start" /> to <see cref="Stop" />: profiles are resolved while the panel is closed too, so the counter is always current.
        /// </summary>
        private CancellationTokenSource? lifetimeCts;

        /// <summary>
        ///     Only one refresh may write the buffers at a time.
        /// </summary>
        private CancellationTokenSource? refreshCts;

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
        ///     Starts the service by subscribing to channel and member status changes.
        /// </summary>
        public void Start()
        {
            if (friendsService == null)
            {
                ReportHub.LogWarning(ReportCategory.UI, "[ChatMemberListService] FriendsService is not configured. Cannot start member list service.");
                return;
            }

            lifetimeCts = new CancellationTokenSource();

            subscriptions.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
            subscriptions.Add(eventBus.Subscribe<ChatEvents.ChannelUsersStatusUpdated>(RefreshFullListIfNeeded));
            subscriptions.Add(eventBus.Subscribe<ChatEvents.UserStatusUpdatedEvent>(RefreshFullListIfNeeded));

            OnChannelSelected();
        }

        /// <summary>
        ///     Stops the service, cancels all running tasks, and unsubscribes from events.
        /// </summary>
        public void Stop()
        {
            subscriptions.Dispose();
            CancelRefresh();
            lifetimeCts.SafeCancelAndDispose();
            lifetimeCts = null;
            onMemberListUpdated = null;
        }

        /// <summary>
        ///     Registers the consumer of the full member list. It is invoked on every refresh until <see cref="StopLiveMemberUpdates" />.
        /// </summary>
        public void StartLiveMemberUpdates(Action<IReadOnlyList<ChatMemberListData>> onMemberListUpdated)
        {
            ReportHub.Log(ReportCategory.UI, "[ChatMemberListService] Starting live member updates...");
            this.onMemberListUpdated = onMemberListUpdated;
        }

        /// <summary>
        ///     Stops delivering the full member list. The counter keeps updating.
        /// </summary>
        public void StopLiveMemberUpdates()
        {
            ReportHub.Log(ReportCategory.UI, "[ChatMemberListService] Stopping live member updates...");
            onMemberListUpdated = null;
        }

        /// <summary>
        ///     Performs a single, fresh fetch of the full member list.
        /// </summary>
        public UniTask RequestInitialMemberListAsync() =>
            RefreshFullListIfNeeded(force: true);

        private void OnChannelSelected(ChatEvents.ChannelSelectedEvent @event)
        {
            OnChannelSelected();
        }

        private void OnChannelSelected()
        {
            ResetAllMemberState();
            RefreshFullListIfNeeded(force: true).Forget();
        }

        private void ResetAllMemberState()
        {
            // A refresh started for the previous channel must not publish into the new one
            CancelRefresh();
            membersBuffer.Clear();
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
            if (lifetimeCts == null)
                return UniTask.CompletedTask;

            ICurrentChannelUserStateService? userStateService = currentChannelService.UserStateService;

            if (userStateService == null)
                participantsBuffer.Clear();
            else
                userStateService.CopyOnlineParticipantsTo(participantsBuffer);

            // On panel re-open the last known set is still the one from the previous opening
            if (!force && lastKnownMemberIds.SetEquals(participantsBuffer))
                return UniTask.CompletedTask;

            lastKnownMemberIds.Clear();
            lastKnownMemberIds.UnionWith(participantsBuffer);

            refreshCts = refreshCts.SafeRestartLinked(lifetimeCts.Token);
            return RefreshFullListAsync(refreshCts.Token);
        }

        private async UniTask RefreshFullListAsync(CancellationToken ct)
        {
            try
            {
                membersBuffer.Clear();
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
                        $"[ChatMemberListService] {unresolvedMemberIds.Count} online participant(s) have no resolvable profile after {MAX_UNRESOLVED_RETRIES} retries and are excluded from the member list and counter: {string.Join(", ", unresolvedMemberIds)}");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportHub.LogException(ex, ReportCategory.CHAT_MESSAGES); }
        }

        private async UniTask FetchUnresolvedAsync(CancellationToken ct)
        {
            if (unresolvedMemberIds.Count == 0)
                return;

            requestBuffer.Clear();

            foreach (string id in unresolvedMemberIds)
                requestBuffer.Add(id);

            // Suppresses failures per id: one null or throwing profile must not drop the others
            List<Profile.CompactInfo> profiles = await profileRepository.GetProfilesAsync(requestBuffer, ct);

            if (ct.IsCancellationRequested) return;

            foreach (Profile.CompactInfo profile in profiles)
                if (unresolvedMemberIds.Remove(profile.UserId.Value))
                    membersBuffer.Add(new ChatMemberListData(profile, ChatMemberConnectionStatus.Online));
        }

        private void PublishMembers()
        {
            membersBuffer.Sort(BY_NAME);
            UpdateAndBroadcastCount(membersBuffer.Count);
            onMemberListUpdated?.Invoke(membersBuffer);
        }

        private void UpdateAndBroadcastCount(int newCount)
        {
            if (newCount == lastKnownTitleBarCount) return;
            lastKnownTitleBarCount = newCount;

            MultithreadingUtility.InvokeOnMainThread(() => OnMemberCountUpdated?.Invoke(lastKnownTitleBarCount));
        }
    }
}
