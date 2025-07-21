using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.ChatUseCases;
using DCL.Chat.ChatViewModels;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Chat.Services;
using DCL.Diagnostics;
using DCL.Web3;
using DG.Tweening;
using MVC;
using System.Collections.Generic;
using Utility;

public class ChatTitlebarPresenter : IDisposable
{
    private readonly ChatTitlebarView2 view;
    private readonly ChatConfig chatConfig;
    private readonly IEventBus eventBus;
    private readonly GetTitlebarViewModelCommand getTitlebarViewModel;
    private readonly DeleteChatHistoryCommand deleteChatHistoryCommand;
    private readonly ChatContextMenuService chatContextMenuService;
    private readonly ChatMemberListService chatMemberListService;
    private readonly CancellationTokenSource lifeCts = new ();
    private readonly EventSubscriptionScope scope = new ();
    private CancellationTokenSource profileLoadCts = new ();
    private CancellationTokenSource? activeMenuCts;
    private UniTaskCompletionSource? activeMenuTcs;

    private ChatTitlebarViewModel currentViewModel { get; set; }

    public ChatTitlebarPresenter(
        ChatTitlebarView2 view,
        ChatConfig chatConfig,
        IEventBus eventBus,
        ChatMemberListService chatMemberListService,
        ChatContextMenuService chatContextMenuService,
        ChatClickDetectionService chatClickDetectionService,
        GetTitlebarViewModelCommand getTitlebarViewModel,
        DeleteChatHistoryCommand deleteChatHistoryCommand)
    {
        this.view = view;
        this.chatConfig = chatConfig;
        this.eventBus = eventBus;
        this.chatMemberListService = chatMemberListService;
        this.chatContextMenuService = chatContextMenuService;
        this.getTitlebarViewModel = getTitlebarViewModel;
        this.deleteChatHistoryCommand = deleteChatHistoryCommand;

        view.Initialize();
        view.OnCloseRequested += OnCloseRequested;
        view.OnMembersToggleRequested += OnMembersToggleRequested;
        view.OnContextMenuRequested += OnChatContextMenuRequested;
        view.OnProfileContextMenuRequested += OnProfileContextMenuRequested;

        chatMemberListService.OnMemberListUpdated += OnMemberListUpdated;

        scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
    }

    public void Dispose()
    {
        view.OnCloseRequested -= OnCloseRequested;
        view.OnMembersToggleRequested -= OnMembersToggleRequested;
        chatMemberListService.OnMemberListUpdated -= OnMemberListUpdated;

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

    private void OnMemberListUpdated(IReadOnlyList<ChatMemberListView.MemberData> memberList)
    {
        var memberCount = memberList.Count.ToString();

        view.defaultTitlebarView.SetMemberCount(memberCount);
        view.membersTitlebarView.SetMemberCount(memberCount);
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
            var loadingViewModel = ChatTitlebarViewModel
               .CreateLoading(channel.ChannelType == ChatChannel.ChatChannelType.NEARBY ? Mode.Nearby : Mode.DirectMessage);

            view.defaultTitlebarView.Setup(loadingViewModel);

            ChatTitlebarViewModel? finalViewModel = await getTitlebarViewModel.ExecuteAsync(channel, ct);
            if (ct.IsCancellationRequested) return;

            view.defaultTitlebarView.Setup(finalViewModel);
            currentViewModel = finalViewModel;
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

    public void ShowMembersView(bool isMemberListVisible) =>
        view.SetMemberListMode(isMemberListVisible);

    public void Show() =>
        view.Show();

    public void Hide() =>
        view.Hide();

    public void SetFocusState(bool isFocused, bool animate, float duration, Ease easing) =>
        view.SetFocusedState(isFocused, animate, duration, easing);
}
