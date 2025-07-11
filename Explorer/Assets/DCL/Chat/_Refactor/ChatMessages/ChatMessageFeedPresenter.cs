using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.Web3.Identities;
using DCL.Chat;
using DCL.Diagnostics;
using DCL.UI.InputFieldFormatting;
using DG.Tweening;
using Utility;

public class ChatMessageFeedPresenter : IDisposable
{
    private readonly IChatMessageFeedView view;
    private readonly IChatHistory chatHistory;
    private readonly ChatHistoryStorage chatHistoryStorage;
    private readonly IProfileCache profileCache;
    private readonly IWeb3IdentityCache web3IdentityCache;
    private readonly ITextFormatter hyperlinkFormatter;
    
    private ChatChannel currentChannel;
    
    public ChatMessageFeedPresenter(
        IChatMessageFeedView view,
        IChatHistory chatHistory,
        ChatHistoryStorage chatHistoryStorage,
        IProfileCache profileCache,
        IWeb3IdentityCache web3IdentityCache,
        ITextFormatter hyperlinkFormatter)
    {
        this.view = view;
        this.chatHistory = chatHistory;
        this.chatHistoryStorage = chatHistoryStorage;
        this.profileCache = profileCache;
        this.web3IdentityCache = web3IdentityCache;
        this.hyperlinkFormatter = hyperlinkFormatter;
    }

    public void Activate()
    {
        // This presenter might not need to subscribe to anything globally
        // as the coordinator will push messages to it via OnMessageReceived.
        view.OnScrollToBottom += MarkCurrentChannelAsRead;
    }

    public void Deactivate()
    {
        view.OnScrollToBottom -= MarkCurrentChannelAsRead;
    }

    public async void LoadChannel(ChatChannel channel)
    {
        loadChannelCts = loadChannelCts.SafeRestart();
        var token = loadChannelCts.Token;

        try
        {
            await LoadChannelAsync(channel, token);
        }
        catch (OperationCanceledException)
        {
            // This is expected if the user clicks another channel while this one is loading.
            // We can safely ignore it.
        }
        catch (Exception ex)
        {
            // Handle any other unexpected errors during loading
            ReportHub.LogException(ex, $"Failed to load channel: {channel.Id.Id}");
            // Optionally, show an error state in the view
            // view.ShowError("Failed to load message history.");
        }
    }
    private CancellationTokenSource loadChannelCts = new();
    public async UniTask LoadChannelAsync(ChatChannel channel, CancellationToken token)
    {
        currentChannel = channel;
        view.Clear();
        if (chatHistoryStorage != null && !chatHistoryStorage.IsChannelInitialized(channel.Id))
        {
            // Pass the token down to the storage layer.
            // This ensures that if this operation is cancelled, the disk read stops.
            await chatHistoryStorage.InitializeChannelWithMessagesAsync(channel.Id);
        }

        if (token.IsCancellationRequested)
            return;
        
        // An alternative to the if-check that is more idiomatic with async/await.
        // This will throw the OperationCanceledException caught in the public method.
        token.ThrowIfCancellationRequested();
        
        var messagesToDisplay = new List<ChatMessage>();
        var processedMessages = new List<ChatMessage>(channel.Messages);

        // Filter out the old padding elements. The view shouldn't know about them.
        processedMessages.RemoveAll(msg => msg.IsPaddingElement);

        int unreadCount = processedMessages.Count - channel.ReadMessages;
        bool needsSeparator = unreadCount > 0 && channel.ReadMessages > 0;

        if (needsSeparator)
        {
            // Our message list is ordered oldest-to-newest after removing padding.
            // We need to calculate the correct read message count *without* the padding.
            // Let's assume ChatChannel.ReadMessages also includes padding. We need to be careful.
            // A safer way is to count non-padding messages.
            int nonPaddingMessagesCount = 0;
            for (int i = 0; i < channel.ReadMessages; i++)
            {
                if (!channel.Messages[i].IsPaddingElement)
                    nonPaddingMessagesCount++;
            }

            // Insert separator at the position of the first unread message.
            if (nonPaddingMessagesCount < processedMessages.Count)
                processedMessages.Insert(nonPaddingMessagesCount, ChatMessage.NewSeparator());
        }

        // Format the final list for display
        foreach (var msg in processedMessages)
        {
            messagesToDisplay.Add(FormatMessage(msg));
        }

        view.SetMessages(messagesToDisplay);
        view.ScrollToBottom();
    }

    public void OnMessageReceived(ChatChannel channel, ChatMessage message)
    {
        if (currentChannel == null || channel.Id.Id != currentChannel.Id.Id)
            return;

        // The presenter formats the message before giving it to the view
        view.AppendMessage(FormatMessage(message), true);

        if (view.IsAtBottom())
            MarkCurrentChannelAsRead();
    }

    /// <summary>
    /// The presenter is still responsible for transforming data.
    /// Here, it formats the message text for hyperlinks.
    /// </summary>
    private ChatMessage FormatMessage(ChatMessage originalMessage)
    {
        if (originalMessage.IsSystemMessage)
            return originalMessage;

        string formattedText = hyperlinkFormatter.FormatText(originalMessage.Message);
        return ChatMessage.CopyWithNewMessage(formattedText, originalMessage);
    }
    
    private void MarkCurrentChannelAsRead()
    {
        currentChannel?.MarkAllMessagesAsRead();
    }
    
    public void Dispose()
    {
        loadChannelCts.SafeCancelAndDispose();
        Deactivate();
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
}