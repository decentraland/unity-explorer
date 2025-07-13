using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat;
using DCL.Chat.ChatUseCases;
using DCL.Chat.EventBus;
using DCL.Chat.Services;
using DG.Tweening;
using Utility;

public class ChatMessageFeedPresenter : IDisposable
{
    private readonly IChatMessageFeedView view;
    private readonly IEventBus eventBus;
    private readonly ICurrentChannelService currentChannelService;
    private readonly GetMessageHistoryUseCase getMessageHistoryUseCase;
    private readonly CreateMessageViewModelUseCase createMessageViewModelUseCase;
    private readonly MarkChannelAsReadUseCase markChannelAsReadUseCase;
    
    private readonly EventSubscriptionScope scope = new();
    private CancellationTokenSource loadChannelCts = new();
    
    public ChatMessageFeedPresenter(IChatMessageFeedView view,
        IEventBus eventBus,
        ICurrentChannelService currentChannelService,
        GetMessageHistoryUseCase getMessageHistoryUseCase,
        CreateMessageViewModelUseCase createMessageViewModelUseCase,
        MarkChannelAsReadUseCase markChannelAsReadUseCase)
    {
        this.view = view;
        this.eventBus = eventBus;
        this.currentChannelService = currentChannelService;
        this.getMessageHistoryUseCase = getMessageHistoryUseCase;
        this.createMessageViewModelUseCase = createMessageViewModelUseCase;
        this.markChannelAsReadUseCase = markChannelAsReadUseCase;
        
        scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
        scope.Add(eventBus.Subscribe<ChatEvents.MessageReceivedEvent>(OnMessageReceived));
    }

    private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt)
    {
        loadChannelCts = loadChannelCts.SafeRestart();
        LoadChannelAsync(evt.Channel.Id, loadChannelCts.Token).Forget();
    }

    public void Activate()
    {
        view.OnScrollToBottom += MarkCurrentChannelAsRead;
    }

    public void Deactivate()
    {
        view.OnScrollToBottom -= MarkCurrentChannelAsRead;
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
        view.SetFocusedState(isFocused, animate, duration,easing);
    }

    private async UniTask LoadChannelAsync(ChatChannel.ChannelId channelId, CancellationToken token)
    {
        var result = await getMessageHistoryUseCase.ExecuteAsync(channelId, token);

        if (token.IsCancellationRequested) return;

        view.SetMessages(result.Messages);
        view.ScrollToBottom();
    }

    private void OnMessageReceived(ChatEvents.MessageReceivedEvent evt)
    {
        if (!currentChannelService.CurrentChannelId.Equals(evt.ChannelId))
            return;

        var viewModel = createMessageViewModelUseCase.Execute(evt.Message);
        view.AppendMessage(viewModel, true);

        if (view.IsAtBottom())
            MarkCurrentChannelAsRead();
    }

    private void MarkCurrentChannelAsRead()
    {
        markChannelAsReadUseCase.Execute(currentChannelService.CurrentChannelId);
    }
    
    public void Dispose()
    {
        loadChannelCts.SafeCancelAndDispose();
        Deactivate();
    }
}