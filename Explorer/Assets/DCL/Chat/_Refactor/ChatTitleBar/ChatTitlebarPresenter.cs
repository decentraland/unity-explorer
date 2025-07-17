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
using DG.Tweening;
using Utilities;
using Utility;

public class ChatTitlebarPresenter : IDisposable
{
    private readonly ChatTitlebarView2 view;
    private readonly IEventBus eventBus;
    private readonly GetTitlebarViewModelCommand getTitlebarViewModel;
    private readonly ChatMemberListService chatMemberListService;
    
    CancellationTokenSource profileLoadCts = new ();
    private readonly EventSubscriptionScope scope = new();

    public ChatTitlebarPresenter(
        ChatTitlebarView2 view,
        IEventBus eventBus,
        ChatMemberListService chatMemberListService,
        GetTitlebarViewModelCommand getTitlebarViewModel)
    {
        this.view = view;
        this.eventBus = eventBus;
        this.chatMemberListService = chatMemberListService;
        this.getTitlebarViewModel = getTitlebarViewModel;
        
        view.Initialize();
        view.OnCloseRequested += OnCloseRequested;
        view.OnMembersToggleRequested += OnMembersToggleRequested;
        
        chatMemberListService.OnMemberCountUpdated += OnChannelMembersUpdated;
        
        scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
    }

    private void OnChannelMembersUpdated(int memberCount)
    {
        view.defaultTitlebarView.SetMemberCount(memberCount.ToString());
        view.membersTitlebarView.SetMemberCount(memberCount.ToString());
    }

    private void OnCloseRequested() => eventBus.Publish(new ChatEvents.CloseChatEvent());
    private void OnMembersToggleRequested() => eventBus.Publish(new ChatEvents.ToggleMembersEvent());
    private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt) => LoadTitlebarDataAsync(evt.Channel).Forget();
    
    private async UniTaskVoid LoadTitlebarDataAsync(ChatChannel channel)
    {
        profileLoadCts = profileLoadCts.SafeRestart();
        var ct = profileLoadCts.Token;

        try
        {
            var loadingViewModel = ChatTitlebarViewModel
                .CreateLoading(channel.ChannelType == ChatChannel.ChatChannelType.NEARBY ?
                    Mode.Nearby : Mode.DirectMessage);
            view.defaultTitlebarView.Setup(loadingViewModel);
            
            var finalViewModel = await getTitlebarViewModel.ExecuteAsync(channel, ct);
            if (ct.IsCancellationRequested) return;

            view.defaultTitlebarView.Setup(finalViewModel);
        }
        catch (OperationCanceledException) { /* ignored */ }
        catch (Exception e)
        {
            view.defaultTitlebarView.Setup(new ChatTitlebarViewModel
            {
                Username = "Error"
            });
            ReportHub.LogError(ReportCategory.UI, $"Titlebar load failed for channel {channel.Id}: {e}");
        }
    }

    public void ShowMembersView(bool isMemberListVisible) => view.SetMemberListMode(isMemberListVisible);
    public void Show() => view.Show();
    public void Hide() => view.Hide();
    public void SetFocusState(bool isFocused, bool animate, float duration, Ease easing) => view.SetFocusedState(isFocused, animate, duration, easing);

    public void Dispose()
    {
        if (view != null)
        {
            view.OnCloseRequested -= OnCloseRequested;
            view.OnMembersToggleRequested -= OnMembersToggleRequested;
            chatMemberListService.OnMemberCountUpdated -= OnChannelMembersUpdated;
        }
        
        profileLoadCts.SafeCancelAndDispose();
        scope.Dispose();
    }
}