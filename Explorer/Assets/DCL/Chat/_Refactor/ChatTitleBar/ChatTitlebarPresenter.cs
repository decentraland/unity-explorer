using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.ChatViewModels;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Web3;
using DG.Tweening;
using MVC;
using System.Collections.Generic;
using System.Linq;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatCommands.DCL.Chat.ChatUseCases;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatServices.ChatContextService;
using DCL.Communities;
using DCL.Settings.Settings;
using DCL.UI;
using DCL.UI.Controls.Configs;
using DCL.UI.ProfileElements;
using UnityEngine;
using UnityEngine.UI;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Chat
{
    public class ChatTitlebarPresenter : IDisposable
    {
        private readonly ChatTitlebarView2 view;
        private readonly ChatConfig.ChatConfig chatConfig;
        private readonly IEventBus eventBus;
        private readonly CommunityDataService communityDataService;
        private readonly GetTitlebarViewModelCommand getTitlebarViewModel;
        private readonly GetCommunityThumbnailCommand getCommunityThumbnailCommand;
        private readonly DeleteChatHistoryCommand deleteChatHistoryCommand;
        private readonly CurrentChannelService currentChannelService;
        private readonly ChatContextMenuService chatContextMenuService;
        private readonly ChatMemberListService chatMemberListService;
        private readonly CancellationTokenSource lifeCts = new ();
        private readonly EventSubscriptionScope scope = new ();
        private CancellationTokenSource profileLoadCts = new ();
        private CancellationTokenSource thumbCts = new ();
        private CancellationTokenSource? activeMenuCts;
        private UniTaskCompletionSource? activeMenuTcs;
        private ChatTitlebarViewModel? currentViewModel;
        private GenericContextMenu? contextMenuInstance;
        private readonly GenericContextMenu contextMenuConfiguration;
        private ToggleWithCheckContextMenuControlSettings[]? notificationPingToggles;

        public ChatTitlebarPresenter(
            ChatTitlebarView2 view,
            ChatConfig.ChatConfig chatConfig,
            IEventBus eventBus,
            CommunityDataService communityDataService,
            CurrentChannelService currentChannelService,
            ChatMemberListService chatMemberListService,
            ChatContextMenuService chatContextMenuService,
            GetTitlebarViewModelCommand getTitlebarViewModel,
            GetCommunityThumbnailCommand getCommunityThumbnailCommand,
            DeleteChatHistoryCommand deleteChatHistoryCommand)
        {
            this.view = view;
            this.chatConfig = chatConfig;
            this.eventBus = eventBus;
            this.communityDataService = communityDataService;
            this.chatMemberListService = chatMemberListService;
            this.currentChannelService = currentChannelService;
            this.chatContextMenuService = chatContextMenuService;
            this.getTitlebarViewModel = getTitlebarViewModel;
            this.getCommunityThumbnailCommand = getCommunityThumbnailCommand;
            this.deleteChatHistoryCommand = deleteChatHistoryCommand;

            view.Initialize();
            view.OnCloseRequested += OnCloseRequested;
            view.OnMembersToggleRequested += OnMembersToggleRequested;
            view.OnContextMenuRequested += OnChatContextMenuRequested;
            view.OnProfileContextMenuRequested += OnProfileContextMenuRequested;
            view.OnCommunityContextMenuRequested += OnCommunityContextMenuRequested;

            communityDataService.CommunityMetadataUpdated += CommunityMetadataUpdated;
            chatMemberListService.OnMemberCountUpdated += OnMemberCountUpdated;

            scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelUsersStatusUpdated>(OnChannelUsersStatusUpdated));
            scope.Add(this.eventBus.Subscribe<ChatEvents.UserStatusUpdatedEvent>(OnLiveUserConnectionStateChange));
            scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
            scope.Add(this.eventBus.Subscribe<ChatEvents.ChatResetEvent>(OnChatResetEvent));

            var contextMenuSettings = chatConfig.communityChatConversationContextMenuSettings;
            contextMenuConfiguration = new GenericContextMenu(contextMenuSettings.Width,
                    contextMenuSettings.Offset,
                    contextMenuSettings.VerticalLayoutPadding,
                    contextMenuSettings.ElementsSpacing,
                    ContextMenuOpenDirection.TOP_LEFT)
                .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.ViewCommunityText,
                    contextMenuSettings.ViewCommunitySprite,
                    OpenCommunityCard));

            InitializeChannelContextMenu();
        }

        private void CommunityMetadataUpdated(CommunityMetadataUpdatedEvent evt)
        {
            // Only care if we’re viewing a Community channel and it matches
            if (currentViewModel == null || currentViewModel.ViewMode != TitlebarViewMode.Community)
                return;

            if (communityDataService.TryGetCommunity(evt.ChannelId, out var cd))
            {
                currentViewModel.Username = cd.name;
                view.defaultTitlebarView.Setup(currentViewModel);
                view.membersTitlebarView.SetChannelName(currentViewModel);

                if (cd.thumbnails?.raw != null)
                    RefreshTitlebarCommunityThumbnailAsync(cd.thumbnails?.raw).Forget();
            }
        }

        private void OnChatResetEvent(ChatEvents.ChatResetEvent evt)
        {
            profileLoadCts.SafeCancelAndDispose();
            thumbCts.SafeCancelAndDispose();
            currentViewModel = null;
            view.defaultTitlebarView.Setup(ChatTitlebarViewModel.CreateLoading(TitlebarViewMode.Nearby));
            view.membersTitlebarView.SetChannelName(ChatTitlebarViewModel.CreateLoading(TitlebarViewMode.Nearby));
        }

        private void OnDeleteChatHistoryButtonClicked()
        {
            deleteChatHistoryCommand.Execute();
        }

        private void OpenCommunityCard()
        {
            communityDataService
                .OpenCommunityCard(currentChannelService.CurrentChannel);
        }

        private GameObject contextMenuToggleGroup;
        public void Dispose()
        {
            view.OnCloseRequested -= OnCloseRequested;
            view.OnMembersToggleRequested -= OnMembersToggleRequested;
            view.OnProfileContextMenuRequested -= OnProfileContextMenuRequested;
            view.OnCommunityContextMenuRequested -= OnCommunityContextMenuRequested;
            view.OnContextMenuRequested -= OnChatContextMenuRequested;
            chatMemberListService.OnMemberCountUpdated -= OnMemberCountUpdated;

            if (contextMenuToggleGroup != null)
                Object.Destroy(contextMenuToggleGroup);

            lifeCts.SafeCancelAndDispose();
            profileLoadCts.SafeCancelAndDispose();
            scope.Dispose();
        }

        private void OnChannelUsersStatusUpdated(ChatEvents.ChannelUsersStatusUpdated @event)
        {
            if (@event.ChannelType != ChatChannel.ChatChannelType.USER) return;

            if (currentViewModel == null ||
                currentViewModel?.Id == null ||
                currentViewModel.ViewMode != TitlebarViewMode.DirectMessage) return;

            currentViewModel.IsOnline = @event.OnlineUsers.Contains(currentViewModel.Id);
            view.defaultTitlebarView.Setup(currentViewModel);
        }

        private void OnLiveUserConnectionStateChange(ChatEvents.UserStatusUpdatedEvent userStatusUpdatedEvent)
        {
            if (userStatusUpdatedEvent.ChannelType != ChatChannel.ChatChannelType.USER) return;

            if (currentViewModel == null ||
                currentViewModel?.Id == null ||
                currentViewModel.ViewMode != TitlebarViewMode.DirectMessage) return;

            if (currentViewModel.Id.Equals(userStatusUpdatedEvent.UserId, StringComparison.OrdinalIgnoreCase))
            {
                currentViewModel.IsOnline = userStatusUpdatedEvent.IsOnline;
                view.defaultTitlebarView.Setup(currentViewModel);
            }
        }

        private void OnProfileContextMenuRequested(UserProfileMenuRequest request)
        {
            request.WalletAddress = new Web3Address(currentViewModel.Id);

            chatContextMenuService
                .ShowUserProfileMenuAsync(request)
                .Forget();
        }

        private void OnCommunityContextMenuRequested(ShowContextMenuRequest request)
        {
            if (currentViewModel.ViewMode != TitlebarViewMode.Community) return;

            request.MenuConfiguration = contextMenuConfiguration;
            chatContextMenuService
                .ShowCommunityContextMenuAsync(request)
                .Forget();
        }

        private void OnChatContextMenuRequested(ShowChannelContextMenuRequest request)
        {
            if (contextMenuInstance == null)
                InitializeChannelContextMenu();

            var currentSetting = ChatUserSettings
                .GetNotificationPingValuePerChannel(currentChannelService.CurrentChannelId);

            for (int i = 0; i < notificationPingToggles.Length; ++i)
                notificationPingToggles[i].SetInitialValue(i == (int)currentSetting);

            request.MenuConfiguration = contextMenuInstance;
            chatContextMenuService.ShowChannelContextMenuAsync(request).Forget();
        }

        private void OnMemberCountUpdated(int memberCount)
        {
            string memberCountText = memberCount.ToString();

            view.defaultTitlebarView.SetMemberCount(memberCountText);
            view.membersTitlebarView.SetMemberCount(memberCountText);
        }

        private void OnCloseRequested()
        {
            eventBus.Publish(new ChatEvents.CloseChatEvent());
        }

        private void OnMembersToggleRequested()
        {
            eventBus.Publish(new ChatEvents.ToggleMembersEvent());
        }


        private async UniTaskVoid RefreshTitlebarCommunityThumbnailAsync(string? imageUrl)
        {
            thumbCts = thumbCts.SafeRestart();
            var ct = thumbCts.Token;

            if (currentViewModel == null) return;

            if (string.IsNullOrEmpty(imageUrl))
            {
                // Optional: clear or set default
                currentViewModel.SetThumbnail(ProfileThumbnailViewModel.ReadyToLoad());
                return;
            }

            var sprite = await getCommunityThumbnailCommand.ExecuteAsync(imageUrl, ct);
            if (ct.IsCancellationRequested) return;

            // Fallback to your default image if sprite is null
            var loaded = sprite != null
                ? ProfileThumbnailViewModel.FromLoaded(sprite, true)
                : ProfileThumbnailViewModel.ReadyToLoad(); // or build with your default sprite

            currentViewModel.SetThumbnail(loaded);

            // If your view is fully reactive on Thumbnail, no need to call Setup again.
            // If not, uncomment the next line:
            // view.defaultTitlebarView.Setup(currentViewModel);
        }

        private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt)
        {
            LoadTitlebarDataAsync(evt.Channel).Forget();
        }

        private async UniTaskVoid LoadTitlebarDataAsync(ChatChannel channel)
        {
            profileLoadCts = profileLoadCts.SafeRestart();
            var ct = profileLoadCts.Token;

            try
            {
                var loadingViewModel = ChatTitlebarViewModel.CreateLoading(channel.ChannelType switch
                {
                    ChatChannel.ChatChannelType.NEARBY => TitlebarViewMode.Nearby,
                    ChatChannel.ChatChannelType.USER => TitlebarViewMode.DirectMessage,
                    ChatChannel.ChatChannelType.COMMUNITY => TitlebarViewMode.Community,
                    _ => TitlebarViewMode.Nearby
                });

                view.defaultTitlebarView.Setup(loadingViewModel);

                var finalViewModel = await getTitlebarViewModel.ExecuteAsync(channel, ct);

                if (ct.IsCancellationRequested) return;

                currentViewModel = finalViewModel;
                view.defaultTitlebarView.Setup(finalViewModel);
                view.membersTitlebarView.SetChannelName(finalViewModel);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                view.defaultTitlebarView.Setup(new ChatTitlebarViewModel
                {
                    Username = "Error"
                });

                ReportHub.LogError(ReportCategory.UI, $"Titlebar load failed for channel {channel.Id}: {e}");
            }
        }

        public void ShowMembersView(bool isMemberListVisible)
        {
            view.SetMemberListMode(isMemberListVisible);
        }

        public void Show()
        {
            view.Show();
        }

        public void Hide()
        {
            view.Hide();
        }

        public void SetFocusState(bool isFocused, bool animate, float duration, Ease easing)
        {
            view.SetFocusedState(isFocused, animate, duration, easing);
        }

        private void OnNotificationPingOptionSelected(ChatAudioSettings selectedMode)
        {
            if (currentChannelService.CurrentChannel == null) return;

            ChatUserSettings.SetNotificationPintValuePerChannel(selectedMode,
                currentChannelService.CurrentChannel.Id);
        }

        private void InitializeChannelContextMenu()
        {
            var toggleGroup = view.gameObject.GetComponent<ToggleGroup>();
            notificationPingToggles = new ToggleWithCheckContextMenuControlSettings[3];

            var deleteChatHistoryButton =
                new ButtonContextMenuControlSettings(chatConfig.chatContextMenuSettings.DeleteChatHistoryText,
                    chatConfig.chatContextMenuSettings.DeleteChatHistorySprite,
                    OnDeleteChatHistoryButtonClicked);

            var subMenuSettings = new SubMenuContextMenuButtonSettings(
                chatConfig.chatContextMenuSettings.NotificationPingText,
                chatConfig.chatContextMenuSettings.NotificationPingSprite,
                new GenericContextMenu(chatConfig.chatContextMenuSettings.ContextMenuWidth,
                        verticalLayoutPadding: chatConfig.chatContextMenuSettings.VerticalPadding,
                        elementsSpacing: chatConfig.chatContextMenuSettings.ElementsSpacing,
                        offsetFromTarget: chatConfig.chatContextMenuSettings.NotificationPingSubMenuOffsetFromTarget,
                        anchorPoint:ContextMenuOpenDirection.TOP_LEFT)
                    .AddControl(notificationPingToggles[(int)ChatAudioSettings.ALL] =
                        new ToggleWithCheckContextMenuControlSettings("All Messages",
                            x => OnNotificationPingOptionSelected(ChatAudioSettings.ALL), toggleGroup))
                    .AddControl(notificationPingToggles[(int)ChatAudioSettings.MENTIONS_ONLY] =
                        new ToggleWithCheckContextMenuControlSettings("Mentions Only",
                            x => OnNotificationPingOptionSelected(ChatAudioSettings.MENTIONS_ONLY), toggleGroup))
                    .AddControl(notificationPingToggles[(int)ChatAudioSettings.NONE] =
                        new ToggleWithCheckContextMenuControlSettings("None",
                            x => OnNotificationPingOptionSelected(ChatAudioSettings.NONE), toggleGroup)));

            contextMenuInstance = new GenericContextMenu(
                    chatConfig.chatContextMenuSettings.ContextMenuWidth,
                    chatConfig.chatContextMenuSettings.OffsetFromTarget,
                    chatConfig.chatContextMenuSettings.VerticalPadding,
                    chatConfig.chatContextMenuSettings.ElementsSpacing,
                    anchorPoint: ContextMenuOpenDirection.TOP_LEFT)
                .AddControl(subMenuSettings)
                .AddControl(deleteChatHistoryButton);
        }
    }
}
