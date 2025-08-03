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
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatServices.ChatContextService;
using DCL.Communities;
using DCL.Settings.Settings;
using DCL.UI.Communities;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.GenericContextMenuParameter;
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
        private readonly IChatUserStateEventBus chatUserStateEventBus;
        private readonly CommunityDataService communityDataService;
        private readonly GetTitlebarViewModelCommand getTitlebarViewModel;
        private readonly DeleteChatHistoryCommand deleteChatHistoryCommand;
        private readonly CurrentChannelService currentChannelService;
        private readonly ChatContextMenuService chatContextMenuService;
        private readonly ChatMemberListService chatMemberListService;
        private readonly CancellationTokenSource lifeCts = new ();
        private readonly EventSubscriptionScope scope = new ();
        private CancellationTokenSource profileLoadCts = new ();
        private CancellationTokenSource? activeMenuCts;
        private UniTaskCompletionSource? activeMenuTcs;
        private ChatTitlebarViewModel? currentViewModel { get; set; }
        private GenericContextMenu? contextMenuInstance;
        private readonly GenericContextMenu contextMenuConfiguration;
        private ToggleWithCheckContextMenuControlSettings[] notificationPingToggles;

        public ChatTitlebarPresenter(
            ChatTitlebarView2 view,
            ChatConfig.ChatConfig chatConfig,
            IEventBus eventBus,
            CommunityDataService communityDataService,
            CurrentChannelService currentChannelService,
            ChatMemberListService chatMemberListService,
            ChatContextMenuService chatContextMenuService,
            GetTitlebarViewModelCommand getTitlebarViewModel,
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
            this.deleteChatHistoryCommand = deleteChatHistoryCommand;

            view.Initialize();
            view.OnCloseRequested += OnCloseRequested;
            view.OnMembersToggleRequested += OnMembersToggleRequested;
            view.OnContextMenuRequested += OnChatContextMenuRequested;
            view.OnProfileContextMenuRequested += OnProfileContextMenuRequested;
            view.OnCommunityContextMenuRequested += OnCommunityContextMenuRequested;

            chatMemberListService.OnMemberCountUpdated += OnMemberCountUpdated;

            scope.Add(this.eventBus.Subscribe<ChatEvents.UserStatusUpdatedEvent>(OnLiveUserConnectionStateChange));
            scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));

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

        private void OnLiveUserConnectionStateChange(ChatEvents.UserStatusUpdatedEvent userStatusUpdatedEvent)
        {
            if (currentViewModel == null ||
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
            var toggleGroup = view.gameObject.AddComponent<ToggleGroup>();
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
                        offsetFromTarget: chatConfig.chatContextMenuSettings.NotificationPingSubMenuOffsetFromTarget)
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
