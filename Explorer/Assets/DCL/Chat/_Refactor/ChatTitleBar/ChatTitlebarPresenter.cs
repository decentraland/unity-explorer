using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatServices.ChatContextService;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.UI.Communities;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.GenericContextMenuParameter;
using DCL.Web3;
using DG.Tweening;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Chat
{
    public class ChatTitlebarPresenter : IDisposable
    {
        private readonly ChatTitlebarView2 view;
        private readonly ChatConfig.ChatConfig chatConfig;
        private readonly IEventBus eventBus;
        private readonly CommunityDataService communityDataService;
        private readonly GetTitlebarViewModelCommand getTitlebarViewModel;
        private readonly DeleteChatHistoryCommand deleteChatHistoryCommand;
        private readonly ICurrentChannelService currentChannelService;
        private readonly ChatContextMenuService chatContextMenuService;
        private readonly ChatMemberListService chatMemberListService;
        private readonly CancellationTokenSource lifeCts = new ();
        private readonly EventSubscriptionScope scope = new ();
        private CancellationTokenSource profileLoadCts = new ();
        private CancellationTokenSource? activeMenuCts;
        private UniTaskCompletionSource? activeMenuTcs;

        private ChatTitlebarViewModel currentViewModel { get; set; }

        private readonly GenericContextMenu contextMenuConfiguration;

        public ChatTitlebarPresenter(
            ChatTitlebarView2 view,
            ChatConfig.ChatConfig chatConfig,
            IEventBus eventBus,
            CommunityDataService communityDataService,
            ICurrentChannelService currentChannelService,
            ChatMemberListService chatMemberListService,
            ChatContextMenuService chatContextMenuService,
            ChatClickDetectionService chatClickDetectionService,
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

            scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));

            CommunityChatConversationContextMenuSettings contextMenuSettings = chatConfig.communityChatConversationContextMenuSettings;

            contextMenuConfiguration = new GenericContextMenu(contextMenuSettings.Width,
                    contextMenuSettings.Offset,
                    contextMenuSettings.VerticalLayoutPadding,
                    contextMenuSettings.ElementsSpacing,
                    ContextMenuOpenDirection.TOP_LEFT)
               .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.ViewCommunityText,
                    contextMenuSettings.ViewCommunitySprite,
                    OpenCommunityCard));
        }

        private void OpenCommunityCard()
        {
            communityDataService
               .OpenCommunityCard(currentChannelService.CurrentChannel);
        }

        public void Dispose()
        {
            view.OnCloseRequested -= OnCloseRequested;
            view.OnMembersToggleRequested -= OnMembersToggleRequested;
            view.OnProfileContextMenuRequested -= OnProfileContextMenuRequested;
            view.OnCommunityContextMenuRequested -= OnCommunityContextMenuRequested;
            chatMemberListService.OnMemberCountUpdated -= OnMemberCountUpdated;

            lifeCts.SafeCancelAndDispose();
            profileLoadCts.SafeCancelAndDispose();
            scope.Dispose();
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
            Debug.Log("OnCommunityContextMenuRequested");
            if (currentViewModel.ViewMode != TitlebarViewMode.Community) return;

            request.MenuConfiguration = contextMenuConfiguration;

            chatContextMenuService
               .ShowCommunityContextMenuAsync(request)
               .Forget();
        }

        private void OnChatContextMenuRequested(ChatContextMenuRequest data)
        {
            var options = new ChatOptionsContextMenuData
            {
                DeleteChatHistoryText = chatConfig.DeleteChatHistoryContextMenuText, DeleteChatHistoryIcon = chatConfig.ClearChatHistoryContextMenuIcon,
            };

            data.contextMenuData = options;
            data.OnDeleteHistory = deleteChatHistoryCommand.Execute;
            chatContextMenuService.ShowChannelOptionsAsync(data).Forget();
        }

        private void OnMemberCountUpdated(int memberCount)
        {
            var memberCountText = memberCount.ToString();

            view.defaultTitlebarView.SetMemberCount(memberCountText);
            view.membersTitlebarView.SetMemberCount(memberCountText);
        }

        private void OnCloseRequested() =>
            eventBus.Publish(new ChatEvents.CloseChatEvent());

        private void OnMembersToggleRequested() =>
            eventBus.Publish(new ChatEvents.ToggleMembersEvent());

        private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt) =>
            LoadTitlebarDataAsync(evt.Channel).Forget();

        private async UniTaskVoid LoadTitlebarDataAsync(ChatChannel channel)
        {
            profileLoadCts = profileLoadCts.SafeRestart();
            CancellationToken ct = profileLoadCts.Token;

            try
            {
                var loadingViewModel = ChatTitlebarViewModel.CreateLoading(channel.ChannelType switch
                                                                           {
                                                                               ChatChannel.ChatChannelType.NEARBY => TitlebarViewMode.Nearby,
                                                                               ChatChannel.ChatChannelType.USER => TitlebarViewMode.DirectMessage,
                                                                               ChatChannel.ChatChannelType.COMMUNITY => TitlebarViewMode.Community,
                                                                               _ => TitlebarViewMode.Nearby,
                                                                           });

                view.defaultTitlebarView.Setup(loadingViewModel);

                ChatTitlebarViewModel? finalViewModel = await getTitlebarViewModel.ExecuteAsync(channel, ct);

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
                    Username = "Error",
                });

                ReportHub.LogError(ReportCategory.UI, $"Titlebar load failed for channel {channel.Id}: {e}");
            }
        }

        public void ShowMembersView(bool isMemberListVisible)
        {
            view.SetMemberListMode(isMemberListVisible);
        }

        public void Show() =>
            view.Show();

        public void Hide() =>
            view.Hide();

        public void SetFocusState(bool isFocused, bool animate, float duration, Ease easing) =>
            view.SetFocusedState(isFocused, animate, duration, easing);
    }
}
