using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.ChatUseCases;
using DCL.Chat.Services;
using Utilities;
using Utility;

public class ChatMemberListPresenter : IDisposable
{
    private readonly ChannelMemberFeedView view;
    private readonly IEventBus eventBus;
    private readonly GetChannelMembersCommand _getChannelMembersCommand;
    private readonly ChatMemberListService memberListService;
    private readonly EventSubscriptionScope scope = new ();
    private CancellationTokenSource cts = new ();

    public ChatMemberListPresenter(
        ChannelMemberFeedView view,
        IEventBus eventBus,
        ChatMemberListService memberListService,
        GetChannelMembersCommand getChannelMembersCommand)
    {
        this.view = view;
        this.eventBus = eventBus;
        this.memberListService = memberListService;
        this._getChannelMembersCommand = getChannelMembersCommand;
        
        this.view.OnMemberContextMenuRequested += OnMemberContextMenuRequested;
    }

    private async UniTaskVoid LoadMembersAsync()
    {
        cts = cts.SafeRestart();
        
        try
        {
            view.SetLoading(true);
            var memberViewModels = await _getChannelMembersCommand.ExecuteAsync(cts.Token);
            if (cts.IsCancellationRequested) return;
            view.SetData(memberViewModels);
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            view.SetLoading(false);
        }
    }
    
    private void OnMemberCountUpdated(int memberCount)
    {
        view.SetMemberCount(memberCount);
    }

    private void HandleLiveUpdate(IReadOnlyList<ChatMemberListView.MemberData> members)
    {
        
    }
    
    public void ShowAndLoad()
    {
        view.Show();
        LoadMembersAsync().Forget();
        // TODO: use memberListService.LastKnownMemberList
        memberListService.OnMemberListUpdated += HandleLiveUpdate;
    }
    
    public void Show() => ShowAndLoad();

    public void Hide()
    {
        view.Hide();
        memberListService.OnMemberListUpdated -= HandleLiveUpdate;
    }

    private void OnMemberContextMenuRequested(string obj)
    {
        
    }
    
    public void Dispose()
    {
        if (memberListService != null)
            memberListService.OnMemberListUpdated -= HandleLiveUpdate;
        
        cts.SafeCancelAndDispose();
        scope.Dispose();
    }
}